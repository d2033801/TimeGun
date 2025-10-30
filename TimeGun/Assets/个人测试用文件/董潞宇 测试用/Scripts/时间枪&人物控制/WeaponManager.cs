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
        [Tooltip("榴弹发射位置"), SerializeField] public Transform grenadeLaunchPoint;
        [Tooltip("榴弹预制件"), SerializeField] public GameObject grenadePrefab;
        

        private AbstractWeaponBase currentAbstractWeapon;

        void Start()
        {
            EquipWeapon(gunPrefab);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                ThrowGrenade();
            }
        }

        void EquipWeapon(GameObject weaponPrefab)
        {
            if (currentAbstractWeapon != null)
                Destroy(currentAbstractWeapon.gameObject);

            var weaponObj = Instantiate(weaponPrefab, handTransform);
            currentAbstractWeapon = weaponObj.GetComponent<AbstractWeaponBase>();
            currentAbstractWeapon.Initialize(this);     // 调用武器系统自身的初始化方法
        }

        /// <summary>
        /// 投掷榴弹 TODO: 现在实现的是手榴弹，需要改成枪榴弹
        /// </summary>
        void ThrowGrenade()
        {
            GameObject grenade = Instantiate(grenadePrefab, grenadeLaunchPoint.position + transform.forward, Quaternion.identity);       // 在本地坐标系下前方生成榴弹
            Rigidbody rb = grenade.GetComponent<Rigidbody>();
            rb.AddForce(transform.forward * 10f + Vector3.up * 3f, ForceMode.Impulse);
        }
    }
}
