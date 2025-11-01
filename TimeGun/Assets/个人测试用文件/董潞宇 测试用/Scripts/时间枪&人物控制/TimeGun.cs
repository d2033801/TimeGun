using System;
using UnityEngine;

namespace TimeGun
{
    public class TimeGun : AbstractWeaponBase, IThrowable
    {
        [Header("预制件 References")]
        [Tooltip("枪榴弹发射点"), SerializeField] private Transform grenadeLaunchPoint;
        [Tooltip("枪榴弹预制件"), SerializeField] private GameObject grenadePrefab;
        [Tooltip("子弹预制件"), SerializeField] private GameObject bulletPrefab;

        [Header("子弹设置项 Bullets Configs")]
        [Tooltip("子弹发射速度"), SerializeField] private float bulletSpeed = 100f;         // 此处子弹质量为0.01
        [Tooltip("子弹发射间隔"), SerializeField] private float bulletFireInterval = 0.5f;
        [Header("枪榴弹设置项 Grenade Configs")]
        [Tooltip("枪榴弹发射速度"), SerializeField] private float grenadeSpeed = 30f;
        [Tooltip("枪榴弹发射间隔"), SerializeField] private float grenadeFireInterval = 1f;

        private float bulletIntervalTimer = 0f;             // 计时器，记录子弹发射间隔时间
        private float grenadeIntervalTimer = 0f;             // 计时器，记录榴弹发射间隔时间

        private void Update()
        {
            float dt= Time.deltaTime;
            bulletIntervalTimer += dt;
            grenadeIntervalTimer += dt;
        }

        /// <summary>
        /// 开火
        /// </summary>
        /// <param name="targetPoint">目标点</param>
        public override void Fire(Vector3 targetPoint)
        {
            if (!TryConsume(ref bulletIntervalTimer, bulletFireInterval)) return;
            var dir = (targetPoint - muzzlePoint.position).normalized;
            Rigidbody rb = InstantiateAmmoRigidbody(bulletPrefab, muzzlePoint.position, Quaternion.LookRotation(dir,Vector3.up));
            
            rb.AddForce(dir * bulletSpeed, ForceMode.VelocityChange);

        }
        /// <summary>
        /// 开火, 对准枪正前方
        /// </summary>
        public override void Fire()
        {
            Fire(muzzlePoint.forward);
        }



        /// <summary>
        /// 投掷榴弹, 向着发射口正前方 
        /// </summary>
        public void Throw()
        {
            Throw(grenadeLaunchPoint.forward);
        }

        /// <summary>
        /// 投掷榴弹, 朝向目标点
        /// </summary>
        /// <param name="targetPoint">瞄准的目标位置</param>

        public void Throw(Vector3 targetPoint)
        {

            if(!TryConsume(ref grenadeIntervalTimer, grenadeFireInterval)) return;
            
            Rigidbody rb = InstantiateAmmoRigidbody(grenadePrefab, grenadeLaunchPoint.position ,
                Quaternion.identity);
            var dir = (targetPoint - grenadeLaunchPoint.position).normalized;
            rb.AddForce(dir * grenadeSpeed + Vector3.up * 2f, ForceMode.VelocityChange);
            //rb.linearVelocity = dir * 80f + Vector3.up * 5f;

        }
    }
}