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
        private bool oriIsKinematic;
        private VelocityValuesSnapshot pauseSnapshot;


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
            oriIsKinematic = rb.isKinematic;
            rb.isKinematic = true; // 刚体设为运动学，不受物理引擎影响
        }

        protected override void OnStopRewind()
        {
            base.OnStopRewind();
            rb.isKinematic = oriIsKinematic;    // 恢复原始运动学状态

        }

        /// <summary>
        /// 暂停开始：冻结刚体物理行为（复用回溯的冻结逻辑）
        /// </summary>
        protected override void OnStartPause()
        {
            base.OnStartPause();
            pauseSnapshot.Velocity = rb.linearVelocity;
            pauseSnapshot.AngularVelocity = rb.angularVelocity;
            oriIsKinematic = rb.isKinematic;
            rb.isKinematic = true; // 刚体设为运动学，不受物理引擎影响
        }

        /// <summary>
        /// 暂停结束：恢复刚体物理行为
        /// </summary>
        protected override void OnStopPause()
        {
            base.OnStopPause();
            rb.isKinematic = oriIsKinematic;    // 恢复原始运动学状态
            rb.linearVelocity = pauseSnapshot.Velocity;
            rb.angularVelocity = pauseSnapshot.AngularVelocity;
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
            // 取出最新快照并应用，然后 pop
            var snap = rigidBodyHistory.PopBack();
            rb.linearVelocity = snap.Velocity;
            rb.angularVelocity = snap.AngularVelocity;

        }
    }
}
