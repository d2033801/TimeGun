using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Utility;

namespace TimeRewind
{
    /// <summary>
    /// 敌人回溯组件：
    /// - 继承 AbstractTimeRewindObject（Transform 已由基类录制/回放）
    /// - 额外录制并回放：NavMeshAgent、Animator、Enemy 自身关键变量（状态机/计时/巡逻索引/死亡等）
    /// - 回溯期间会临时"冻结"导航与动画推进，防止引擎在我们手动回写历史时施加自动行为
    /// </summary>
    public class EnemyTimeRewind : AbstractTimeRewindObject
    {
        //==================== 组件缓存 ====================
        // 通过 [SerializeField] 允许在 Inspector 手动绑定（可减少 GetComponent 开销），也支持运行时懒加载
        [SerializeField, Tooltip("敌人身上的NavMeshAgent")]
        private NavMeshAgent agent;

        [SerializeField, Tooltip("敌人身上的Animator组件")]
        private Animator anim;

        [SerializeField, Tooltip("敌人的Enemy控制脚本")]
        private Enemy enemy;

        private NavMeshAgent Agent => agent ??= GetComponent<NavMeshAgent>();
        private Animator Anim => anim ??= GetComponent<Animator>();
        private Enemy TheEnemy => enemy ??= GetComponent<Enemy>();

        //==================== 历史缓冲与最近一次快照 ====================
        // NavMeshAgent 历史环形缓冲（固定容量，先进先出）
        private RingBuffer<AgentSnapshot> _agentHistory;
        private AgentSnapshot _lastAppliedAgentSnap; // 记录最后一次应用的快照，用于 StopRewind 时恢复

        // Animator 历史（封装在内部 Recorder，避免此类过大）
        private AnimatorRecorder _animRecorder;

        // Enemy 自身历史（状态、计时等）
        private RingBuffer<EnemySnapshot> _enemyHistory;
        private EnemySnapshot _lastAppliedEnemySnap;

        //==================== 回溯/暂停期间的运行态标记 ====================
        /// <summary>
        /// 回溯/暂停期间用于保存 NavMeshAgent 原始配置的标记结构体
        /// 用途：在 OnStartRewind/OnStartPause 时保存原始状态，在 StopRewind/StopPause 时恢复
        /// </summary>
        private struct AgentRuntimeFlags
        {
            public bool hadAgent; // 启动时是否存在 Agent（用于判断恢复时是否需要处理）
            public bool origUpdatePosition; // 原始 updatePosition
            public bool origUpdateRotation; // 原始 updateRotation
            public bool origIsStopped; // 原始 isStopped
            public bool origAutoBraking; // 原始 autoBraking
        }

        private AgentRuntimeFlags _agentFlags;
        private float _animOriginalSpeed = 1f; // 原始 Animator 播放速度

        private bool _enemyHadComponent; // 启动时是否存在 Enemy 组件
        private bool _enemyWasEnabled; // 原始 Enemy.enabled 值

        /// <summary>
        /// NavMeshAgent 的运行快照（仅涉及可回放的"配置/目标值"，真实位置由 Transform 快照恢复）
        /// 用途：录制每一帧 NavMeshAgent 的运动参数，回放时恢复这些参数以重现 AI 行为
        /// </summary>
        private struct AgentSnapshot
        {
            public bool IsStopped; // 导航是否停止（影响 Agent 是否继续寻路）
            public bool AutoBraking; // 是否自动减速（影响到达目标点时的行为）
            public float Speed; // 移动速度（单位：米/秒，影响移动快慢）
            public float AngularSpeed; // 转向速度（单位：度/秒，影响转身快慢）
            public float Acceleration; // 加速度（单位：米/秒²，影响加速/减速过程）
            public Vector3 Destination; // 导航目标点（寻路系统的目的地坐标）
        }

        /// <summary>
        /// Enemy 的关键运行态快照（仅与逻辑/动画驱动相关，不包含 Transform）
        /// 用途：录制每一帧 Enemy 脚本的关键状态，回放时恢复完整的 AI 逻辑状态
        /// </summary>
        private struct EnemySnapshot
        {
            public bool IsDead; // 敌人是否死亡（影响状态机行为与动画）
            public float StateTimer; // 当前状态的计时器（用于状态持续时间，如 Idle 等待时间）
            public int CurrentPointIndex; // 当前巡逻点索引（影响巡逻路径，-1 表示未初始化）
            public EnemyStateId StateId; // 当前状态机状态的 ID（用于切换到对应状态实例）

            // 速度平滑相关（直接访问 public 属性）
            public float CurrentSpeed; // 当前平滑后的移动速度（用于驱动 Animator 的 Speed 参数）
            public float SpeedVelocity; // SmoothDamp 内部使用的速度变化率（用于平滑过渡）

            public float SeePlayerTimer; // 发现玩家后的持续观察时间（影响警戒状态持续时长）
        }

        // 轻量状态枚举：用于"识别/切换"状态实例，避免直接序列化引用
        private enum EnemyStateId : byte
        {
            None = 0,
            Idle = 1,
            Patrol = 2,
            Alert = 3,
            Death = 4
        }

        /// <summary>
        /// 组件初始化：创建各环形缓冲，构建 AnimatorRecorder，并缓存反射字段
        /// </summary>
        protected override void MainInit()
        {
            base.MainInit();

            _agentHistory = RewindInit<AgentSnapshot>(out _);
            _enemyHistory = RewindInit<EnemySnapshot>(out _);

            // Animator 使用独立的 Recorder，但沿用相同容量与生命周期
            var animBuffer = RewindInit<AnimatorRecorder.Snapshot>(out _);
            _animRecorder = new AnimatorRecorder(Anim, animBuffer);
        }

        /// <summary>
        /// 录制一帧快照（Transform 已由基类处理，这里追加 NavMeshAgent / Animator / Enemy）
        /// </summary>
        protected override void RecordOneSnap()
        {
            base.RecordOneSnap();

            // 录制 NavMeshAgent 的"配置/目标值"
            if (Agent != null)
            {
                var snap = new AgentSnapshot
                {
                    IsStopped = Agent.isStopped,
                    AutoBraking = Agent.autoBraking,
                    Speed = Agent.speed,
                    AngularSpeed = Agent.angularSpeed,
                    Acceleration = Agent.acceleration,
                    Destination = SafeGetAgentDestination(Agent) // 访问 destination 可能抛异常，做了防护
                };
                _agentHistory.Push(snap);
            }

            // 录制 Animator（层状态 + normalizedTime + 参数）
            _animRecorder?.RecordOneSnap();

            // 录制 Enemy 自身状态（死亡、状态机、计时等）
            if (TheEnemy != null)
            {
                var es = new EnemySnapshot
                {
                    IsDead = TheEnemy.IsDead,
                    StateTimer = TheEnemy.StateTimer,
                    CurrentPointIndex = TheEnemy.CurrentPointIndex,
                    StateId = GetCurrentStateId(TheEnemy),
                    CurrentSpeed = TheEnemy.CurrentSpeed, // 直接访问 public 属性
                    SpeedVelocity = TheEnemy.SpeedVelocity, // 直接访问 public 属性
                    SeePlayerTimer = TheEnemy.SeePlayerTimer
                };

                _enemyHistory.Push(es);
            }
        }

        /// <summary>
        /// 回放一帧快照（先 Transform，再 Agent/Animator/Enemy，避免驱动类组件"跑到旧位置"）
        /// </summary>
        protected override void RewindOneSnap()
        {
            // 先回放 Transform（位置、旋转、缩放）
            base.RewindOneSnap();

            // 再回放 NavMeshAgent 配置
            if (Agent != null && _agentHistory != null && _agentHistory.Count > 0)
            {
                var snap = _agentHistory.PopBack();
                ApplyAgentSnapshotDuringRewind(snap);
                _lastAppliedAgentSnap = snap; // 记录以便 StopRewind 时恢复
            }

            // 回放 Animator（状态/参数）
            _animRecorder?.RewindOneSnap();

            // 回放 Enemy 自身（死亡/状态机/计时/平滑）
            if (TheEnemy != null && _enemyHistory != null && _enemyHistory.Count > 0)
            {
                var snap = _enemyHistory.PopBack();
                ApplyEnemySnapshotDuringRewind(TheEnemy, snap);
                _lastAppliedEnemySnap = snap;
            }
        }

        /// <summary>
        /// 每步回放后对齐一次 NavMeshAgent 的 nextPosition/velocity，防止物理/寻路产生"拉扯"
        /// </summary>
        protected override void OnAppliedSnapshotDuringRewind()
        {
            base.OnAppliedSnapshotDuringRewind();

            // 仅在 Agent 生效且 updatePosition 被我们关闭时对齐 nextPosition
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh && !Agent.updatePosition)
            {
                Agent.nextPosition = transform.position; // 令内部寻路位置跟随 Transform
                Agent.velocity = Vector3.zero; // 清零速度，避免残留速度影响
            }
        }

        /// <summary>
        /// 回溯开始：冻结 NavMesh 自动更新、冻结 Animator 推进、暂停 Enemy Update 驱动
        /// </summary>
        protected override void OnStartRewind()
        {
            base.OnStartRewind();
            FreezeAllComponents(); // 复用冻结逻辑
        }

        /// <summary>
        /// 回溯结束：恢复 NavMeshAgent 配置与 Animator 速度，恢复 Enemy Update
        /// </summary>
        protected override void OnStopRewind()
        {
            base.OnStopRewind();
            UnfreezeAllComponents(true); // 复用恢复逻辑，参数true表示恢复到回溯快照状态
        }

        /// <summary>
        /// 暂停开始：冻结 NavMesh、Animator、Enemy（复用回溯的冻结逻辑）
        /// </summary>
        protected override void OnStartPause()
        {
            base.OnStartPause();
            FreezeAllComponents(); // 复用冻结逻辑
        }

        /// <summary>
        /// 暂停结束：恢复所有组件到暂停前的状态
        /// </summary>
        protected override void OnStopPause()
        {
            base.OnStopPause();
            UnfreezeAllComponents(false); // 复用恢复逻辑，参数false表示恢复到原始状态
        }

        /// <summary>
        /// 冻结所有组件（NavMeshAgent、Animator、Enemy）
        /// 被 OnStartRewind 和 OnStartPause 复用
        /// </summary>
        private void FreezeAllComponents()
        {
            // 暂停 NavMesh 自动行为，记录原始配置
            if (Agent != null)
            {
                _agentFlags.hadAgent = true;
                _agentFlags.origIsStopped = Agent.isStopped;
                _agentFlags.origUpdatePosition = Agent.updatePosition;
                _agentFlags.origUpdateRotation = Agent.updateRotation;
                _agentFlags.origAutoBraking = Agent.autoBraking;

                // 仅在 Agent 可用且位于 NavMesh 时设置 isStopped，避免报错
                SafeSetStopped(Agent, true);
                Agent.updatePosition = false; // 我们手动回写位置
                Agent.updateRotation = false; // 我们手动回写旋转（随 Transform）
            }

            // 暂停 Animator（speed=0，仍可通过 Play+Update(0) 立即切换）
            if (Anim != null)
            {
                _animOriginalSpeed = Anim.speed;
                Anim.speed = 0f;
            }

            // 暂停 Enemy 行为逻辑（状态机不再推进，防止与回放冲突）
            if (TheEnemy != null)
            {
                _enemyHadComponent = true;
                _enemyWasEnabled = TheEnemy.enabled;
                TheEnemy.enabled = false;
            }
        }

        /// <summary>
        /// 解冻所有组件（NavMeshAgent、Animator、Enemy）
        /// 被 OnStopRewind 和 OnStopPause 复用
        /// </summary>
        /// <param name="restoreToSnapshot">
        /// true = 恢复到回溯快照状态（用于回溯结束）
        /// false = 恢复到冻结前的原始状态（用于暂停结束）
        /// </param>
        private void UnfreezeAllComponents(bool restoreToSnapshot)
        {
            // 恢复 NavMeshAgent（对齐位置 -> 恢复配置 -> 恢复 isStopped）
            if (_agentFlags.hadAgent && Agent != null)
            {
                // 若仍在 NavMesh 上，直接 Warp 到 Transform 位置（更安全的对齐方式）
                if (Agent.isOnNavMesh)
                {
                    Agent.Warp(transform.position);
                }

                // 恢复自动更新配置
                Agent.updatePosition = _agentFlags.origUpdatePosition;
                Agent.updateRotation = _agentFlags.origUpdateRotation;
                Agent.autoBraking = _agentFlags.origAutoBraking;

                if (restoreToSnapshot && _agentHistory != null)
                {
                    // 回溯结束：恢复到快照值
                    Agent.speed = _lastAppliedAgentSnap.Speed;
                    Agent.angularSpeed = _lastAppliedAgentSnap.AngularSpeed;
                    Agent.acceleration = _lastAppliedAgentSnap.Acceleration;

                    if (Agent.isOnNavMesh)
                    {
                        TrySetAgentDestination(Agent, _lastAppliedAgentSnap.Destination);
                    }

                    SafeSetStopped(Agent, _lastAppliedAgentSnap.IsStopped);
                }
                else
                {
                    // 暂停结束：恢复到原始值
                    SafeSetStopped(Agent, _agentFlags.origIsStopped);
                }
            }

            // 恢复 Animator 推进速度
            if (Anim != null)
            {
                Anim.speed = _animOriginalSpeed;
            }

            // 恢复 Enemy 行为逻辑：仅当敌人未死亡时才恢复
            if (_enemyHadComponent && TheEnemy != null && !TheEnemy.IsDead)
            {
                TheEnemy.enabled = _enemyWasEnabled;
            }
            // 如果 IsDead == true，则保持 Enemy.enabled = false，防止死亡敌人继续 Update
        }

        /// <summary>
        /// 在回溯期间应用一帧 NavMeshAgent 快照（只改配置与目标，不做寻路推进）
        /// </summary>
        private void ApplyAgentSnapshotDuringRewind(AgentSnapshot snap)
        {
            if (Agent == null) return;

            // 禁止自动位置/旋转更新，避免寻路覆盖回放的 Transform
            Agent.updatePosition = false;
            Agent.updateRotation = false;

            // 尝试停止 Agent（需在 NavMesh 上才可写 isStopped）
            SafeSetStopped(Agent, true);

            // 清零速度：仅当 Agent.isOnNavMesh 才可安全写入
            if (Agent.enabled && Agent.isOnNavMesh)
                Agent.velocity = Vector3.zero;

            // 回放移动学参数与制动
            Agent.speed = snap.Speed;
            Agent.angularSpeed = snap.AngularSpeed;
            Agent.acceleration = snap.Acceleration;
            Agent.autoBraking = snap.AutoBraking;

            // 回放目标点（可能瞬时不可达，内部做了 try/catch）
            TrySetAgentDestination(Agent, snap.Destination);

            // nextPosition 跟随 Transform，避免寻路系统产生位移
            if (Agent.isOnNavMesh)
            {
                Agent.nextPosition = transform.position;
            }
        }

        /// <summary>
        /// 安全读取 NavMeshAgent.destination（某些状态下读会抛异常）
        /// </summary>
        private static Vector3 SafeGetAgentDestination(NavMeshAgent agent)
        {
            try
            {
                return agent.destination;
            }
            catch
            {
                return agent.transform.position;
            }
        }

        /// <summary>
        /// 尝试设置 NavMeshAgent.destination（仅当 isOnNavMesh 时生效）
        /// </summary>
        private static void TrySetAgentDestination(NavMeshAgent agent, Vector3 dest)
        {
            try
            {
                if (agent.isOnNavMesh)
                {
                    agent.destination = dest;
                }
            }
            catch
            {
                /* 忽略瞬时不可达/未放置等异常 */
            }
        }

        /// <summary>
        /// 安全设置 isStopped：仅在 agent.enabled 且已放置在 NavMesh 上时写入，避免抛错
        /// </summary>
        private static void SafeSetStopped(NavMeshAgent agent, bool stopped)
        {
            try
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = stopped;
                }
            }
            catch
            {
                /* 忽略：Agent 未放置到 NavMesh 或未激活 */
            }
        }

        //================== Enemy 自身回溯 ==================

        /// <summary>
        /// 应用一帧 Enemy 快照（死亡标记/状态机/计时/平滑参数）
        /// </summary>
        private void ApplyEnemySnapshotDuringRewind(Enemy enemy, EnemySnapshot snap)
        {
            if (enemy == null) return;

            // 还原核心变量（直接访问 public 属性）
            enemy.IsDead = snap.IsDead;
            enemy.StateTimer = snap.StateTimer;
            enemy.CurrentPointIndex = snap.CurrentPointIndex;

            // 状态机状态还原：若与当前不一致则切换
            var curId = GetCurrentStateId(enemy);
            if (curId != snap.StateId)
            {
                var targetState = ResolveStateInstance(enemy, snap.StateId);
                if (targetState != null && enemy.stateMachine != null)
                {
                    enemy.stateMachine.ChangeState(targetState, enemy);
                }
            }

            // 平滑速度字段（直接访问 public 属性）
            enemy.CurrentSpeed = snap.CurrentSpeed;
            enemy.SpeedVelocity = snap.SpeedVelocity;
            enemy.SeePlayerTimer = snap.SeePlayerTimer;
        }

        /// <summary>
        /// 获取当前 Enemy 状态机的"枚举化"状态标识（直接访问 public 属性）
        /// </summary>
        private static EnemyStateId GetCurrentStateId(Enemy enemy)
        {
            if (enemy == null || enemy.stateMachine == null) return EnemyStateId.None;

            var cur = enemy.stateMachine.CurrentState;
            if (cur == null) return EnemyStateId.None;

            if (ReferenceEquals(cur, enemy.idleState)) return EnemyStateId.Idle;
            if (ReferenceEquals(cur, enemy.patrolState)) return EnemyStateId.Patrol;
            if (ReferenceEquals(cur, enemy.alertState)) return EnemyStateId.Alert;
            if (ReferenceEquals(cur, enemy.deathState)) return EnemyStateId.Death;

            return EnemyStateId.None;
        }

        /// <summary>
        /// 根据枚举解析出实际的状态实例（供回放切换用）
        /// </summary>
        private static IEnemyState ResolveStateInstance(Enemy enemy, EnemyStateId id)
        {
            switch (id)
            {
                case EnemyStateId.Idle: return enemy.idleState;
                case EnemyStateId.Patrol: return enemy.patrolState;
                case EnemyStateId.Alert: return enemy.alertState;
                case EnemyStateId.Death: return enemy.deathState;
                default: return null;
            }
        }

        private void OnDestroy()
        {
            // 手动清理 RingBuffer，避免引用类型内存泄漏
            _agentHistory?.Clear();
            _enemyHistory?.Clear();
            _animRecorder = null; // 解除引用，帮助 GC

            Debug.Log($"{gameObject.name} 的 EnemyTimeRewind 已清理历史缓冲");
        }

        #region Animator Recorder（集中封装，便于未来拆分）

        /// <summary>
        /// Animator 的历史录制与回放封装（已优化内存占用）：
        /// - 元数据分离：参数的 Hash 和 Type 仅在初始化时构建一次，所有快照共享
        /// - Union 压缩：参数值使用 FieldOffset 重叠存储，从 12 字节/参数降至 4 字节/参数
        /// - 总体优化：相比原方案节省约 80% 内存（120 KB → 24 KB，以 5 参数 × 1200 帧为例）
        /// </summary>
        private sealed class AnimatorRecorder
        {
            /// <summary>
            /// 【优化】快照仅存储动态数据（层状态 + 参数值），元数据在初始化时提取
            /// </summary>
            public sealed class Snapshot
            {
                public int[] LayerStateHashes; // 每层的动画状态哈希
                public float[] LayerNormalizedTimes; // 每层的归一化播放时间
                public ParamValue[] ParameterValues; // 参数值数组（仅存值，不存 Hash/Type）
            }

            /// <summary>
            /// 【优化】参数元数据结构（仅初始化时构建一次，所有快照共享）
            /// </summary>
            private struct ParamMetadata
            {
                public int Hash; // 参数哈希值（用于 Get/Set）
                public AnimatorControllerParameterType Type; // 参数类型（Float/Int/Bool）
            }

            /// <summary>
            /// 【Union 优化】
            /// 魔改C#的结构体使得功能类似C++的union联合体
            /// 参数值使用显式内存布局，三个字段共享同一内存地址。
            /// 由于每个参数只能是 Float/Int/Bool 之一，使用 union 可节省内存。
            /// </summary>
            /// <remarks>
            /// 原理是将每个存储位置的偏移值强制设为0，使得它们重叠存储。
            /// 内存布局对比：
            /// - 原方案（独立存储）：float(4) + int(4) + bool(4对齐) = 12 字节
            /// - Union方案（重叠存储）：max(float, int, bool) = 4 字节
            /// - 节省：67% 内存/参数
            /// 
            /// 类型安全保证：
            /// - 通过 ParamMetadata.Type 确保读写字段一致（录制和回放使用相同类型）
            /// - 编译器会自动处理内存对齐，无需手动计算偏移量
            /// </remarks>
            [System.Runtime.InteropServices.StructLayout(
                System.Runtime.InteropServices.LayoutKind.Explicit)]
            public struct ParamValue
            {
                [System.Runtime.InteropServices.FieldOffset(0)]
                public float F; // Float 类型参数值（与 I/B 共享内存地址 0）

                [System.Runtime.InteropServices.FieldOffset(0)]
                public int I; // Int 类型参数值（与 F/B 共享内存地址 0）

                [System.Runtime.InteropServices.FieldOffset(0)]
                public bool B; // Bool 类型参数值（与 F/I 共享内存地址 0）
            }

            private readonly Animator _anim;
            private readonly RingBuffer<Snapshot> _history;

            // 【核心优化】参数元数据仅构建一次，所有快照共享（避免每帧重复分配）
            private readonly ParamMetadata[] _paramMetadata;

            public int Count => _history?.Count ?? 0;

            /// <summary>
            /// 构造函数：初始化 Recorder 并提取参数元数据
            /// </summary>
            public AnimatorRecorder(Animator anim, RingBuffer<Snapshot> buffer)
            {
                _anim = anim;
                _history = buffer;

                // 【一次性构建】提取所有非 Trigger 参数的元数据（Hash + Type）
                _paramMetadata = BuildParamMetadata(anim);
            }

            /// <summary>
            /// 【一次性构建】提取所有非 Trigger 参数的元数据（仅在构造时调用）
            /// </summary>
            /// <remarks>
            /// 元数据包含动画机参数的名字hash和对应的数据类型
            /// 忽略 Trigger 类型的原因：
            /// - Trigger 在设置后会自动重置，录制和回放会导致重复触发
            /// - 仅录制持久性参数（Float/Int/Bool）可确保回放一致性
            /// </remarks>
            private static ParamMetadata[] BuildParamMetadata(Animator anim)
            {
                if (anim == null) return Array.Empty<ParamMetadata>();

                var parameters = anim.parameters;
                var list = new List<ParamMetadata>(parameters.Length);

                foreach (var p in parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger) continue;
                    list.Add(new ParamMetadata
                    {
                        Hash = p.nameHash,
                        Type = p.type
                    });
                }

                return list.ToArray();
            }

            /// <summary>
            /// 录制当前帧的 Animator 状态快照。
            /// 用途：捕获所有层的状态哈希、归一化时间以及所有非 Trigger 参数的值，存入历史缓冲区。
            /// </summary>
            /// <remarks>
            /// 录制内容：
            /// 1. 每层的当前状态哈希（fullPathHash）
            /// 2. 每层的归一化播放时间（normalizedTime）
            /// 3. 所有参数的值（Float/Int/Bool，忽略 Trigger）
            /// 
            /// 优化点：
            /// - 参数元数据仅在构造时提取一次（避免每帧遍历 anim.parameters）
            /// - 参数值使用 Union 压缩存储（节省 67% 内存）
            /// </remarks>
            public void RecordOneSnap()
            {
                if (_anim == null) return;

                int layerCount = _anim.layerCount;
                var snap = new Snapshot
                {
                    LayerStateHashes = new int[layerCount],
                    LayerNormalizedTimes = new float[layerCount],
                    ParameterValues = CaptureParameterValues(_anim, _paramMetadata) // 【优化】复用元数据
                };

                // 捕获每层的当前状态与归一化时间
                for (int layer = 0; layer < layerCount; layer++)
                {
                    var st = _anim.GetCurrentAnimatorStateInfo(layer);
                    snap.LayerStateHashes[layer] = st.fullPathHash;
                    snap.LayerNormalizedTimes[layer] = st.normalizedTime;
                }

                _history.Push(snap);
            }

            /// <summary>
            /// 回放一帧 Animator 快照。
            /// 用途：从历史缓冲区弹出最新快照，恢复所有层的状态和参数，实现精确的动画回溯。
            /// </summary>
            /// <remarks>
            /// 回放步骤：
            /// 1. 从缓冲区弹出最新快照（PopBack）
            /// 2. 使用 Play() 切换每层到目标状态的指定归一化时间
            /// 3. 恢复所有参数值（Float/Int/Bool）
            /// 4. 调用 Update(0) 强制立即评估，确保即使 Animator.speed=0 也能即时生效
            /// </remarks>
            public void RewindOneSnap()
            {
                if (_anim == null || _history.Count == 0) return;

                var snap = _history.PopBack();

                // 回放每层动画状态到指定 normalizedTime
                var layerLen = Mathf.Min(_anim.layerCount, snap.LayerStateHashes.Length);
                for (int layer = 0; layer < layerLen; layer++)
                {
                    _anim.Play(snap.LayerStateHashes[layer], layer, snap.LayerNormalizedTimes[layer]);
                }

                // 【优化】使用元数据 + 值数组恢复参数
                ApplyParameters(_anim, _paramMetadata, snap.ParameterValues);

                // 立即评估一帧，确保切换立刻生效（即便 animator.speed=0）
                _anim.Update(0f);
            }

            /// <summary>
            /// 【优化】基于预构建的元数据捕获参数值（避免每帧重新遍历 anim.parameters）
            /// </summary>
            /// <remarks>
            /// 优化原理：
            /// - 元数据（Hash/Type）在构造时提取一次，录制时仅读取值
            /// - 避免每帧分配 List 和遍历 Trigger 类型参数
            /// - 使用 Union 存储，三个字段共享内存（F/I/B 仅一个有效）
            /// </remarks>
            private static ParamValue[] CaptureParameterValues(Animator anim, ParamMetadata[] metadata)
            {
                if (metadata == null || metadata.Length == 0)
                    return Array.Empty<ParamValue>();

                var values = new ParamValue[metadata.Length];

                for (int i = 0; i < metadata.Length; i++)
                {
                    var meta = metadata[i];

                    // 【Union 特性】根据类型写入对应字段，三者共享同一内存地址
                    switch (meta.Type)
                    {
                        case AnimatorControllerParameterType.Float:
                            values[i].F = anim.GetFloat(meta.Hash);
                            break;
                        case AnimatorControllerParameterType.Int:
                            values[i].I = anim.GetInteger(meta.Hash);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            values[i].B = anim.GetBool(meta.Hash);
                            break;
                    }
                }

                return values;
            }

            /// <summary>
            /// 【优化】基于元数据 + 值数组恢复参数（确保类型安全）
            /// </summary>
            /// <remarks>
            /// 类型安全保证：
            /// - 录制时根据 meta.Type 写入对应字段（F/I/B）
            /// - 回放时根据相同的 meta.Type 读取对应字段
            /// - Union 虽然允许访问任意字段，但通过元数据控制确保读写一致性
            /// </remarks>
            private static void ApplyParameters(Animator anim, ParamMetadata[] metadata, ParamValue[] values)
            {
                if (metadata == null || values == null) return;

                int count = Mathf.Min(metadata.Length, values.Length);
                for (int i = 0; i < count; i++)
                {
                    var meta = metadata[i];
                    var val = values[i];

                    // 【Union 特性】根据类型读取对应字段（必须与录制时使用的字段一致）
                    switch (meta.Type)
                    {
                        case AnimatorControllerParameterType.Float:
                            anim.SetFloat(meta.Hash, val.F);
                            break;
                        case AnimatorControllerParameterType.Int:
                            anim.SetInteger(meta.Hash, val.I);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            anim.SetBool(meta.Hash, val.B);
                            break;
                    }
                }
            }
        }

        #endregion
    }
}