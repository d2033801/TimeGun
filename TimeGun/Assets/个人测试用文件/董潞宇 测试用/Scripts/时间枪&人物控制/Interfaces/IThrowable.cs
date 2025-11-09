using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 可投掷武器能力接口（如枪榴弹、手榴弹等）。
    /// </summary>
    /// <remarks>
    /// 实现方通常会：
    /// - 使用自身的发射点（如 muzzlePoint）与力度/重力参数完成投掷；
    /// - 在投掷前进行冷却、弹药等校验；
    /// - 触发相应的动画、特效与声音。
    /// </remarks>
    public interface IThrowable
    {
        /// <summary>
        /// 按武器的默认方向与力度执行投掷。
        /// </summary>
        /// <returns>是否成功投掷（true=投掷成功，false=CD未到或其他原因）</returns>
        /// <remarks>
        /// 典型实现会以发射点的 forward 为方向进行投掷。
        /// </remarks>
        bool Throw();

        /// <summary>
        /// 朝给定世界坐标的目标点执行投掷（实现可据此计算方向或初速度）。
        /// </summary>
        /// <param name="targetPoint">目标点（世界坐标）。实现应将其视为"期望命中点"或"瞄准参考点"。</param>
        /// <returns>是否成功投掷（true=投掷成功，false=CD未到或其他原因）</returns>
        bool Throw(Vector3 targetPoint);
    }
}