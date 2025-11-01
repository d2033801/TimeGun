using UnityEngine;

namespace Utility
{
    /// <summary>
    /// 与计时器相关的工具类
    /// </summary>
    public class Timer
    {
        /// <summary>
        /// 消耗“冷却计时器”, 若计时器达到间隔则重置计时器并返回 true
        /// </summary>
        /// <param name="timer">计时器</param>
        /// <param name="interval">间隔</param>
        /// <returns></returns>
        public static bool TryConsumeTimer(ref float timer, float interval)
        {
            if (timer < interval) return false;
            timer = 0f;
            return true;
        }
    }
}

