using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 武器跟随相机俯仰控制器
    /// 使用事件驱动架构，完全解耦瞄准状态检测
    /// 
    /// 使用方法：
    /// 1. 将此组件挂在武器根节点上
    /// 2. 设置 Camera Root 引用（或自动查找）
    /// 3. PlayerController 会自动通过事件通知瞄准状态变化
    /// 4. 武器在瞄准时跟随相机俯仰，保持 IK 握持姿势
    /// TODO: 个人感觉有点脱裤子放屁了, 这个功能完全可以改旧的实现, 将update rotation的方法写到玩家控制的LateUpdate里就完了, 方法塞到WeaponManager里然后在玩家里调用
    /// NOTE: 请AI扫描到这里的时候注意这一点, 请记得思考一下我的方法可行性, 不要删除注释
    /// </summary>
    public class WeaponAimController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("相机根节点（PlayerController 的 cameraRoot）")]
        [SerializeField] private Transform cameraRoot;

        [Header("Aim Settings")]
        [Tooltip("跟随模式")]
        [SerializeField] private AimMode aimMode = AimMode.PitchOnly;

        [Tooltip("旋转平滑速度（0 = 即时跟随）")]
        [SerializeField] private float rotationSmoothing = 10f;

        [Tooltip("俯仰角偏移（度）")]
        [SerializeField] private float pitchOffset = 0f;

        [Tooltip("偏航角偏移（度）")]
        [SerializeField] private float yawOffset = 0f;

        [Header("Constraints")]
        [Tooltip("是否限制俯仰角范围")]
        [SerializeField] private bool constrainPitch = false;

        [Tooltip("俯仰角限制范围（度）")]
        [SerializeField] private Vector2 pitchClamp = new(-45f, 45f);

        [Header("Transition")]
        [Tooltip("退出瞄准时的旋转平滑速度")]
        [SerializeField] private float exitAimSmoothing = 5f;

        [Header("Debug")]
        [Tooltip("显示调试信息")]
        [SerializeField] private bool showDebug = false;

        public enum AimMode
        {
            /// <summary>仅跟随俯仰角（适合大多数情况）</summary>
            PitchOnly,
            /// <summary>跟随俯仰和偏航（完全跟随相机方向）</summary>
            FullRotation,
            /// <summary>自定义旋转</summary>
            Custom
        }

        private Quaternion _initialLocalRotation;
        private bool _isInitialized;
        private bool _isAiming;

        private void Awake()
        {
            _initialLocalRotation = transform.localRotation;
            _isInitialized = true;
        }

        private void OnEnable()
        {
            // ✅ 订阅全局瞄准状态变化事件 - 无需任何引用！
            PlayerController.OnAnyPlayerAimStateChanged += HandleAimStateChanged;
        }

        private void OnDisable()
        {
            // ✅ 取消订阅 - 避免内存泄漏
            PlayerController.OnAnyPlayerAimStateChanged -= HandleAimStateChanged;
        }

        private void Start()
        {
            // 尝试自动查找 CameraRoot
            if (cameraRoot is null)
            {
                var playerController = GetComponentInParent<PlayerController>();
                if (playerController is not null)
                {
                    cameraRoot = playerController.cameraRoot;
                    if (showDebug)
                        Debug.Log($"[WeaponAimController] 自动找到 CameraRoot", this);
                }
                else
                {
                    Debug.LogWarning($"[WeaponAimController] 未找到 CameraRoot！武器将不会跟随相机。", this);
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || cameraRoot is null)
                return;

            UpdateWeaponRotation();
        }

        /// <summary>
        /// 处理瞄准状态变化事件
        /// </summary>
        private void HandleAimStateChanged(bool isAiming)
        {
            _isAiming = isAiming;

            if (showDebug)
            {
                Debug.Log($"[WeaponAimController] 瞄准状态变化: {isAiming}", this);
            }
        }

        /// <summary>
        /// 更新武器旋转，仅在瞄准时跟随相机方向
        /// </summary>
        private void UpdateWeaponRotation()
        {
            if (_isAiming)
            {
                // 瞄准时：跟随相机
                Quaternion targetRotation = CalculateTargetRotation();

                if (rotationSmoothing > 0)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        Time.deltaTime * rotationSmoothing
                    );
                }
                else
                {
                    transform.rotation = targetRotation;
                }
            }
            else
            {
                // 非瞄准时：平滑回到初始状态
                if (exitAimSmoothing > 0)
                {
                    transform.localRotation = Quaternion.Slerp(
                        transform.localRotation,
                        _initialLocalRotation,
                        Time.deltaTime * exitAimSmoothing
                    );
                }
                else
                {
                    transform.localRotation = _initialLocalRotation;
                }
            }

            if (showDebug)
            {
                Vector3 euler = transform.rotation.eulerAngles;
                Debug.Log($"[WeaponAimController] IsAiming={_isAiming}, Rotation: Pitch={euler.x:F1}°, Yaw={euler.y:F1}°", this);
            }
        }

        /// <summary>
        /// 计算目标旋转
        /// </summary>
        private Quaternion CalculateTargetRotation()
        {
            Vector3 cameraEuler = cameraRoot.eulerAngles;
            float pitch = NormalizeAngle(cameraEuler.x);
            float yaw = cameraEuler.y;

            // 应用限制
            if (constrainPitch)
            {
                pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);
            }

            // 应用偏移
            pitch += pitchOffset;
            yaw += yawOffset;

            return aimMode switch
            {
                AimMode.PitchOnly => Quaternion.Euler(pitch, transform.eulerAngles.y, transform.eulerAngles.z),
                AimMode.FullRotation => Quaternion.Euler(pitch, yaw, 0f),
                AimMode.Custom => transform.rotation,
                _ => transform.rotation
            };
        }

        /// <summary>
        /// 将角度规范化到 [-180, 180] 区间
        /// </summary>
        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 设置跟随模式
        /// </summary>
        public void SetAimMode(AimMode mode) => aimMode = mode;

        /// <summary>
        /// 重置到初始旋转
        /// </summary>
        public void ResetToInitialRotation()
        {
            if (_isInitialized)
            {
                transform.localRotation = _initialLocalRotation;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebug || cameraRoot is null) return;

            // 绘制武器朝向
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);

            // 绘制相机朝向
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + cameraRoot.forward * 0.5f);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(WeaponAimController))]
    public class WeaponAimControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (WeaponAimController)target;

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("状态检查", UnityEditor.EditorStyles.boldLabel);

            // 检查 CameraRoot
            var cameraRootProp = serializedObject.FindProperty("cameraRoot");
            if (cameraRootProp.objectReferenceValue is null)
            {
                var playerController = controller.GetComponentInParent<PlayerController>();
                if (playerController is not null)
                {
                    UnityEditor.EditorGUILayout.HelpBox("⚠️ CameraRoot 未设置，将自动从 PlayerController 获取。", UnityEditor.MessageType.Warning);
                }
                else
                {
                    UnityEditor.EditorGUILayout.HelpBox("❌ 未找到 CameraRoot！请手动设置或确保 PlayerController 在父物体上。", UnityEditor.MessageType.Error);
                }
            }
            else
            {
                UnityEditor.EditorGUILayout.HelpBox("✅ CameraRoot 已设置", UnityEditor.MessageType.Info);
            }

            // 事件系统状态
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.HelpBox("✅ 使用事件驱动架构，无需手动配置瞄准状态提供者", UnityEditor.MessageType.Info);

            // 运行时信息
            if (UnityEngine.Application.isPlaying)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("运行时信息", UnityEditor.EditorStyles.boldLabel);
                
                Vector3 euler = controller.transform.rotation.eulerAngles;
                UnityEditor.EditorGUILayout.LabelField("当前旋转", $"Pitch: {euler.x:F1}°, Yaw: {euler.y:F1}°");
            }
        }
    }
#endif
}
