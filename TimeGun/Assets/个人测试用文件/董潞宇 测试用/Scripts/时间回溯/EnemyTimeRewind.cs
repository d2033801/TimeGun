using System;
using System.Collections.Generic;
using System.Reflection;
using TimeRewind;
using UnityEngine;
using UnityEngine.AI;
using Utility;

namespace TimeRewind
{

    /// <summary>
    /// 敌人回溯组件：
    /// - 继承 AbstractTimeRewindObject（Transform 已由基类录制）
    /// - 额外录制 NavMeshAgent、Animator、Enemy 自身关键变量（状态机/计时/索引/死亡等）
    /// </summary>
    public class EnemyTimeRewind : AbstractTimeRewindObject
    {
        // 组件缓存
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private Animator _anim;
        [SerializeField] private Enemy _enemy;

        private NavMeshAgent Agent => _agent ??= GetComponent<NavMeshAgent>();
        private Animator Anim => _anim ??= GetComponent<Animator>();
        private Enemy TheEnemy => _enemy ??= GetComponent<Enemy>();

        //=========== NavMeshAgent 历史 ===========
        private RingBuffer<AgentSnapshot> _agentHistory;
        private AgentSnapshot _lastAppliedAgentSnap;

        //=========== Animator 历史（集中封装） ===========
        private AnimatorRecorder _animRecorder;

        //=========== Enemy 自身历史 ===========
        private RingBuffer<EnemySnapshot> _enemyHistory;
        private EnemySnapshot _lastAppliedEnemySnap;

        // 反射缓存（读取 EnemyStateMachine._currentState、_currentSpeed、_speedVelocity）
        private static FieldInfo s_fsmCurrentStateField;
        private static FieldInfo s_enemyCurrentSpeedField;
        private static FieldInfo s_enemySpeedVelocityField;

        // 回溯时用于临时保存/恢复运行态
        private struct AgentRuntimeFlags
        {
            public bool hadAgent;
            public bool origUpdatePosition;
            public bool origUpdateRotation;
            public bool origIsStopped;
            public bool origAutoBraking;
        }

        private AgentRuntimeFlags _agentFlags;
        private float _animOriginalSpeed = 1f;

        private bool _enemyHadComponent;
        private bool _enemyWasEnabled;

        /// <summary>记录 NavMeshAgent 的运行快照</summary>
        private struct AgentSnapshot
        {
            public bool IsStopped;
            public bool UpdatePosition;
            public bool UpdateRotation;
            public bool AutoBraking;
            public float Speed;
            public float AngularSpeed;
            public float Acceleration;
            public Vector3 Destination;
        }

        /// <summary>Enemy 的关键运行态快照</summary>
        private struct EnemySnapshot
        {
            public bool IsDead;
            public float StateTimer;
            public int CurrentPointIndex;
            public EnemyStateId StateId;

            // 可选：平滑速度参数（通过反射）
            public float CurrentSpeed;
            public float SpeedVelocity;
            public bool HasSmoothFields;
        }

        private enum EnemyStateId : byte
        {
            None = 0,
            Idle = 1,
            Patrol = 2,
            Alert = 3,
            Death = 4
        }

        protected override void MainInit()
        {
            base.MainInit();

            _agentHistory = RewindInit<AgentSnapshot>(out _);
            _enemyHistory = RewindInit<EnemySnapshot>(out _);

            // Animator 单独封装，使用同一容量
            var animBuffer = RewindInit<AnimatorRecorder.Snapshot>(out _);
            _animRecorder = new AnimatorRecorder(Anim, animBuffer);

            // 反射字段缓存（延迟获取亦可，这里提前一次）
            CacheReflectionFields();
        }

        protected override void RecordOneSnap()
        {
            base.RecordOneSnap();

            // 录制 NavMeshAgent
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
                    Destination = SafeGetAgentDestination(Agent)
                };
                _agentHistory.Push(snap);
            }

            // 录制 Animator
            _animRecorder?.RecordOneSnap();

            // 录制 Enemy 自身
            if (TheEnemy != null)
            {
                var es = new EnemySnapshot
                {
                    IsDead = TheEnemy.isDead,
                    StateTimer = TheEnemy.stateTimer,
                    CurrentPointIndex = TheEnemy.currentPointIndex,
                    StateId = GetCurrentStateId(TheEnemy),
                    HasSmoothFields = TryReadEnemySmooth(TheEnemy, out var curSpd, out var spdVel)
                };
                if (es.HasSmoothFields)
                {
                    es.CurrentSpeed = curSpd;
                    es.SpeedVelocity = spdVel;
                }

                _enemyHistory.Push(es);
            }
        }

        protected override void RewindOneSnap()
        {
            // 先还原 Transform
            base.RewindOneSnap();

            // 应用 NavMeshAgent
            if (Agent != null && _agentHistory != null && _agentHistory.Count > 0)
            {
                var snap = _agentHistory.PopBack();
                ApplyAgentSnapshotDuringRewind(snap);
                _lastAppliedAgentSnap = snap;
            }

            // 应用 Animator
            _animRecorder?.RewindOneSnap();

            // 应用 Enemy 自身
            if (TheEnemy != null && _enemyHistory != null && _enemyHistory.Count > 0)
            {
                var snap = _enemyHistory.PopBack();
                ApplyEnemySnapshotDuringRewind(TheEnemy, snap);
                _lastAppliedEnemySnap = snap;
            }
        }

        protected override void OnAppliedSnapshotDuringRewind()
        {
            base.OnAppliedSnapshotDuringRewind();

            // NavMesh 对齐
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh && !Agent.updatePosition)
            {
                Agent.nextPosition = transform.position;
                Agent.velocity = Vector3.zero;
            }
        }

        public override void StartRewind(float speed)
        {
            base.StartRewind(speed);

            // 暂停 NavMesh 自动行为
            if (Agent != null)
            {
                _agentFlags.hadAgent = true;
                _agentFlags.origIsStopped = Agent.isStopped;
                _agentFlags.origUpdatePosition = Agent.updatePosition;
                _agentFlags.origUpdateRotation = Agent.updateRotation;
                _agentFlags.origAutoBraking = Agent.autoBraking;

                SafeSetStopped(Agent, true);
                Agent.updatePosition = false;
                Agent.updateRotation = false;
            }

            // 暂停 Animator 推进
            if (Anim != null)
            {
                _animOriginalSpeed = Anim.speed;
                Anim.speed = 0f;
            }

            // 暂停 Enemy Update（阻止状态机继续运行）
            if (TheEnemy != null)
            {
                _enemyHadComponent = true;
                _enemyWasEnabled = TheEnemy.enabled;
                TheEnemy.enabled = false;
            }
        }

        public override void StopRewind()
        {
            base.StopRewind();

            // 对齐并恢复 NavMeshAgent
            if (_agentFlags.hadAgent && Agent != null)
            {
                if (Agent.isOnNavMesh)
                {
                    Agent.Warp(transform.position);
                }

                Agent.updatePosition = _agentFlags.origUpdatePosition;
                Agent.updateRotation = _agentFlags.origUpdateRotation;
                Agent.autoBraking = _agentFlags.origAutoBraking;

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

            // 恢复 Animator 速度
            if (Anim != null)
            {
                Anim.speed = _animOriginalSpeed;
            }

            // 恢复 Enemy Update
            if (_enemyHadComponent && TheEnemy != null)
            {
                TheEnemy.enabled = _enemyWasEnabled;
            }
        }

        /// <summary>
        /// 瞬时回退多帧，确保每步后都进行对齐。
        /// </summary>
        public override void RewindBySeconds(float seconds)
        {
            if (seconds <= 0f || frameCount == 0) return;

            int frames = Mathf.RoundToInt(seconds / Mathf.Max(1e-6f, GetRecordIntervalSafe()));
            int agentCount = _agentHistory != null ? _agentHistory.Count : frameCount;
            int animCount = _animRecorder != null ? _animRecorder.Count : frameCount;
            int enemyCount = _enemyHistory != null ? _enemyHistory.Count : frameCount;

            frames = Mathf.Clamp(frames, 0, Mathf.Min(frameCount, agentCount, animCount, enemyCount));

            for (int i = 0; i < frames; i++)
            {
                RewindOneSnap();
                OnAppliedSnapshotDuringRewind();
            }
        }

        private float GetRecordIntervalSafe()
        {
            int fps = Mathf.Max(1, recordFPS);
            return 1f / fps;
        }

        private void ApplyAgentSnapshotDuringRewind(AgentSnapshot snap)
        {
            if (Agent == null) return;

            Agent.updatePosition = false;
            Agent.updateRotation = false;
            SafeSetStopped(Agent, true);
            if (Agent.enabled && Agent.isOnNavMesh)
                Agent.velocity = Vector3.zero;

            Agent.speed = snap.Speed;
            Agent.angularSpeed = snap.AngularSpeed;
            Agent.acceleration = snap.Acceleration;
            Agent.autoBraking = snap.AutoBraking;

            TrySetAgentDestination(Agent, snap.Destination);

            if (Agent.isOnNavMesh)
            {
                Agent.nextPosition = transform.position;
            }
        }

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
                /* 忽略瞬时不可达 */
            }
        }

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
        private void ApplyEnemySnapshotDuringRewind(Enemy enemy, EnemySnapshot snap)
        {
            if (enemy == null) return;

            enemy.isDead = snap.IsDead;
            enemy.stateTimer = snap.StateTimer;
            enemy.currentPointIndex = snap.CurrentPointIndex;

            // 状态机状态还原（反射获取当前，若不一致再切换）
            var curId = GetCurrentStateId(enemy);
            if (curId != snap.StateId)
            {
                var targetState = ResolveStateInstance(enemy, snap.StateId);
                if (targetState != null && enemy.stateMachine != null)
                {
                    enemy.stateMachine.ChangeState(targetState, enemy);
                }
            }

            // 平滑速度字段（若存在）还原
            if (snap.HasSmoothFields)
            {
                TryWriteEnemySmooth(enemy, snap.CurrentSpeed, snap.SpeedVelocity);
            }
        }

        private static EnemyStateId GetCurrentStateId(Enemy enemy)
        {
            if (enemy == null || enemy.stateMachine == null) return EnemyStateId.None;
            try
            {
                var cur = (IEnemyState)s_fsmCurrentStateField?.GetValue(enemy.stateMachine);
                if (cur == null) return EnemyStateId.None;

                if (ReferenceEquals(cur, enemy.idleState)) return EnemyStateId.Idle;
                if (ReferenceEquals(cur, enemy.patrolState)) return EnemyStateId.Patrol;
                if (ReferenceEquals(cur, enemy.alertState)) return EnemyStateId.Alert;
                if (ReferenceEquals(cur, enemy.deathState)) return EnemyStateId.Death;
            }
            catch
            {
                /* ignore */
            }

            return EnemyStateId.None;
        }

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

        private static bool TryReadEnemySmooth(Enemy enemy, out float currentSpeed, out float speedVelocity)
        {
            currentSpeed = 0f;
            speedVelocity = 0f;
            try
            {
                if (s_enemyCurrentSpeedField != null)
                    currentSpeed = (float)s_enemyCurrentSpeedField.GetValue(enemy);
                if (s_enemySpeedVelocityField != null)
                    speedVelocity = (float)s_enemySpeedVelocityField.GetValue(enemy);
                return s_enemyCurrentSpeedField != null || s_enemySpeedVelocityField != null;
            }
            catch
            {
                return false;
            }
        }

        private static void TryWriteEnemySmooth(Enemy enemy, float currentSpeed, float speedVelocity)
        {
            try
            {
                if (s_enemyCurrentSpeedField != null)
                    s_enemyCurrentSpeedField.SetValue(enemy, currentSpeed);
                if (s_enemySpeedVelocityField != null)
                    s_enemySpeedVelocityField.SetValue(enemy, speedVelocity);
            }
            catch
            {
                /* ignore */
            }
        }

        private static void CacheReflectionFields()
        {
            if (s_fsmCurrentStateField == null)
                s_fsmCurrentStateField =
                    typeof(EnemyStateMachine).GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);

            if (s_enemyCurrentSpeedField == null)
                s_enemyCurrentSpeedField =
                    typeof(Enemy).GetField("_currentSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            if (s_enemySpeedVelocityField == null)
                s_enemySpeedVelocityField =
                    typeof(Enemy).GetField("_speedVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        #region Animator Recorder（集中封装，便于未来拆分）

        /// <summary>
        /// Animator 的历史录制与回放，集中在一个位置，后续可独立成组件。
        /// 录制：
        /// - 每层：fullPathHash + normalizedTime
        /// - 参数：Float/Int/Bool（忽略 Trigger）
        /// 回放：
        /// - 按层 Play(hash, layer, normalizedTime)，紧跟一次 Update(0) 以立即生效
        /// - 还原参数
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

                // 按层回放状态
                var layerLen = Mathf.Min(_anim.layerCount, snap.LayerStateHashes.Length);
                for (int layer = 0; layer < layerLen; layer++)
                {
                    _anim.Play(snap.LayerStateHashes[layer], layer, snap.LayerNormalizedTimes[layer]);
                }

                // 恢复参数
                ApplyParameters(_anim, snap.Parameters);

                // 立即评估一帧以生效（在回溯期间 animator.speed=0）
                _anim.Update(0f);
            }

            private static Param[] CaptureParameters(Animator anim)
            {
                var parameters = anim.parameters;
                var list = new List<Param>(parameters.Length);
                foreach (var p in parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger) continue; // 忽略触发器
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
