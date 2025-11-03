using System.Collections;
using UnityEngine;

namespace TimeGun
{
    public class RewindRifleGrenade : AmmoRewindAbstract
    {
        [Header("榴弹设定 Grenade Settings")]
        [SerializeField, Tooltip("引信时间")] public float fuseSeconds = 2.0f;        // 引信时间
        [SerializeField, Tooltip("爆炸半径")] public float explosionRadius = 4f;
        [SerializeField, Tooltip("爆炸类型")] private GrenadeExplosionMode explosionMode = GrenadeExplosionMode.OnImpact;
        [Header("特效 Effect References")]
        [SerializeField, Tooltip("爆炸特效")] private GameObject explosionEffectPrefab;

        enum GrenadeExplosionMode
        {
            /// <summary>
            /// 碰撞时立即爆炸
            /// </summary>
            OnImpact,
            /// <summary>
            /// 碰撞后延时引爆
            /// </summary>
            Timed
        }
        private bool _isExploded = false;               // 是否已爆炸
        private bool _hasBeenTriggered = false;         // 是否已被碰撞触发过

        /// <summary>
        /// 引信倒计时协程
        /// </summary>
        /// <param name="seconds">触发引信多久后爆炸</param>
        /// <returns></returns>
        private IEnumerator FuseRoutine(float seconds)
        {
            // 若不希望受 Time.timeScale 影响，改用 WaitForSecondsRealtime(fuseSeconds)
            yield return new WaitForSeconds(seconds);       // 等待引信时间后再次调用函数
            Explode();
        }

        /// <summary>
        /// 爆炸处理
        /// </summary>
        private void Explode()
        {
            if (_isExploded)
            {
                return;
            }
            _isExploded = true;


            Vector3 center = transform.position;
            if (explosionEffectPrefab != null)
            {
                // 在命中点创建特效（使用第一个碰撞点）
                GameObject fx = Instantiate(explosionEffectPrefab, center, Quaternion.identity);
                fx.transform.localScale = Vector3.one * explosionRadius;
                // 设置特效在播放完后自动销毁
                var main = fx.GetComponent<ParticleSystem>().main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
            Collider[] hits = Physics.OverlapSphere(center, explosionRadius);
            foreach (var c in hits)
            {
                TryTriggerRewind(c, rewindSecondsOnHit);
            }

            Destroy(gameObject);

        }

        protected override void HandleHit(Collision hitCollision)
        {
            if (_hasBeenTriggered) return;
            _hasBeenTriggered = true;
            switch (explosionMode)
            {
                
                case GrenadeExplosionMode.OnImpact:
                    StartCoroutine(FuseRoutine(0.1f));      // 碰撞后短暂延时以确保物理稳定
                    break;
                case GrenadeExplosionMode.Timed:
                    StartCoroutine(FuseRoutine(fuseSeconds));
                    break;
            }
        }

        protected override void OnLifeExpired()
        {
            Explode();
        }

        // ==== Editor Gizmos ====
        [Header("调试")]
        [SerializeField, Tooltip("在编辑器中绘制爆炸范围（选中该物体时显示）")]
        private bool drawExplosionGizmo = true;

        [SerializeField, Tooltip("Gizmo 填充色（带透明）")]
        private Color explosionGizmoColor = new Color(1f, 0.6f, 0f, 0.15f);

        [SerializeField, Tooltip("Gizmo 线框颜色")]
        private Color explosionGizmoWireColor = new Color(1f, 0.6f, 0f, 0.9f);

        private void OnDrawGizmosSelected()
        {
            if (!drawExplosionGizmo) return;

            // 使用单位缩放，避免物体缩放影响半径显示
            var prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);

            Gizmos.color = explosionGizmoColor;
            Gizmos.DrawSphere(Vector3.zero, explosionRadius);

            Gizmos.color = explosionGizmoWireColor;
            Gizmos.DrawWireSphere(Vector3.zero, explosionRadius);

            Gizmos.matrix = prev;

#if UNITY_EDITOR
            UnityEditor.Handles.color = explosionGizmoWireColor;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f, $"Radius: {explosionRadius:0.##} m");
#endif
        }
    }
}