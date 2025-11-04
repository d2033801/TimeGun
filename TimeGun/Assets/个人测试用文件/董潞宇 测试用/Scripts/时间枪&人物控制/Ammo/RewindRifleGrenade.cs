using System.Collections;
using TimeRewind;
using UnityEngine;

namespace TimeGun
{
    public class RewindRifleGrenade : AmmoRewindAbstract
    {
        [Header("榴弹设定 Grenade Settings")]
        [SerializeField, Tooltip("引信时间")] public float fuseSeconds = 2.0f;
        [SerializeField, Tooltip("爆炸半径")] public float explosionRadius = 4f;
        [SerializeField, Tooltip("爆炸类型")] private GrenadeExplosionMode explosionMode = GrenadeExplosionMode.OnImpact;
        [SerializeField, Tooltip("效果模式")] private GrenadeEffectMode effectMode = GrenadeEffectMode.Rewind;
        [SerializeField, Tooltip("暂停持续时间(仅在Pause模式下生效)")] private float pauseDuration = 3f;
        
        [Header("特效 Effect References")]
        [SerializeField, Tooltip("爆炸特效")] private GameObject explosionEffectPrefab;

        enum GrenadeExplosionMode
        {
            OnImpact,
            Timed
        }

        enum GrenadeEffectMode
        {
            Rewind,
            Pause
        }

        private bool _isExploded = false;
        private bool _hasBeenTriggered = false;

        private IEnumerator FuseRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Explode();
        }

        private void Explode()
        {
            if (_isExploded) return;
            _isExploded = true;

            Vector3 center = transform.position;

            // ✅ 播放爆炸音效（使用基类提供的方法）
            PlaySound();

            // 播放爆炸特效
            if (explosionEffectPrefab != null)
            {
                GameObject fx = Instantiate(explosionEffectPrefab, center, Quaternion.identity);
                fx.transform.localScale = Vector3.one * explosionRadius;
                var main = fx.GetComponent<ParticleSystem>().main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }

            // 应用爆炸效果
            Collider[] hits = Physics.OverlapSphere(center, explosionRadius);
            
            if (effectMode == GrenadeEffectMode.Pause)
            {
                foreach (var c in hits)
                {
                    TryTriggerPause(c, pauseDuration);
                }
            }
            else
            {
                foreach (var c in hits)
                {
                    TryTriggerRewind(c, rewindSecondsOnHit);
                }
            }

            Destroy(gameObject);
        }

        private void TryTriggerPause(Collider targetCollider, float duration)
        {
            if (targetCollider == null || duration <= 0f) return;

            var rewindObj = targetCollider.GetComponentInParent<AbstractTimeRewindObject>();
            if (rewindObj == null) return;

            rewindObj.StartPause();
            rewindObj.StartCoroutine(StopPauseAfterDelay(rewindObj, duration));
        }

        private IEnumerator StopPauseAfterDelay(AbstractTimeRewindObject rewindObj, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (rewindObj != null && rewindObj.IsPaused)
            {
                rewindObj.StopPause();
            }
        }

        protected override void HandleHit(Collision hitCollision)
        {
            if (_hasBeenTriggered) return;
            _hasBeenTriggered = true;
            
            switch (explosionMode)
            {
                case GrenadeExplosionMode.OnImpact:
                    StartCoroutine(FuseRoutine(0.02f));
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