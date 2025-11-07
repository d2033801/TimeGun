using UnityEngine;
using Utility;
using TimeGun;  // ✅ 添加 SmartDoorController 所在的命名空间

namespace TimeRewind
{
    /// <summary>
    /// 智能门的时间回溯组件
    /// - 录制和回放 Animator 动画状态
    /// - 录制和回放门的自定义状态（IsOpen、自动关门计时器等）
    /// - 在回溯期间冻结门的检测和控制逻辑
    /// </summary>
    [RequireComponent(typeof(SmartDoorController))]
    public class SmartDoorTimeRewind : AnimatorTimeRewind
    {
        //==================== 组件缓存 ====================
        private SmartDoorController _doorController;
        private SmartDoorController DoorController => _doorController ??= GetComponent<SmartDoorController>();

        //==================== 门状态历史缓冲 ====================
        private RingBuffer<DoorStateSnapshot> _doorStateHistory;

        /// <summary>
        /// 门的自定义状态快照
        /// </summary>
        private struct DoorStateSnapshot
        {
            public bool IsDoorOpen;          // 门是否开启
            public float AutoCloseTimer;     // 自动关门计时器
        }

        //==================== 冻结管理器 ====================
        /// <summary>
        /// 门控制器的冻结状态
        /// </summary>
        private struct DoorFreezeState
        {
            public bool WasEnabled;
        }

        private ComponentFreezeManager<DoorFreezeState> _doorFreezeManager 
            = new ComponentFreezeManager<DoorFreezeState>();

        //==================== 帧计数重写（包含动画 + 门状态）====================
        protected override int frameCount => 
            Mathf.Min(
                base.frameCount,  // Animator 的帧数
                _doorStateHistory?.Count ?? 0  // 门状态的帧数
            );

        //==================== 初始化 ====================
        protected override void MainInit()
        {
            base.MainInit();

            // 创建门状态专用的历史缓冲
            _doorStateHistory = RewindInit<DoorStateSnapshot>(out _);

            Debug.Log($"[SmartDoorTimeRewind] 初始化完成: {gameObject.name}");
        }

        //==================== 录制与回放 ====================
        /// <summary>
        /// 录制一帧（Animator + 门状态）
        /// </summary>
        protected override void RecordOneSnap()
        {
            // 录制 Animator 状态
            base.RecordOneSnap();

            // 录制门的自定义状态
            if (DoorController != null)
            {
                var doorSnap = new DoorStateSnapshot
                {
                    IsDoorOpen = DoorController.IsDoorOpen,
                    AutoCloseTimer = GetAutoCloseTimer()  // 使用反射或公开属性获取
                };

                _doorStateHistory.Push(doorSnap);
            }
        }

        /// <summary>
        /// 回放一帧（Animator + 门状态）
        /// </summary>
        protected override void RewindOneSnap()
        {
            // 回放 Animator 状态
            base.RewindOneSnap();

            // 回放门的自定义状态
            if (DoorController != null && _doorStateHistory.Count > 0)
            {
                var doorSnap = _doorStateHistory.PopBack();

                // 强制设置门的状态（绕过门的自动控制逻辑）
                if (doorSnap.IsDoorOpen && !DoorController.IsDoorOpen)
                {
                    DoorController.ForceOpen();
                }
                else if (!doorSnap.IsDoorOpen && DoorController.IsDoorOpen)
                {
                    DoorController.ForceClose();
                }

                SetAutoCloseTimer(doorSnap.AutoCloseTimer);
            }
        }

        //==================== 冻结控制 ====================
        /// <summary>
        /// 回溯开始：冻结门的控制逻辑（防止自动开关门）
        /// </summary>
        protected override void OnStartRewind()
        {
            base.OnStartRewind();
            RequestFreezeDoor();
        }

        /// <summary>
        /// 回溯结束：解冻门的控制逻辑
        /// </summary>
        protected override void OnStopRewind()
        {
            base.OnStopRewind();
            ReleaseFreezeDoor();
        }

        /// <summary>
        /// 暂停开始：冻结门的控制逻辑
        /// </summary>
        protected override void OnStartPause()
        {
            base.OnStartPause();
            RequestFreezeDoor();
        }

        /// <summary>
        /// 暂停结束：解冻门的控制逻辑
        /// </summary>
        protected override void OnStopPause()
        {
            base.OnStopPause();
            ReleaseFreezeDoor();
        }

        /// <summary>
        /// 请求冻结门控制器（引用计数+1）
        /// </summary>
        private void RequestFreezeDoor()
        {
            var currentState = new DoorFreezeState
            {
                WasEnabled = DoorController != null && DoorController.enabled
            };

            // 仅首次冻结时执行
            if (_doorFreezeManager.RequestFreeze(currentState))
            {
                if (DoorController != null)
                {
                    DoorController.enabled = false;  // 禁用门控制器，停止检测和自动开关
                }
            }
        }

        /// <summary>
        /// 释放冻结门控制器（引用计数-1）
        /// </summary>
        private void ReleaseFreezeDoor()
        {
            // 仅完全解冻时执行
            if (_doorFreezeManager.ReleaseFreeze(out var savedState))
            {
                if (savedState.WasEnabled && DoorController != null)
                {
                    DoorController.enabled = true;  // 恢复门控制器
                }
            }
        }

        //==================== 辅助方法（访问门控制器的私有字段）====================
        /// <summary>
        /// 获取门的自动关门计时器（使用反射访问私有字段）
        /// </summary>
        private float GetAutoCloseTimer()
        {
            if (DoorController == null) return 0f;

            // 使用反射获取私有字段 _autoCloseTimer
            var field = typeof(SmartDoorController).GetField(
                "_autoCloseTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            return field != null ? (float)field.GetValue(DoorController) : 0f;
        }

        /// <summary>
        /// 设置门的自动关门计时器（使用反射设置私有字段）
        /// </summary>
        private void SetAutoCloseTimer(float value)
        {
            if (DoorController == null) return;

            // 使用反射设置私有字段 _autoCloseTimer
            var field = typeof(SmartDoorController).GetField(
                "_autoCloseTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            field?.SetValue(DoorController, value);
        }

        //==================== 调试 ====================
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && DoorController != null)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.color = IsRewinding ? Color.cyan : Color.white;
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 3f,
                    $"门回溯状态: {(IsRewinding ? "回溯中" : "正常")}\n" +
                    $"历史帧数: {frameCount}\n" +
                    $"门状态: {(DoorController.IsDoorOpen ? "开启" : "关闭")}"
                );
#endif
            }
        }

        //==================== 清理 ====================
        private void OnDestroy()
        {
            // ❌ 不调用 base.OnDestroy()，因为它是 private
            // base.OnDestroy();
            
            _doorStateHistory = null;
            Debug.Log($"[SmartDoorTimeRewind] 清理完成: {gameObject.name}");
        }
    }
}
