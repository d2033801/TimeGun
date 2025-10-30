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

        [Header("设置项 Configs")]
        [Tooltip("子弹发射速度"), SerializeField] private float bulletSpeed = 50f;
        [Tooltip("子弹发射间隔"), SerializeField] private float bulletFireInterval  = 0.5f;


        private float bulletIntervalTimer = 0f;

        private void Update()
        {
            bulletIntervalTimer += Time.deltaTime;
        }

        public override void Fire()
        {
            if (bulletIntervalTimer > bulletFireInterval)
            {
                bulletIntervalTimer = 0;
                GameObject grenade = Instantiate(bulletPrefab, muzzlePoint.position + transform.forward, Quaternion.identity);       // 在本地坐标系下前方生成榴弹
                Rigidbody rb = grenade.GetComponent<Rigidbody>();
                rb.AddForce(transform.forward * 50f, ForceMode.Impulse);
            }
            
        }

        /// <summary>
        /// 投掷榴弹 
        /// </summary>

        public void Throw()
        {

            GameObject grenade = Instantiate(grenadePrefab, grenadeLaunchPoint.position + transform.forward, Quaternion.identity);       // 在本地坐标系下前方生成榴弹
            Rigidbody rb = grenade.GetComponent<Rigidbody>();
            rb.AddForce(transform.forward * 10f + Vector3.up * 3f, ForceMode.Impulse);

        }


    }
}