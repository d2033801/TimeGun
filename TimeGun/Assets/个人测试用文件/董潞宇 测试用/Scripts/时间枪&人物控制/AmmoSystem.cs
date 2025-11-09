using System;
using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 弹药系统（Unity 6.2 + 事件驱动架构）
    /// 
    /// 功能：
    /// - 管理子弹和榴弹的备弹量和CD
    /// - 支持自动恢复备弹（基于时间）
    /// - 提供UI接口（当前/最大弹药数、CD进度）
    /// - 事件驱动（弹药变化时触发全局事件，UI可订阅）
    /// 
    /// 使用场景：
    /// - 挂载在玩家身上（或武器管理器上）
    /// - UI系统订阅 OnAmmoChanged 事件来更新显示
    /// - 武器系统调用 TryConsumeBullet/TryConsumeGrenade 来消耗弹药
    /// 
    /// 设计理念：
    /// - 低耦合：通过事件与UI解耦，武器系统只需调用Try方法
    /// - 高内聚：所有弹药逻辑封装在一个组件内
    /// - 易扩展：可以轻松添加新的弹药类型
    /// </summary>
    [AddComponentMenu("TimeGun/Ammo System")]
    public class AmmoSystem : MonoBehaviour
    {
        #region 全局事件
        /// <summary>
        /// 弹药变化事件（当弹药数量或CD变化时触发）
        /// 参数：AmmoType - 弹药类型，int current - 当前弹药数，int max - 最大弹药数
        /// </summary>
        public static event Action<AmmoType, int, int> OnAmmoChanged;

        /// <summary>
        /// 弹药恢复事件（当一发弹药恢复完成时触发）
        /// 参数：AmmoType - 弹药类型
        /// </summary>
        public static event Action<AmmoType> OnAmmoRestored;
        #endregion

        #region 枚举定义
        /// <summary>
        /// 弹药类型
        /// </summary>
        public enum AmmoType
        {
            Bullet,    // 子弹
            Grenade    // 榴弹
        }
        #endregion

        #region 检视器参数
        [Header("子弹配置")]
        [Tooltip("子弹最大备弹量")]
        [SerializeField, Min(0)] private int maxBullets = 2;

        [Tooltip("子弹恢复CD（秒）")]
        [SerializeField, Min(0)] private float bulletRestoreTime = 40f;

        [Header("榴弹配置")]
        [Tooltip("榴弹最大备弹量")]
        [SerializeField, Min(0)] private int maxGrenades = 1;

        [Tooltip("榴弹恢复CD（秒）")]
        [SerializeField, Min(0)] private float grenadeRestoreTime = 40f;

        [Header("调试信息")]
        [SerializeField, ReadOnly] private int currentBullets = 2;
        [SerializeField, ReadOnly] private int currentGrenades = 1;
        [SerializeField, ReadOnly] private float bulletRestoreTimer = 0f;
        [SerializeField, ReadOnly] private float grenadeRestoreTimer = 0f;
        #endregion

        #region 公共接口 - 弹药状态查询
        /// <summary>
        /// 获取当前子弹数量
        /// </summary>
        public int CurrentBullets => currentBullets;

        /// <summary>
        /// 获取最大子弹数量
        /// </summary>
        public int MaxBullets => maxBullets;

        /// <summary>
        /// 获取当前榴弹数量
        /// </summary>
        public int CurrentGrenades => currentGrenades;

        /// <summary>
        /// 获取最大榴弹数量
        /// </summary>
        public int MaxGrenades => maxGrenades;

        /// <summary>
        /// 获取子弹恢复进度（0-1）
        /// </summary>
        public float BulletRestoreProgress => bulletRestoreTime > 0 ? Mathf.Clamp01(bulletRestoreTimer / bulletRestoreTime) : 1f;

        /// <summary>
        /// 获取榴弹恢复进度（0-1）
        /// </summary>
        public float GrenadeRestoreProgress => grenadeRestoreTime > 0 ? Mathf.Clamp01(grenadeRestoreTimer / grenadeRestoreTime) : 1f;

        /// <summary>
        /// 获取子弹剩余恢复时间（秒）
        /// </summary>
        public float BulletRestoreRemainingTime => Mathf.Max(0f, bulletRestoreTime - bulletRestoreTimer);

        /// <summary>
        /// 获取榴弹剩余恢复时间（秒）
        /// </summary>
        public float GrenadeRestoreRemainingTime => Mathf.Max(0f, grenadeRestoreTime - grenadeRestoreTimer);
        #endregion

        #region 公共接口 - 弹药消耗
        /// <summary>
        /// 尝试消耗一发子弹
        /// </summary>
        /// <returns>是否成功消耗（true=有弹药且已消耗，false=弹药不足）</returns>
        public bool TryConsumeBullet()
        {
            if (currentBullets <= 0) return false;

            currentBullets--;

            // 如果弹药未满且未在恢复中，启动恢复计时器
            if (currentBullets < maxBullets && bulletRestoreTimer == 0f)
            {
                bulletRestoreTimer = 0.01f; // 避免0值判断问题
            }

            OnAmmoChanged?.Invoke(AmmoType.Bullet, currentBullets, maxBullets);
            return true;
        }

        /// <summary>
        /// 尝试消耗一发榴弹
        /// </summary>
        /// <returns>是否成功消耗（true=有弹药且已消耗，false=弹药不足）</returns>
        public bool TryConsumeGrenade()
        {
            if (currentGrenades <= 0) return false;

            currentGrenades--;

            // 如果弹药未满且未在恢复中，启动恢复计时器
            if (currentGrenades < maxGrenades && grenadeRestoreTimer == 0f)
            {
                grenadeRestoreTimer = 0.01f; // 避免0值判断问题
            }

            OnAmmoChanged?.Invoke(AmmoType.Grenade, currentGrenades, maxGrenades);
            return true;
        }
        #endregion

        #region 公共接口 - 弹药管理
        /// <summary>
        /// 立即补满所有弹药（例如拾取弹药箱时调用）
        /// </summary>
        public void RefillAll()
        {
            currentBullets = maxBullets;
            currentGrenades = maxGrenades;
            bulletRestoreTimer = 0f;
            grenadeRestoreTimer = 0f;

            OnAmmoChanged?.Invoke(AmmoType.Bullet, currentBullets, maxBullets);
            OnAmmoChanged?.Invoke(AmmoType.Grenade, currentGrenades, maxGrenades);
        }

        /// <summary>
        /// 重置弹药系统（回到初始满弹状态）
        /// </summary>
        public void ResetAmmo()
        {
            RefillAll();
        }
        #endregion

        #region Unity生命周期
        private void Start()
        {
            // 初始化为满弹状态
            currentBullets = maxBullets;
            currentGrenades = maxGrenades;
            bulletRestoreTimer = 0f;
            grenadeRestoreTimer = 0f;

            // 触发初始事件
            OnAmmoChanged?.Invoke(AmmoType.Bullet, currentBullets, maxBullets);
            OnAmmoChanged?.Invoke(AmmoType.Grenade, currentGrenades, maxGrenades);
        }

        private void Update()
        {
            UpdateAmmoRestore(ref bulletRestoreTimer, bulletRestoreTime, ref currentBullets, maxBullets, AmmoType.Bullet);
            UpdateAmmoRestore(ref grenadeRestoreTimer, grenadeRestoreTime, ref currentGrenades, maxGrenades, AmmoType.Grenade);
        }
        #endregion

        #region 内部逻辑
        /// <summary>
        /// 更新弹药恢复逻辑（通用方法）
        /// </summary>
        private void UpdateAmmoRestore(ref float timer, float restoreTime, ref int current, int max, AmmoType ammoType)
        {
            // 只有在弹药未满且恢复时间大于0时才恢复
            if (current >= max || restoreTime <= 0f) 
            {
                timer = 0f;
                return;
            }

            // 累加计时器
            timer += Time.deltaTime;

            // 达到恢复时间
            if (timer >= restoreTime)
            {
                timer = 0f;
                current++;

                // 限制最大值
                current = Mathf.Min(current, max);

                // 触发事件
                OnAmmoChanged?.Invoke(ammoType, current, max);
                OnAmmoRestored?.Invoke(ammoType);
            }
        }
        #endregion
    }
}
