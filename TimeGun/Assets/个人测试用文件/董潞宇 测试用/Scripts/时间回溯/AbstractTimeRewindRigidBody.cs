using UnityEngine;
using Utility;
namespace TimeGun
{

    public class AbstractTimeRewindRigidBody : AbstractTimeRewindObject
    {

        private RingBuffer<VelocityValuesSnapshot> rigidBodyHistory;
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

        
    }
}
