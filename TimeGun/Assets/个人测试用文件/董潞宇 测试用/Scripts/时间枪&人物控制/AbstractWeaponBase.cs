using UnityEngine;

namespace TimeGun
{
    public abstract class AbstractWeaponBase : MonoBehaviour
    {
        protected WeaponManager manager;

        public virtual void Initialize(WeaponManager wm)
        {
            manager = wm;
        }

        public abstract void Fire();
    }

}
