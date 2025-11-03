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
        [SerializeField, Tooltip("敌人身上的NavMeshAgent")] private NavMeshAgent agent;
        [SerializeField, Tooltip("敌人身上的Animator组件")] private Animator anim;
        [SerializeField, Tooltip("敌人的Enemy控制脚本")] private Enemy enemy;

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

        //==================== 回溯期间的运行态标记 ====================
        private struct AgentRuntimeFlags
        {
            public bool hadAgent;           // 回溯启动时是否存在 Agent
            public bool origUpdatePosition; // 原始 updatePosition
            public bool origUpdateRotation; // 原始 updateRotation
            public bool origIsStopped;      // 原始 isStopped
            public bool origAutoBraking;    // 原始 autoBraking
        }

        private AgentRuntimeFlags _agentFlags;
        private float _animOriginalSpeed = 1f;

        private bool _enemyHadComponent; // 是否存在 Enemy 组件
        private bool _enemyWasEnabled;   // 回溯前 Enemy.enabled

        /// <summary>
        /// NavMeshAgent 的运行快照（仅涉及可回放的"配置/目标值"，真实位置由 Transform 快照恢复）
        /// </summary>
        private struct AgentSnapshot
        {
            public bool IsStopped;
            public bool UpdatePosition;
            public bool UpdateRotation;
            public bool AutoBraking;
            public float Speed;
            public float AngularSpeed;
            public float Acceleration;
            public Vector3 Destination; // 目标点：不总是可达，设置时注意 try/catch
        }

        /// <summary>
        /// Enemy 的关键运行态快照（仅与逻辑/动画驱动相关，不包含 Transform）
        /// </summary>
        private struct EnemySnapshot
        {
            public bool IsDead;
            public float StateTimer;
            public int CurrentPointIndex;
            public EnemyStateId StateId;

            // 速度平滑字段（直接访问 public 属性）
            public float CurrentSpeed;
            public float SpeedVelocity;
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
                    UpdatePosition = Agent.updatePosition,
                    UpdateRotation = Agent.updateRotation,
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
                    CurrentSpeed = TheEnemy.CurrentSpeed,      // 直接访问 public 属性
                    SpeedVelocity = TheEnemy.SpeedVelocity    // 直接访问 public 属性
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
                Agent.velocity = Vector3.zero;           // 清零速度，避免残留速度影响
            }
        }

        /// <summary>
        /// 回溯开始：冻结 NavMesh 自动更新、冻结 Animator 推进、暂停 Enemy Update 驱动
        /// </summary>
        ///
        protected override void OnStartRewind()
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
        /// 回溯结束：恢复 NavMeshAgent 配置与 Animator 速度，恢复 Enemy Update
        /// </summary>
        public override void StopRewind()
        {
            base.StopRewind();

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

                // 若我们在回溯期间应用了快照，则恢复到快照值；否则恢复原始值
                if (_agentHistory != null)
                {
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
                    SafeSetStopped(Agent, _agentFlags.origIsStopped);
                }
            }

            // 恢复 Animator 推进速度
            if (Anim != null)
            {
                Anim.speed = _animOriginalSpeed;
            }

            // 恢复 Enemy 行为逻辑
            if (_enemyHadComponent && TheEnemy != null)
            {
                TheEnemy.enabled = _enemyWasEnabled;
            }
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
        }

        /// <summary>
        /// 获取当前 Enemy 状态机的"枚举化"状态标识（直接访问 public 属性）
        /// </summary>
        private static EnemyStateId GetCurrentStateId(Enemy enemy)
        {
            if (enemy == null || enemy.stateMachine == null) return EnemyStateId.None;

            // 直接访问 public 属性代替反射
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

        #region Animator Recorder（集中封装，便于未来拆分）

        /// <summary>
        /// Animator 的历史录制与回放封装：
        /// - Snapshot：每层 fullPathHash + normalizedTime + 所有非 Trigger 参数
        /// - RecordOneSnap：捕获层状态与参数
        /// - RewindOneSnap：Play 到目标状态并 Update(0) 强制评估，使得 animator.speed=0 也能即时生效
        /// </summary>
        private sealed class AnimatorRecorder
        {
            public sealed class Snapshot
            {
                public int[] LayerStateHashes;
                public float[] LayerNormalizedTimes;
                public Param[] Parameters;
            }

            public struct Param
            {
                public int Hash;
                public AnimatorControllerParameterType Type;
                public float F;
                public int I;
                public bool B;
            }

            private readonly Animator _anim;
            private readonly RingBuffer<Snapshot> _history;

            public int Count => _history?.Count ?? 0;

            public AnimatorRecorder(Animator anim, RingBuffer<Snapshot> buffer)
            {
                _anim = anim;
                _history = buffer;
            }

            public void RecordOneSnap()
            {
                if (_anim == null) return;

                int layerCount = _anim.layerCount;
                var snap = new Snapshot
                {
                    LayerStateHashes = new int[layerCount],
                    LayerNormalizedTimes = new float[layerCount],
                    Parameters = CaptureParameters(_anim)
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

                // 恢复 Animator 参数（Float/Int/Bool）
                ApplyParameters(_anim, snap.Parameters);

                // 立即评估一帧，确保切换立刻生效（即便 animator.speed=0）
                _anim.Update(0f);
            }

            private static Param[] CaptureParameters(Animator anim)
            {
                var parameters = anim.parameters;
                var list = new List<Param>(parameters.Length);
                foreach (var p in parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger) continue; // 忽略触发器，避免错误重复触发
                    var pr = new Param { Hash = p.nameHash, Type = p.type };
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            pr.F = anim.GetFloat(p.nameHash);
                            break;
                        case AnimatorControllerParameterType.Int:
                            pr.I = anim.GetInteger(p.nameHash);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            pr.B = anim.GetBool(p.nameHash);
                            break;
                    }

                    list.Add(pr);
                }

                return list.ToArray();
            }

            private static void ApplyParameters(Animator anim, Param[] ps)
            {
                if (ps == null) return;
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    switch (p.Type)
                    {
                        case AnimatorControllerParameterType.Float:
                            anim.SetFloat(p.Hash, p.F);
                            break;
                        case AnimatorControllerParameterType.Int:
                            anim.SetInteger(p.Hash, p.I);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            anim.SetBool(p.Hash, p.B);
                            break;
                    }
                }
            }
        }

        #endregion
    }
}