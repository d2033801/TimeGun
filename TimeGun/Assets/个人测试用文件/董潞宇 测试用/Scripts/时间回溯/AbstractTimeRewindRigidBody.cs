using Unity.VisualScripting;
using UnityEngine;
using Utility;
namespace TimeGun
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
        /// 记录刚体的线速度与角速度的结构体
        /// </summary>
        public struct VelocityValuesSnapshot
        {
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            
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

        protected override void RewindOneStep()
        {
            base.RewindOneStep();
            // 取出最新快照并应用，然后 pop
            var snap = rigidBodyHistory.PopBack();
            rb.linearVelocity = snap.Velocity;
            rb.angularVelocity = snap.AngularVelocity;

        }
    }
}
