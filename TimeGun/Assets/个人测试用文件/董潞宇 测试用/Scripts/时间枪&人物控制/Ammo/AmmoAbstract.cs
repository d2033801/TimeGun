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

        [Header("音效 Audio (可选)")]
        [Tooltip("击中/爆炸音效（可选）")] 
        [SerializeField] protected AudioClip impactSound;
        
        [Tooltip("音效音量(0-1)"), Range(0f, 1f)] 
        [SerializeField] protected float soundVolume = 0.5f;

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

        #region 音效辅助方法

        /// <summary>
        /// 在指定位置播放音效（使用AudioSource.PlayClipAtPoint）
        /// </summary>
        /// <param name="position">音效播放的3D空间位置</param>
        /// <param name="clip">要播放的音效片段（如果为null则使用impactSound）</param>
        /// <remarks>
        /// 使用PlayClipAtPoint的优点：
        /// - 无需管理AudioSource组件
        /// - 3D空间音效，自动根据距离衰减
        /// - 播放完自动销毁，无内存泄漏
        /// - 支持同时播放多个音效
        /// </remarks>
        protected void PlaySoundAtPoint(Vector3 position, AudioClip clip = null)
        {
            AudioClip soundToPlay = clip ?? impactSound;
            if (soundToPlay == null) return;

            AudioSource.PlayClipAtPoint(soundToPlay, position, soundVolume);
        }

        /// <summary>
        /// 在当前位置播放音效
        /// </summary>
        /// <param name="clip">要播放的音效片段（如果为null则使用impactSound）</param>
        protected void PlaySound(AudioClip clip = null)
        {
            PlaySoundAtPoint(transform.position, clip);
        }

        /// <summary>
        /// 在击中点播放音效（自动从Collision中提取击中点位置）
        /// </summary>
        /// <param name="hitCollision">碰撞信息</param>
        /// <param name="clip">要播放的音效片段（如果为null则使用impactSound）</param>
        protected void PlayHitSound(Collision hitCollision, AudioClip clip = null)
        {
            if (hitCollision.contactCount > 0)
            {
                Vector3 hitPoint = hitCollision.GetContact(0).point;
                PlaySoundAtPoint(hitPoint, clip);
            }
            else
            {
                PlaySound(clip);
            }
        }

        #endregion

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