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

        [Header("特效 Effects")]
        [Tooltip("枪口火光特效预制件"), SerializeField] private GameObject muzzleFlashPrefab;
        [Tooltip("榴弹发射特效预制件"), SerializeField] private GameObject grenadeLaunchEffectPrefab;
        [Tooltip("特效持续时间(秒)"), SerializeField] private float effectDuration = 0.5f;

        [Header("音效 Audio")]
        [Tooltip("子弹发射音效"), SerializeField] private AudioClip bulletFireSound;
        [Tooltip("榴弹发射音效"), SerializeField] private AudioClip grenadeFireSound;
        [Tooltip("音效音量(0-1)"), SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
        [Tooltip("音频源组件(如果为空则自动创建)"), SerializeField] private AudioSource audioSource;

        [Header("子弹设置项 Bullets Configs")]
        [Tooltip("子弹发射速度"), SerializeField] private float bulletSpeed = 100f;         // 此处子弹质量为0.01
        [Tooltip("子弹发射间隔"), SerializeField] private float bulletFireInterval = 0.5f;
        
        [Header("枪榴弹设置项 Grenade Configs")]
        [Tooltip("枪榴弹发射速度"), SerializeField] private float grenadeSpeed = 30f;
        [Tooltip("枪榴弹发射间隔"), SerializeField] private float grenadeFireInterval = 1f;

        private float bulletIntervalTimer = 0f;             // 计时器，记录子弹发射间隔时间
        private float grenadeIntervalTimer = 0f;             // 计时器，记录榴弹发射间隔时间

        private void Awake()
        {
            // 确保有音频源组件
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // 配置音频源（不循环播放，空间混音）
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D音效
            audioSource.volume = soundVolume;
        }

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
        /// <returns>是否成功发射（true=发射成功，false=CD未到或弹药不足）</returns>
        public override bool Fire(Vector3 targetPoint)
        {
            // ✅ 1. 检查CD
            if (!TryConsume(ref bulletIntervalTimer, bulletFireInterval)) 
                return false;
            
            // ✅ 2. 检查并消耗弹药（在武器内部直接扣除）
            if (manager != null && !manager.TryConsumeBullet())
            {
                Debug.Log("[TimeGun] 子弹不足，无法开火");
                return false;
            }
            
            // ✅ 3. 发射子弹
            var dir = (targetPoint - muzzlePoint.position).normalized;
            Rigidbody rb = InstantiateAmmoRigidbody(bulletPrefab, muzzlePoint.position, Quaternion.LookRotation(dir,Vector3.up));
            rb.AddForce(dir * bulletSpeed, ForceMode.VelocityChange);

            // ✅ 4. 播放特效音效
            PlayMuzzleFlash(muzzlePoint);
            PlayFireSound(bulletFireSound);

            return true;
        }
        
        /// <summary>
        /// 开火, 对准枪正前方
        /// </summary>
        /// <returns>是否成功发射（true=发射成功，false=CD未到或弹药不足）</returns>
        public override bool Fire()
        {
            return Fire(muzzlePoint.position + muzzlePoint.forward * 100f);
        }

        /// <summary>
        /// 投掷榴弹, 向着发射口正前方 
        /// </summary>
        /// <returns>是否成功投掷（true=投掷成功，false=CD未到或弹药不足）</returns>
        public bool Throw()
        {
            return Throw(grenadeLaunchPoint.position + grenadeLaunchPoint.forward * 100f);
        }

        /// <summary>
        /// 投掷榴弹, 朝向目标点
        /// </summary>
        /// <param name="targetPoint">瞄准的目标位置</param>
        /// <returns>是否成功投掷（true=投掷成功，false=CD未到或弹药不足）</returns>
        public bool Throw(Vector3 targetPoint)
        {
            // ✅ 1. 检查CD
            if(!TryConsume(ref grenadeIntervalTimer, grenadeFireInterval)) 
                return false;
            
            // ✅ 2. 检查并消耗榴弹（在武器内部直接扣除）
            if (manager != null && !manager.TryConsumeGrenade())
            {
                Debug.Log("[TimeGun] 榴弹不足，无法投掷");
                return false;
            }
            
            // ✅ 3. 投掷榴弹
            Rigidbody rb = InstantiateAmmoRigidbody(grenadePrefab, grenadeLaunchPoint.position, Quaternion.identity);
            var dir = (targetPoint - grenadeLaunchPoint.position).normalized;
            rb.AddForce(dir * grenadeSpeed + Vector3.up * 5f, ForceMode.VelocityChange);

            // ✅ 4. 播放特效音效
            PlayGrenadeLaunchEffect(grenadeLaunchPoint);
            PlayFireSound(grenadeFireSound);

            return true;
        }

        /// <summary>
        /// 播放枪口火光特效
        /// </summary>
        /// <param name="spawnPoint">特效生成位置</param>
        private void PlayMuzzleFlash(Transform spawnPoint)
        {
            if (muzzleFlashPrefab == null) return;

            // 在枪口位置创建特效
            GameObject fx = Instantiate(muzzleFlashPrefab, spawnPoint.position, spawnPoint.rotation);
            
            // 设置特效为枪口的子物体，跟随枪口移动
            fx.transform.SetParent(spawnPoint);

            // 自动销毁特效
            Destroy(fx, effectDuration);

            // 如果特效包含粒子系统，设置自动停止
            var particleSystem = fx.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var main = particleSystem.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
        }

        /// <summary>
        /// 播放榴弹发射特效
        /// </summary>
        /// <param name="spawnPoint">特效生成位置</param>
        private void PlayGrenadeLaunchEffect(Transform spawnPoint)
        {
            if (grenadeLaunchEffectPrefab == null) return;

            // 在榴弹发射点位置创建特效
            GameObject fx = Instantiate(grenadeLaunchEffectPrefab, spawnPoint.position, spawnPoint.rotation);
            
            // 设置特效为发射点的子物体
            fx.transform.SetParent(spawnPoint);

            // 自动销毁特效
            Destroy(fx, effectDuration);

            // 如果特效包含粒子系统，设置自动停止
            var particleSystem = fx.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var main = particleSystem.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
        }

        /// <summary>
        /// 播放发射音效
        /// </summary>
        /// <param name="soundClip">音效片段</param>
        private void PlayFireSound(AudioClip soundClip)
        {
            if (soundClip == null || audioSource == null) return;

            // 使用 PlayOneShot 允许多个音效同时播放（不会被打断）
            audioSource.PlayOneShot(soundClip, soundVolume);
        }
    }
}