using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 该脚本挂载到玩家角色上，负责武器的装备和投掷榴弹功能。
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Tooltip("手的位置"), SerializeField]private Transform handTransform;
        [Tooltip("时间枪预制件"), SerializeField] public GameObject gunPrefab;

        private AbstractWeaponBase currentAbstractWeapon;           // 获取武器后会自动初始化

        void Start()
        {
            EquipWeapon(gunPrefab);
        }


        void EquipWeapon(GameObject weaponPrefab)
        {
            if (currentAbstractWeapon != null)
                Destroy(currentAbstractWeapon.gameObject);

            var weaponObj = Instantiate(weaponPrefab, handTransform);
            currentAbstractWeapon = weaponObj.GetComponent<AbstractWeaponBase>();
            currentAbstractWeapon.Initialize(this);     // 调用武器系统自身的初始化方法
        }



        #region 外部接口
        public void TryFireWeapon()
        {
            if (currentAbstractWeapon != null)
            {
                currentAbstractWeapon.Fire();
            }
        }

        public void TryThrow()
        {
            if (currentAbstractWeapon is IThrowable throwable)
            {
                throwable.Throw();
            }
        }

        public void UpdateWeaponPitch(float pitch)
        {
            currentAbstractWeapon?.UpdatePitchRotation(pitch);
        }

        #endregion
    }
}
