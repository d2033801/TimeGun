using UnityEngine;

namespace TimeGun
{
    public abstract class AbstractWeaponBase : MonoBehaviour
    {

        [SerializeField, Tooltip("枪口位置")] protected Transform muzzlePoint;

        protected WeaponManager manager;        // 不知道获取有什么用反正先获取了
        

        public virtual void Initialize(WeaponManager wm)
        {
            manager = wm;
        }

        public abstract void Fire();

        public void UpdatePitchRotation(float pitch)
        {
            // 只修改竖直方向
            Vector3 euler = transform.localEulerAngles;
            euler.x = pitch;
            transform.localEulerAngles = euler;
        }
    }

}
