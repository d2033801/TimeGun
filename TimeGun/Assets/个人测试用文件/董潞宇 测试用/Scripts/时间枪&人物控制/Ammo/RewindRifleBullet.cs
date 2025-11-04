using System;
using TimeGun;
using UnityEngine;

namespace TimeGun
{
    public class RewindRifleBullet : AmmoRewindAbstract
    {
        [Header("特效 Effects")]
        [SerializeField, Tooltip("击中特效")] private GameObject impactEffectPrefab;

        protected override void HandleHit(Collision hitCollision)
        {
            // 获取击中点信息
            ContactPoint contact = hitCollision.GetContact(0);
            Vector3 hitPoint = contact.point;
            Vector3 hitNormal = contact.normal;

            // ✅ 播放击中音效（使用基类提供的方法）
            PlayHitSound(hitCollision);

            // 播放命中特效
            if (impactEffectPrefab != null)
            {
                Quaternion rot = Quaternion.LookRotation(hitNormal);
                GameObject fx = Instantiate(impactEffectPrefab, hitPoint, rot);

                // 设置特效在播放完后自动销毁
                var main = fx.GetComponent<ParticleSystem>().main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }

            TryTriggerRewind(hitCollision.collider, rewindSecondsOnHit);

            Destroy(gameObject);
        }
    }
}
