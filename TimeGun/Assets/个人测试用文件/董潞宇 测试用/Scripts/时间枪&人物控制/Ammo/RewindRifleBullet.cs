using System;
using TimeGun;
using UnityEngine;

namespace TimeGun
{
    public class RewindRifleBullet : AmmoRewindAbstract
    {
        [Header("特效")]
        [SerializeField, Tooltip("击中特效")] private GameObject impactEffectPrefab;        // TODO: 未来可以改成对象池



        protected override void HandleHit(Collision hitCollision)
        {
            // 播放命中特效
            if (impactEffectPrefab != null)
            {
                // 在命中点创建特效（使用第一个碰撞点）
                ContactPoint contact = hitCollision.GetContact(0);
                Quaternion rot = Quaternion.LookRotation(contact.normal);
                GameObject fx = Instantiate(impactEffectPrefab, contact.point, rot);

                // 设置特效在播放完后自动销毁
                var main = fx.GetComponent<ParticleSystem>().main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
            TryTriggerRewind(hitCollision.collider, rewindSecondsOnHit);

            Destroy(gameObject);

            // 若启用对象池，请使用这个
            // Despawn();
        }

    }

}
