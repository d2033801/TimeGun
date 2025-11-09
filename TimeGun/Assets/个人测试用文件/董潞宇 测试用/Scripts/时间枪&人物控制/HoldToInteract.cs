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
    /// - 自动在物体旁边显示3D世界空间UI提示
    /// 
    /// 使用场景：
    /// - 游戏胜利条件（按住F键激活终点）
    /// - 开门/启动机关（需要按住一段时间）
    /// - 拆除炸弹/修理设备等长时间交互
    /// 
    /// 配置步骤：
    /// 1. 添加Collider组件并勾选IsTrigger
    /// 2. 确保玩家GameObject标记为"Player"标签
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

        [Tooltip("玩家标签（用于检测玩家进入触发器）")]
        [SerializeField] private string playerTag = "Player";

        [Header("世界空间UI配置")]
        [Tooltip("是否显示世界空间UI提示")]
        [SerializeField] private bool showWorldUI = true;

        [Tooltip("UI显示位置偏移（相对于物体中心）")]
        [SerializeField] private Vector3 uiOffset = new Vector3(0, 1.5f, 0);

        [Tooltip("交互提示文本（例如'按住F键激活'）")]
        [SerializeField] private string interactPrompt = "按住 F 键激活";

        [Tooltip("UI字体大小")]
        [SerializeField, Range(12, 48)] private int fontSize = 16;

        [Tooltip("进度条宽度（像素）")]
        [SerializeField, Range(50, 300)] private float progressBarWidth = 120f;

        [Tooltip("进度条高度（像素）")]
        [SerializeField, Range(5, 30)] private float progressBarHeight = 12f;

        [Header("UI颜色配置")]
        [Tooltip("提示文本颜色")]
        [SerializeField] private Color textColor = Color.white;

        [Tooltip("进度条背景颜色")]
        [SerializeField] private Color progressBarBackground = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        [Tooltip("进度条填充颜色")]
        [SerializeField] private Color progressBarFill = new Color(0.3f, 0.8f, 1f, 0.9f);

        [Tooltip("完成时的颜色")]
        [SerializeField] private Color completedColor = Color.green;

        [Header("调试配置")]
        [Tooltip("是否显示调试Gizmo")]
        [SerializeField] private bool showDebugGizmo = true;

        [Tooltip("是否显示屏幕调试信息（左上角）")]
        [SerializeField] private bool showDebugInfo = false;

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
        private Camera _mainCamera;
        private GUIStyle _textStyle;
        private GUIStyle _progressBarBgStyle;
        private GUIStyle _progressBarFillStyle;
        private bool _stylesInitialized = false;
        #endregion

        #region Unity生命周期
        private void Awake()
        {
            _interactAction = interactAction?.action;

            // 确保Collider为触发器
            if (TryGetComponent<Collider>(out var col) && !col.isTrigger)
            {
                Debug.LogWarning($"[HoldToInteract] {gameObject.name} 的Collider未设置为Trigger，已自动修正", this);
                col.isTrigger = true;
            }

            // 调试：检查 Input Action
            if (_interactAction == null && interactAction == null)
            {
                Debug.LogWarning($"[HoldToInteract] {gameObject.name} 未分配 Input Action，交互将无法工作！", this);
            }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError($"[HoldToInteract] {gameObject.name} 未找到MainCamera，世界空间UI将无法显示！请确保场景中有标记为'MainCamera'的相机。", this);
            }
            else
            {
                Debug.Log($"[HoldToInteract] {gameObject.name} 相机已找到：{_mainCamera.name}");
            }
        }

        private void OnEnable()
        {
            if (_interactAction is { enabled: false })
            {
                _interactAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_interactAction is { enabled: true })
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
            else if (currentHoldTime > 0f)
            {
                // 松开按键，重置计时器
                currentHoldTime = 0f;
            }
        }
        #endregion

        #region 触发器检测
        private void OnTriggerEnter(Collider other)
        {
            if (isCompleted || !other.CompareTag(playerTag)) return;

            isPlayerInRange = true;
            Debug.Log($"[HoldToInteract] 玩家进入交互区域: {gameObject.name}");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            isPlayerInRange = false;
            currentHoldTime = 0f;
            Debug.Log($"[HoldToInteract] 玩家离开交互区域: {gameObject.name}");
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

        #region 世界空间UI渲染
        private void OnGUI()
        {
            // 调试信息显示（左上角）
            if (showDebugInfo)
            {
                DrawDebugInfo();
            }

            // UI 显示条件检查
            if (!showWorldUI || _mainCamera == null) return;
            if (!isPlayerInRange || isCompleted) return;

            // 初始化样式
            InitializeStyles();

            // 计算世界空间位置
            Vector3 worldPos = transform.position + uiOffset;
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // 检查是否在相机前方
            if (screenPos.z <= 0)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[HoldToInteract] UI在相机后方！screenPos.z = {screenPos.z}");
                }
                return;
            }

            // 转换为GUI坐标（Y轴翻转）
            screenPos.y = Screen.height - screenPos.y;

            // 绘制提示文本
            DrawPromptText(screenPos);

            // 绘制进度条
            if (currentHoldTime > 0f)
            {
                DrawProgressBar(screenPos);
            }
        }

        private void DrawDebugInfo()
        {
            GUIStyle debugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.yellow }
            };

            string debugText = $"=== HoldToInteract Debug ({gameObject.name}) ===\n" +
                              $"Show World UI: {showWorldUI}\n" +
                              $"Main Camera: {(_mainCamera != null ? _mainCamera.name : "NULL")}\n" +
                              $"Player In Range: {isPlayerInRange}\n" +
                              $"Is Completed: {isCompleted}\n" +
                              $"Current Hold Time: {currentHoldTime:F2}s\n" +
                              $"Progress: {Progress:P0}\n" +
                              $"Input Action: {(_interactAction != null ? "OK" : "NULL")}\n" +
                              $"Styles Initialized: {_stylesInitialized}";

            if (_mainCamera != null && isPlayerInRange)
            {
                Vector3 worldPos = transform.position + uiOffset;
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
                debugText += $"\n\nUI World Pos: {worldPos}\n" +
                            $"UI Screen Pos: {screenPos}\n" +
                            $"Screen Size: {Screen.width}x{Screen.height}";
            }

            GUI.Label(new Rect(10, 10, 400, 300), debugText, debugStyle);
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            // 文本样式
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };

            // 进度条背景样式
            _progressBarBgStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, progressBarBackground) }
            };

            // 进度条填充样式
            _progressBarFillStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, progressBarFill) }
            };

            _stylesInitialized = true;
        }

        private void DrawPromptText(Vector3 screenPos)
        {
            // 计算文本内容和大小
            GUIContent content = new GUIContent(interactPrompt);
            Vector2 textSize = _textStyle.CalcSize(content);

            // 绘制文本（带阴影）
            Rect shadowRect = new Rect(
                screenPos.x - textSize.x / 2 + 1, 
                screenPos.y - textSize.y - progressBarHeight - 5 + 1,
                textSize.x, 
                textSize.y
            );
            
            Rect textRect = new Rect(
                screenPos.x - textSize.x / 2, 
                screenPos.y - textSize.y - progressBarHeight - 5,
                textSize.x, 
                textSize.y
            );

            // 绘制阴影
            var oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.Label(shadowRect, content, _textStyle);
            
            // 绘制文本
            GUI.color = oldColor;
            GUI.Label(textRect, content, _textStyle);
        }

        private void DrawProgressBar(Vector3 screenPos)
        {
            float progress = Progress;

            // 进度条背景
            Rect bgRect = new Rect(
                screenPos.x - progressBarWidth / 2,
                screenPos.y - progressBarHeight / 2,
                progressBarWidth,
                progressBarHeight
            );

            // 进度条填充
            Rect fillRect = new Rect(
                bgRect.x + 2,
                bgRect.y + 2,
                (progressBarWidth - 4) * progress,
                progressBarHeight - 4
            );

            // 绘制背景
            GUI.Box(bgRect, GUIContent.none, _progressBarBgStyle);

            // 绘制填充（根据完成状态改变颜色）
            if (progress >= 1f)
            {
                _progressBarFillStyle.normal.background = MakeTex(2, 2, completedColor);
            }
            else
            {
                _progressBarFillStyle.normal.background = MakeTex(2, 2, progressBarFill);
            }

            GUI.Box(fillRect, GUIContent.none, _progressBarFillStyle);

            // 绘制百分比文本
            string percentText = $"{Mathf.RoundToInt(progress * 100)}%";
            GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, fontSize - 4),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Label(bgRect, percentText, percentStyle);
        }

        /// <summary>
        /// 创建纯色纹理（用于进度条）
        /// </summary>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        #endregion

        #region Gizmo调试
        private void OnDrawGizmos()
        {
            if (!showDebugGizmo || !TryGetComponent<Collider>(out var col)) return;

            // 根据状态改变颜色
            Gizmos.color = isCompleted ? Color.green : 
                          isPlayerInRange ? Color.yellow : 
                          Color.cyan;

            // 绘制触发器范围
            switch (col)
            {
                case BoxCollider box:
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                    break;
                case SphereCollider sphere:
                    Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
                    break;
                case CapsuleCollider capsule:
                    Gizmos.DrawWireSphere(transform.position + capsule.center, capsule.radius);
                    break;
            }

            // 绘制UI位置标记
            if (showWorldUI)
            {
                Gizmos.color = Color.white;
                Vector3 uiWorldPos = transform.position + uiOffset;
                Gizmos.DrawWireSphere(uiWorldPos, 0.1f);
                Gizmos.DrawLine(transform.position, uiWorldPos);
            }
        }
        #endregion
    }
}
