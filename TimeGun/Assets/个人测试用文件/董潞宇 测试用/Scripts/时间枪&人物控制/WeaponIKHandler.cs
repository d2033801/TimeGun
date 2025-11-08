using UnityEngine;

namespace TimeGun
{
    /// <summary>
    /// 武器 IK 处理器
    /// 将此组件挂在带有 Animator 的角色模型上（通常是 PlayerController 的子物体）
    /// 负责处理手部 IK，使角色自然握持武器
    /// 
    /// 使用方法：
    /// 1. 在带有 Animator (Humanoid) 的 GameObject 上添加此组件
    /// 2. 指定 WeaponManager 引用（如果为空会自动从父物体查找）
    /// 3. 确保 Animator Controller 中对应层的 IK Pass 已勾选
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class WeaponIKHandler : MonoBehaviour
    {
        #region Inspector 参数

        [Header("References")]
        [Tooltip("武器管理器（通常在父物体 PlayerController 上）")]
        [SerializeField] private WeaponManager weaponManager;

        [Header("IK Settings")]
        [Tooltip("是否启用 IK")]
        [SerializeField] private bool enableIk = true;

        [Tooltip("IK 层索引，请确保 Animator Controller 中对应层的 IK Pass 已勾选")]
        [SerializeField] private int ikLayer = 0;

        [Header("IK Weights")]
        [Tooltip("右手 IK 权重")]
        [Range(0, 1)]
        [SerializeField] private float rightHandIkWeight = 1f;

        [Tooltip("左手 IK 权重")]
        [Range(0, 1)]
        [SerializeField] private float leftHandIkWeight = 1f;

        [Tooltip("肘部辅助权重（通常与手部权重相同）")]
        [Range(0, 1)]
        [SerializeField] private float elbowHintWeight = 1f;

        [Header("Debug")]
        [Tooltip("是否显示 Gizmos")]
        [SerializeField] private bool showGizmos = true;

        [Tooltip("是否输出调试日志（每 100 帧一次）")]
        [SerializeField] private bool showDebugLogs = false;

        #endregion

        #region 私有字段

        private Animator _animator;
        private bool _isInitialized;
        private PlayerController _playerController; // 用于检测死亡状态

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            // 尝试自动查找 WeaponManager
            if (weaponManager == null)
            {
                weaponManager = GetComponentInParent<WeaponManager>();
                if (weaponManager != null)
                {
                    Debug.Log($"[WeaponIKHandler] 自动找到 WeaponManager：{weaponManager.gameObject.name}", this);
                }
                else
                {
                    Debug.LogError($"[WeaponIKHandler] 未找到 WeaponManager！请在 Inspector 中手动指定，或确保 WeaponManager 在父物体上。", this);
                }
            }

            // 尝试查找 PlayerController（用于检测死亡状态）
            _playerController = GetComponentInParent<PlayerController>();
        }

        /// <summary>
        /// Unity 回调：处理 IK
        /// 仅在 Humanoid 模型且开启 IK Pass 时被调用
        /// </summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (!_isInitialized)
            {
                LogDebug("IK 系统未初始化", isError: true);
                return;
            }

            if (!enableIk)
            {
                LogDebug("IK 已禁用");
                return;
            }

            // 检查玩家是否死亡
            if (_playerController != null && _playerController.IsDead)
            {
                LogDebug("玩家已死亡，禁用 IK");
                DisableAllIK();
                return;
            }

            if (layerIndex != ikLayer)
            {
                return; // 不是目标 IK 层
            }

            // 获取当前武器
            var currentWeapon = weaponManager?.CurrentWeapon;
            if (currentWeapon == null)
            {
                LogDebug("当前没有武器，禁用 IK");
                DisableAllIK();
                return;
            }

            // 应用 IK
            ApplyHandIK(AvatarIKGoal.RightHand, currentWeapon.rightHandIkTarget, rightHandIkWeight, "右手");
            ApplyHandIK(AvatarIKGoal.LeftHand, currentWeapon.leftHandIkTarget, leftHandIkWeight, "左手");
            ApplyElbowHint(AvatarIKHint.RightElbow, currentWeapon.rightElbowHint, "右肘");
            ApplyElbowHint(AvatarIKHint.LeftElbow, currentWeapon.leftElbowHint, "左肘");
        }

        #endregion

        #region IK 应用

        /// <summary>
        /// 应用手部 IK
        /// </summary>
        private void ApplyHandIK(AvatarIKGoal goal, Transform ikTarget, float weight, string debugName)
        {
            if (ikTarget != null)
            {
                _animator.SetIKPositionWeight(goal, weight);
                _animator.SetIKRotationWeight(goal, weight);
                _animator.SetIKPosition(goal, ikTarget.position);
                _animator.SetIKRotation(goal, ikTarget.rotation);

                if (showDebugLogs && Time.frameCount % 100 == 0)
                {
                    Debug.Log($"[WeaponIKHandler] {debugName} IK 目标位置: {ikTarget.position}, 权重: {weight}", this);
                }
            }
            else
            {
                _animator.SetIKPositionWeight(goal, 0);
                _animator.SetIKRotationWeight(goal, 0);
            }
        }

        /// <summary>
        /// 应用肘部 IK 辅助
        /// </summary>
        private void ApplyElbowHint(AvatarIKHint hint, Transform hintTarget, string debugName)
        {
            if (hintTarget != null)
            {
                _animator.SetIKHintPositionWeight(hint, elbowHintWeight);
                _animator.SetIKHintPosition(hint, hintTarget.position);
            }
            else
            {
                _animator.SetIKHintPositionWeight(hint, 0);
            }
        }

        /// <summary>
        /// 禁用所有 IK
        /// </summary>
        private void DisableAllIK()
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0);
        }

        #endregion

        #region 初始化与验证

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            _animator = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogError($"[WeaponIKHandler] 未找到 Animator 组件！IK 系统无法工作。", this);
                _isInitialized = false;
                return;
            }

            if (!_animator.isHuman)
            {
                Debug.LogError($"[WeaponIKHandler] Animator 不是 Humanoid 类型！OnAnimatorIK 不会被调用。请在模型的 Rig 设置中选择 Humanoid。", this);
                _isInitialized = false;
                return;
            }

            _isInitialized = true;
            Debug.Log($"[WeaponIKHandler] IK 系统初始化成功（Humanoid 模型）", this);
        }

        #endregion

        #region 调试工具

        /// <summary>
        /// 输出调试日志（限制频率）
        /// </summary>
        private void LogDebug(string message, bool isError = false)
        {
            if (!showDebugLogs) return;
            if (Time.frameCount % 100 != 0) return; // 每 100 帧输出一次

            string fullMessage = $"[WeaponIKHandler] {message}";
            if (isError)
                Debug.LogError(fullMessage, this);
            else
                Debug.LogWarning(fullMessage, this);
        }

        /// <summary>
        /// Gizmos 可视化（仅在 Editor 中）
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (weaponManager == null) return;

            var weapon = weaponManager.CurrentWeapon;
            if (weapon == null) return;

            // 右手 IK 目标（蓝色）
            if (weapon.rightHandIkTarget != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(weapon.rightHandIkTarget.position, 0.03f);
                Gizmos.DrawLine(weapon.rightHandIkTarget.position, 
                    weapon.rightHandIkTarget.position + weapon.rightHandIkTarget.forward * 0.1f);
            }

            // 左手 IK 目标（绿色）
            if (weapon.leftHandIkTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(weapon.leftHandIkTarget.position, 0.03f);
                Gizmos.DrawLine(weapon.leftHandIkTarget.position, 
                    weapon.leftHandIkTarget.position + weapon.leftHandIkTarget.forward * 0.1f);
            }

            // 右肘辅助点（青色）
            if (weapon.rightElbowHint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(weapon.rightElbowHint.position, 0.02f);
            }

            // 左肘辅助点（洋红色）
            if (weapon.leftElbowHint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(weapon.leftElbowHint.position, 0.02f);
            }

            // 绘制 Animator 根节点
            if (_animator != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_animator.transform.position, 0.1f);
            }

            // 绘制实际手部骨骼位置
            if (_animator != null && _animator.isHuman && Application.isPlaying)
            {
                Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
                
                if (rightHand != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(rightHand.position, 0.05f);
                    // 从 IK 目标到实际手部的连线
                    if (weapon.rightHandIkTarget != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(weapon.rightHandIkTarget.position, rightHand.position);
                    }
                }
                
                if (leftHand != null)
                {
                    Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // 半透明洋红
                    Gizmos.DrawWireSphere(leftHand.position, 0.05f);
                    if (weapon.leftHandIkTarget != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(weapon.leftHandIkTarget.position, leftHand.position);
                    }
                }
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 启用 IK
        /// </summary>
        public void EnableIK()
        {
            enableIk = true;
        }

        /// <summary>
        /// 禁用 IK
        /// </summary>
        public void DisableIK()
        {
            enableIk = false;
            if (_animator != null)
            {
                DisableAllIK();
            }
        }

        /// <summary>
        /// 设置 IK 权重
        /// </summary>
        public void SetIKWeights(float rightHand, float leftHand, float elbowHint = -1)
        {
            rightHandIkWeight = Mathf.Clamp01(rightHand);
            leftHandIkWeight = Mathf.Clamp01(leftHand);
            if (elbowHint >= 0)
            {
                elbowHintWeight = Mathf.Clamp01(elbowHint);
            }
        }

        #endregion
    }

#if UNITY_EDITOR
    /// <summary>
    /// 自定义 Inspector 编辑器
    /// </summary>
    [UnityEditor.CustomEditor(typeof(WeaponIKHandler))]
    public class WeaponIKHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var handler = (WeaponIKHandler)target;

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("IK 系统状态检查", UnityEditor.EditorStyles.boldLabel);

            // 检查 Animator
            var animator = handler.GetComponent<Animator>();
            if (animator == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("❌ 未找到 Animator 组件！", UnityEditor.MessageType.Error);
            }
            else if (!animator.isHuman)
            {
                UnityEditor.EditorGUILayout.HelpBox("❌ Animator 不是 Humanoid 类型！请在模型 Rig 设置中选择 Humanoid。", UnityEditor.MessageType.Error);
            }
            else
            {
                UnityEditor.EditorGUILayout.HelpBox("✅ Animator 配置正确（Humanoid）", UnityEditor.MessageType.Info);
            }

            // 检查 WeaponManager
            var weaponManager = handler.GetComponent<WeaponManager>() ?? handler.GetComponentInParent<WeaponManager>();
            if (weaponManager == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("⚠️ 未找到 WeaponManager！请在父物体上添加或手动指定。", UnityEditor.MessageType.Warning);
            }
            else
            {
                UnityEditor.EditorGUILayout.HelpBox($"✅ 找到 WeaponManager：{weaponManager.gameObject.name}", UnityEditor.MessageType.Info);
                
                // 运行时显示当前武器
                if (Application.isPlaying && weaponManager.CurrentWeapon != null)
                {
                    var weapon = weaponManager.CurrentWeapon;
                    UnityEditor.EditorGUILayout.Space();
                    UnityEditor.EditorGUILayout.LabelField("当前武器", weapon.name);
                    UnityEditor.EditorGUILayout.LabelField("右手 IK", weapon.rightHandIkTarget != null ? "✅ 已设置" : "❌ 未设置");
                    UnityEditor.EditorGUILayout.LabelField("左手 IK", weapon.leftHandIkTarget != null ? "✅ 已设置" : "❌ 未设置");
                }
            }

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.HelpBox("提醒：请确保 Animator Controller 中对应层的 'IK Pass' 已勾选。", UnityEditor.MessageType.Info);
            
            // 快速设置按钮
            UnityEditor.EditorGUILayout.Space();
            if (UnityEditor.EditorGUILayout.LinkButton("📖 查看 IK 配置指南"))
            {
                Application.OpenURL("https://docs.unity3d.com/Manual/InverseKinematics.html");
            }
        }
    }
#endif
}
