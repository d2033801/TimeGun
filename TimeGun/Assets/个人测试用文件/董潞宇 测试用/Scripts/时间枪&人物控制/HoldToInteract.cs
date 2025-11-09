using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeGun
{
    /// <summary>
    /// 按住F键交互系统（Unity 6.2 + Input System）
    /// 
    /// 功能：
    /// - 玩家进入触发器区域后按住F键蓄力
    /// - 达到指定时长后触发交互完成事件
    /// - 提供进度查询接口供UI显示
    /// - 支持多个交互对象（每个对象独立管理）
    /// 
    /// 使用场景：
    /// - 游戏胜利条件（按住F键激活终点）
    /// - 开门/启动机关（需要按住一段时间）
    /// - 拆除炸弹/修理设备等长时间交互
    /// 
    /// 配置步骤：
    /// 1. 添加Collider组件并勾选IsTrigger
    /// 2. 配置LayerMask为玩家层
    /// 3. 在Inspector中分配Input Action（建议为"Hold"类型）
    /// 4. 订阅OnInteractionComplete事件（例如调用胜利UI）
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("TimeGun/Hold To Interact")]
    public class HoldToInteract : MonoBehaviour
    {
        #region 全局事件
        /// <summary>
        /// 交互完成事件（按住时间达到要求时触发）
        /// 参数：HoldToInteract - 触发的交互对象
        /// </summary>
        public static event Action<HoldToInteract> OnInteractionComplete;
        #endregion

        #region 检视器参数
        [Header("交互配置")]
        [Tooltip("按住时长（秒）")]
        [SerializeField, Min(0)] private float holdDuration = 2f;

        [Tooltip("交互输入动作（通常绑定F键）")]
        [SerializeField] private InputActionReference interactAction;

        [Tooltip("玩家层掩码（用于检测玩家进入触发器）")]
        [SerializeField] private LayerMask playerLayer = 1 << 3; // 默认Layer 3

        [Header("可选：提示UI")]
        [Tooltip("交互提示文本（例如'按住F键激活'）")]
        [SerializeField] private string interactPrompt = "按住 F 键激活";

        [Tooltip("是否显示调试Gizmo")]
        [SerializeField] private bool showDebugGizmo = true;

        [Header("调试信息")]
        [SerializeField, ReadOnly] private bool isPlayerInRange = false;
        [SerializeField, ReadOnly] private float currentHoldTime = 0f;
        [SerializeField, ReadOnly] private bool isCompleted = false;
        #endregion

        #region 公共接口
        /// <summary>
        /// 玩家是否在交互范围内
        /// </summary>
        public bool IsPlayerInRange => isPlayerInRange;

        /// <summary>
        /// 当前按住进度（0-1）
        /// </summary>
        public float Progress => holdDuration > 0 ? Mathf.Clamp01(currentHoldTime / holdDuration) : 1f;

        /// <summary>
        /// 剩余按住时间（秒）
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, holdDuration - currentHoldTime);

        /// <summary>
        /// 是否已完成交互
        /// </summary>
        public bool IsCompleted => isCompleted;

        /// <summary>
        /// 交互提示文本
        /// </summary>
        public string InteractPrompt => interactPrompt;

        /// <summary>
        /// 重置交互状态（允许再次交互）
        /// </summary>
        public void ResetInteraction()
        {
            isCompleted = false;
            currentHoldTime = 0f;
        }
        #endregion

        #region 内部状态
        private InputAction _interactAction;
        #endregion

        #region Unity生命周期
        private void Awake()
        {
            _interactAction = interactAction?.action;

            // 确保Collider为触发器
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[HoldToInteract] {gameObject.name} 的Collider未设置为Trigger，已自动修正", this);
                col.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            if (_interactAction != null && !_interactAction.enabled)
            {
                _interactAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_interactAction != null && _interactAction.enabled)
            {
                _interactAction.Disable();
            }
        }

        private void Update()
        {
            if (isCompleted || !isPlayerInRange) 
            {
                // 如果玩家离开范围，重置计时器
                if (!isPlayerInRange && currentHoldTime > 0f)
                {
                    currentHoldTime = 0f;
                }
                return;
            }

            // 检测按键状态
            bool isHolding = _interactAction?.IsPressed() ?? false;

            if (isHolding)
            {
                currentHoldTime += Time.deltaTime;

                // 达到按住时长
                if (currentHoldTime >= holdDuration)
                {
                    CompleteInteraction();
                }
            }
            else
            {
                // 松开按键，重置计时器
                if (currentHoldTime > 0f)
                {
                    currentHoldTime = 0f;
                }
            }
        }
        #endregion

        #region 触发器检测
        private void OnTriggerEnter(Collider other)
        {
            if (isCompleted) return;

            // 检查是否为玩家
            if (((1 << other.gameObject.layer) & playerLayer) != 0)
            {
                isPlayerInRange = true;
                Debug.Log($"[HoldToInteract] 玩家进入交互区域: {gameObject.name}");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // 检查是否为玩家
            if (((1 << other.gameObject.layer) & playerLayer) != 0)
            {
                isPlayerInRange = false;
                currentHoldTime = 0f;
                Debug.Log($"[HoldToInteract] 玩家离开交互区域: {gameObject.name}");
            }
        }
        #endregion

        #region 交互完成逻辑
        private void CompleteInteraction()
        {
            if (isCompleted) return;

            isCompleted = true;
            currentHoldTime = holdDuration; // 锁定进度为100%

            Debug.Log($"[HoldToInteract] 交互完成: {gameObject.name}");

            // 触发全局事件
            OnInteractionComplete?.Invoke(this);
        }
        #endregion

        #region Gizmo调试
        private void OnDrawGizmos()
        {
            if (!showDebugGizmo) return;

            var col = GetComponent<Collider>();
            if (col == null) return;

            // 根据状态改变颜色
            if (isCompleted)
            {
                Gizmos.color = Color.green; // 已完成
            }
            else if (isPlayerInRange)
            {
                Gizmos.color = Color.yellow; // 玩家在范围内
            }
            else
            {
                Gizmos.color = Color.cyan; // 待交互
            }

            // 绘制触发器范围
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
        #endregion
    }
}
