using Unity.VisualScripting;
using UnityEngine;
using Utility;

namespace TimeRewind
{
    /// <summary>
    /// 作为所有刚体时停物体的基抽象类, 提供刚体时停功能的基本框架。继承自 <see cref="AbstractTimeRewindObject"/> 。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AbstractTimeRewindRigidBody : AbstractTimeRewindObject
    {
        private Rigidbody _rb;
        private Rigidbody rb => _rb ??= GetComponent<Rigidbody>();
        private RingBuffer<VelocityValuesSnapshot> rigidBodyHistory;

        /// <summary>
        /// 最后一次回溯应用的速度快照（用于回溯结束时恢复）
        /// </summary>
        private VelocityValuesSnapshot _lastRewindedVelocity;

        //==================== 冻结管理器（使用基类提供的通用版本）====================
        /// <summary>
        /// 刚体冻结状态
        /// </summary>
        private struct RigidbodyFreezeState
        {
            public bool origIsKinematic;      // 原始 isKinematic 状态
            public Vector3 origVelocity;      // 原始线速度
            public Vector3 origAngularVelocity;  // 原始角速度
        }

        private ComponentFreezeManager<RigidbodyFreezeState> _freezeManager
            = new ComponentFreezeManager<RigidbodyFreezeState>();

        /// <summary>
        /// 记录刚体的线速度与角速度的结构体
        /// </summary>
        public struct VelocityValuesSnapshot
        {
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
        }

        protected override void OnStartRewind()
        {
            base.OnStartRewind();
            RequestFreezeRigidbody();
        }

        protected override void OnStopRewind()
        {
            base.OnStopRewind();
            ReleaseFreezeRigidbody(true);  // ✅ 修改：回溯结束时恢复速度（使用最后回溯的快照）
        }

        protected override void OnStartPause()
        {
            base.OnStartPause();
            RequestFreezeRigidbody();
        }

        protected override void OnStopPause()
        {
            base.OnStopPause();
            ReleaseFreezeRigidbody(false);  // ✅ 暂停结束不恢复速度（使用冻结前的原始速度）
        }

        /// <summary>
        /// 请求冻结刚体（引用计数+1）
        /// </summary>
        private void RequestFreezeRigidbody()
        {
            var currentState = new RigidbodyFreezeState
            {
                origIsKinematic = rb.isKinematic,
                origVelocity = rb.linearVelocity,
                origAngularVelocity = rb.angularVelocity
            };

            bool shouldFreeze = _freezeManager.RequestFreeze(currentState);

            if (!shouldFreeze) return;  // 已经冻结，直接返回

            // 执行冻结
            rb.isKinematic = true;
        }

        /// <summary>
        /// 释放冻结刚体（引用计数-1）
        /// </summary>
        /// <param name="restoreVelocity">
        /// true = 恢复速度（回溯结束时使用最后回溯的快照速度）
        /// false = 不恢复速度（暂停结束时使用冻结前的原始速度）
        /// </param>
        private void ReleaseFreezeRigidbody(bool restoreVelocity)
        {
            if (!_freezeManager.ReleaseFreeze(out var savedState))
            {
                return;  // 还有其他系统需要冻结
            }

            // 执行解冻
            rb.isKinematic = savedState.origIsKinematic;

            // ✅ 修复：根据场景恢复速度
            if (restoreVelocity)
            {
                // 回溯结束：应用最后一次回溯的快照速度
                rb.linearVelocity = _lastRewindedVelocity.Velocity;
                rb.angularVelocity = _lastRewindedVelocity.AngularVelocity;
            }
            else
            {
                // 暂停结束：恢复到冻结前的原始速度
                rb.linearVelocity = savedState.origVelocity;
                rb.angularVelocity = savedState.origAngularVelocity;
            }
        }

        protected override void MainInit()
        {
            base.MainInit();
            rigidBodyHistory = RewindInit<VelocityValuesSnapshot>(out _);
        }

        protected override void RecordOneSnap()
        {
            base.RecordOneSnap();
            var snap = new VelocityValuesSnapshot
            {
                Velocity = rb.linearVelocity,
                AngularVelocity = rb.angularVelocity
            };
            rigidBodyHistory.Push(snap);
        }

        protected override void RewindOneSnap()
        {
            base.RewindOneSnap();

            // ✅ 优化：只保存快照，不应用速度（因为 isKinematic=true 时速度无效）
            _lastRewindedVelocity = rigidBodyHistory.PopBack();

            // ❌ 删除：不再每帧设置速度（浪费性能）
            // rb.linearVelocity = snap.Velocity;
            // rb.angularVelocity = snap.AngularVelocity;
        }
    }
}