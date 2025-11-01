using TimeRewind;
using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 子弹/榴弹等抽象基类
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class AmmoAbstract : MonoBehaviour
    {
        [Header("通常设定 Common")]
        [Tooltip("生命周期 到时会自动销毁")] public float lifeTime = 6f;           // 自动销毁时间

        [Tooltip("可受影响的层")] public LayerMask hitLayers = ~0;      // 可打中的层

        protected Rigidbody rb; // 子弹物理刚体引用
        private float lifeTimer = 0f;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        protected virtual void Update()
        {
            lifeTimer += Time.deltaTime;
            if (Utility.Timer.TryConsumeTimer(ref lifeTimer, lifeTime))
            {
                OnLifeExpired();
            }
        }

        /// <summary>
        /// 生命周期结束时的事件
        /// </summary>
        protected virtual void OnLifeExpired()
        {
            Destroy(gameObject);
            // Despawn(); 留给对象池扩展用
        }


        protected virtual void OnCollisionEnter(Collision collision)
        {

            // 只处理指定层
            if (((1 << collision.gameObject.layer) & hitLayers) == 0) return;

            collision.rigidbody?.AddForce(-collision.impulse, ForceMode.Impulse);
            HandleHit(collision);
        }

        /// <summary>
        /// 命中处理：由基类在 OnCollisionEnter 经 hitLayers 筛选后调用。
        /// 派生类应在此实现命中后的实际效果（如伤害、击退、爆炸、时间回溯等）。
        /// </summary>
        /// <param name="hitCollision">被命中的物体碰撞信息（接触点、相对速度、法线等）。</param>
        /// <remarks>
        /// 使用建议：
        /// - 基类已完成层过滤，只有命中允许的层才会回调本方法；
        /// - 若需要销毁/回收弹体，请在效果完成后调用 Destroy(gameObject)，或重写 OnLifeExpired/Despawn；
        /// - 避免在此直接强行修改刚体物理状态导致与物理解算冲突（如瞬移/速度置零），必要时可先禁用碰撞或设 rb.isKinematic。
        /// </remarks>
        protected abstract void HandleHit(Collision hitCollision);


        #region 可选对象池扩展
        // 注意在使用对象池时还要改上面的 OnLifeExpired 

        /// <summary>
        /// 留给对象池扩展用的方法
        /// </summary>
        protected virtual void Despawn()
        {
            // 如果你用对象池，这里应归还池；简单示例先用 SetActive(false)
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 留给对象池扩充用
        /// </summary>
        protected virtual void OnEnable()
        {
            lifeTimer = 0f;
        }
        #endregion
    }





}