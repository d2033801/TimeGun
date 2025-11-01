using UnityEngine;

namespace TimeGun
{
    public abstract class AbstractWeaponBase : MonoBehaviour
    {

        [SerializeField, Tooltip("枪口位置")] protected Transform muzzlePoint;

        protected WeaponManager manager; // 不知道获取有什么用反正先获取了

        /// <summary>
        /// 初始化武器（由 WeaponManager 调用）。
        /// </summary>
        /// <param name="wm">武器管理器实例。</param>
        public virtual void Initialize(WeaponManager wm)
        {
            manager = wm;
        }

        /// <summary>
        /// 按“瞄准目标点”开火（例如通过相机射线得到的命中点）。
        /// 方向通常由 targetPoint - muzzlePoint.position 决定。
        /// </summary>
        /// <param name="targetPoint">目标点（世界坐标）。</param>
        public abstract void Fire(Vector3 targetPoint);

        /// <summary>
        /// 无目标点版本的开火（朝当前枪口正前方射击或由武器自身决定方向）。
        /// </summary>
        public abstract void Fire();

        /// <summary>
        /// 应用俯仰角到武器（仅修改本地 X 轴），用于让武器跟随相机俯仰。
        /// </summary>
        /// <param name="pitch">俯仰角（度）。</param>
        public virtual void UpdatePitchRotation(float pitch)
        {
            // 只修改竖直方向
            Vector3 euler = transform.localEulerAngles;
            euler.x = pitch;
            transform.localEulerAngles = euler;
        }

        /// <summary>
        /// 实例化弹药预制体，并返回其 Rigidbody 组件引用。
        /// 注意：若预制体不包含 Rigidbody，将返回 null。
        /// </summary>
        /// <param name="ammoPrefab">弹药/投射物预制体。</param>
        /// <param name="position">生成位置（世界坐标）。</param>
        /// <param name="rotation">生成旋转（世界旋转）。</param>
        /// <returns>新实例上的 Rigidbody；若缺失 Rigidbody 则为 null。</returns>
        protected Rigidbody InstantiateAmmoRigidbody(GameObject ammoPrefab, Vector3 position, Quaternion rotation)
        {
            GameObject bullet = Instantiate(ammoPrefab, position, rotation); // 在本地坐标系下前方生成榴弹
            return bullet.GetComponent<Rigidbody>();
        }


        /// <summary>
        /// 消耗“冷却计时器”, 若计时器达到间隔则重置计时器并返回 true
        /// </summary>
        /// <param name="timer">计时器</param>
        /// <param name="interval">间隔</param>
        /// <returns></returns>
        protected virtual bool TryConsume(ref float timer, float interval)
        {
            return Utility.Timer.TryConsumeTimer(ref timer, interval);
        }
    }

}

