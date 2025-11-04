using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 挂载在玩家身上的武器管理器。
    /// 负责：
    /// 1) 装备/切换武器（实例化预制体并放到手部挂点）；
    /// 2) 转发开火/投掷请求到当前武器；
    /// 3) 将相机俯仰（pitch）同步给当前武器，用于对齐枪口仰俯。
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        /// <summary>
        /// 武器管理模式
        /// </summary>
        public enum WeaponMode
        {
            /// <summary>
            /// 固定武器模式：使用外部直接提供的武器实例，不可切换
            /// </summary>
            FixedWeapon,

            /// <summary>
            /// 动态装备模式：可通过预制体动态装备和切换武器
            /// </summary>
            DynamicEquip
        }

        [Header("武器管理模式")]
        [Tooltip("选择武器管理模式：\n- FixedWeapon: 使用固定的武器实例\n- DynamicEquip: 可动态装备预制体武器")]
        [SerializeField] private WeaponMode weaponMode = WeaponMode.DynamicEquip;

        [Header("固定武器模式设置")]
        [Tooltip("固定武器模式下使用的武器实例（需在场景中手动放置）")]
        [SerializeField] private AbstractWeaponBase fixedWeapon;

        [Header("动态装备模式设置")]
        /// <summary>
        /// 手部挂点（作为武器实例的父节点）。
        /// </summary>
        [Tooltip("手的位置"), SerializeField] private Transform handTransform;

        /// <summary>
        /// 默认装备的时间枪预制体。
        /// </summary>
        [Tooltip("时间枪预制件"), SerializeField] private GameObject gunPrefab;

        /// <summary>
        /// 当前已装备的武器实例（抽象基类）。
        /// </summary>
        private AbstractWeaponBase currentAbstractWeapon; // 获取武器后会自动初始化

        /// <summary>
        /// 启动时根据模式初始化武器。
        /// </summary>
        private void Start()
        {
            switch (weaponMode)
            {
                case WeaponMode.FixedWeapon:
                    InitializeFixedWeapon();
                    break;

                case WeaponMode.DynamicEquip:
                    EquipWeapon(gunPrefab);
                    break;
            }
        }

        /// <summary>
        /// 初始化固定武器模式
        /// </summary>
        private void InitializeFixedWeapon()
        {
            if (fixedWeapon == null)
            {
                Debug.LogError($"[WeaponManager] 固定武器模式下未指定武器实例！");
                return;
            }

            currentAbstractWeapon = fixedWeapon;
            currentAbstractWeapon.Initialize(this);
        }

        /// <summary>
        /// 实例化并装备指定武器预制体。
        /// 若已有武器实例，则会先销毁旧武器。
        /// 新武器会被设置为 <see cref="handTransform"/> 的子物体并调用其 Initialize。
        /// </summary>
        /// <param name="weaponPrefab">要装备的武器预制体（需包含 <see cref="AbstractWeaponBase"/> 组件）。</param>
        private void EquipWeapon(GameObject weaponPrefab)
        {
            if (weaponMode != WeaponMode.DynamicEquip)
            {
                Debug.LogWarning($"[WeaponManager] 当前为固定武器模式，无法动态装备武器！");
                return;
            }

            if (currentAbstractWeapon != null && weaponMode == WeaponMode.DynamicEquip)
                Destroy(currentAbstractWeapon.gameObject);

            var weaponObj = Instantiate(weaponPrefab, handTransform);
            currentAbstractWeapon = weaponObj.GetComponent<AbstractWeaponBase>();
            currentAbstractWeapon.Initialize(this); // 调用武器系统自身的初始化方法
        }

        #region 外部接口

        /// <summary>
        /// 请求当前武器朝给定目标点开火（通常由相机射线计算得到命中点）。
        /// </summary>
        /// <param name="targetPoint">目标点（世界坐标）。</param>
        public void TryFireWeapon(Vector3 targetPoint)
        {
            if (currentAbstractWeapon != null)
            {
                currentAbstractWeapon.Fire(targetPoint);
            }
        }

        /// <summary>
        /// 请求当前武器按其自身默认方向开火（无需目标点）。
        /// </summary>
        public void TryFireWeapon()
        {
            if (currentAbstractWeapon != null)
            {
                currentAbstractWeapon.Fire();
            }
        }

        /// <summary>
        /// 尝试执行投掷（若当前武器实现了 <see cref="IThrowable"/>）。
        /// </summary>
        public void TryThrow()
        {
            if (currentAbstractWeapon is IThrowable throwable)
            {
                throwable.Throw();
            }
        }

        /// <summary>
        /// 尝试朝指定目标点执行投掷（若当前武器实现了 <see cref="IThrowable"/>）。
        /// </summary>
        /// <param name="targetPoint">投掷目标点（世界坐标）。</param>
        public void TryThrow(Vector3 targetPoint)
        {
            if (currentAbstractWeapon is IThrowable throwable)
            {
                throwable.Throw(targetPoint);
            }
        }

        /// <summary>
        /// 将相机的俯仰角（pitch，度）同步到当前武器，用于对齐武器仰俯。
        /// </summary>
        /// <param name="pitch">俯仰角（度）。</param>
        public void UpdateWeaponPitch(float pitch)
        {
            currentAbstractWeapon?.UpdatePitchRotation(pitch);
        }

        #endregion
    }
}