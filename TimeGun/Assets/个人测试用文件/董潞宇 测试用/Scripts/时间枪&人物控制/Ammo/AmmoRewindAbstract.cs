using System;
using TimeGun;
using TimeRewind;
using UnityEngine;

namespace TimeGun
{
    public abstract class AmmoRewindAbstract : AmmoAbstract
    {
        // TODO: 或许可以单独给可回溯物体创建一个layer，然后用rewindLayers区分普通物体和可回溯物体，实现不同的命中效果
        [Header("时间回溯设定 Time Rewind")]
        [Tooltip("子弹/榴弹让物体回溯的时间")] public float rewindSecondsOnHit = 1f; // 命中目标时触发的回溯秒数（派生类可覆盖）TODO: 现阶段没这功能只是放着作为占位符
        [Tooltip("子弹/榴弹让物体回溯的时间")] public float rewindSpeedOnHit = 2f; // 命中目标时触发的回溯秒数（派生类可覆盖）TODO: 现阶段没这功能只是放着作为占位符
        [Tooltip("回溯应用模式：Instant=瞬时跳回；Gradual=渐进回放"), SerializeField] 
        private RewindMode rewindMode = RewindMode.Gradual;
        private enum RewindMode
        {
            Instant,  // 瞬时跳回
            Gradual   // 渐进回放
        }

        /// <summary>
        /// 触发目标回溯
        /// </summary>
        /// <param name="targetCollider">被触发物体</param>
        /// <param name="seconds">回溯时间</param>
        protected virtual void TryTriggerRewind(Collider targetCollider, float seconds)
        {
            if (targetCollider == null || seconds <= 0f) return;

            // 尝试从被击中的对象或其父级获取回溯组件
            var rewindObj = targetCollider.GetComponentInParent<AbstractTimeRewindObject>();
            if (rewindObj == null) return;
            switch (rewindMode)
            {
                case RewindMode.Gradual:
                    // 若 选择的是渐进回溯 则 使用此行
                    rewindObj.StartRewind(rewindSpeedOnHit);
                    return;
                case RewindMode.Instant:
                    // 若 瞬时跳回 则 使用此行
                    rewindObj.RewindBySeconds(seconds); 
                    return;
            }

        }
    }

}
