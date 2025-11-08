using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace TimeGun
{
    /// <summary>
    /// 智能门控制器 - 使用 Unity 6 新特性
    /// 功能：
    /// 1. 检测范围内活着的 Enemy
    /// 2. 自动开门/关门（支持动画和程序化控制）
    /// 3. 延迟关门机制
    /// 4. 支持多种检测模式（触发器/球形范围）
    /// 5. 可选的 Player 检测（调试模式）
    /// </summary>
    public class SmartDoorController : MonoBehaviour
    {
        #region Inspector 参数

        [Header("门对象引用")]
        [Tooltip("门的Transform（如果使用程序化旋转）或带Animator的GameObject")]
        [SerializeField] private Transform doorTransform;

        [Tooltip("门的Animator组件（如果使用动画控制）")]
        [SerializeField] private Animator doorAnimator;

        [Header("检测设置")]
        [Tooltip("检测模式：触发器模式需要在门上添加Trigger碰撞体")]
        [SerializeField] private DetectionMode detectionMode = DetectionMode.SphereCast;

        [Tooltip("球形检测半径（仅在SphereCast模式下生效）")]
        [SerializeField] private float detectionRadius = 5f;

        [Tooltip("检测中心点偏移（相对于门的位置）")]
        [SerializeField] private Vector3 detectionCenterOffset = Vector3.zero;

        [Tooltip("检测的层级遮罩（应包含Enemy所在层）")]
        [SerializeField] private LayerMask detectionLayer = ~0;

        [Header("门控制设置")]
        [Tooltip("控制模式：动画或程序化旋转")]
        [SerializeField] private DoorControlMode controlMode = DoorControlMode.Animation;

        [Tooltip("动画参数名称（Bool类型，true=开门，false=关门）")]
        [SerializeField] private string animationParameterName = "IsOpen";

        [Tooltip("程序化旋转：门轴向（通常是Y轴）")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        [Tooltip("程序化旋转：开门角度")]
        [SerializeField] private float openAngle = 90f;

        [Tooltip("程序化旋转：旋转速度")]
        [SerializeField] private float rotationSpeed = 90f;

        [Header("时间设置")]
        [Tooltip("Enemy/Player离开后多久自动关门（秒）")]
        [SerializeField] private float autoCloseDelay = 3f;

        [Tooltip("检测更新频率（秒），降低可提升性能")]
        [SerializeField, Range(0.05f, 1f)] private float detectionInterval = 0.2f;

        [Header("调试选项")]
        [Tooltip("调试模式：开启后也会检测 Player（通过 Tag 'Player'）")]
        [SerializeField] private bool debugModeDetectPlayer = false;

        [Tooltip("是否在Scene视图中绘制检测范围")]
        [SerializeField] private bool showDebugGizmos = true;

        [Tooltip("是否在Console中输出调试日志")]
        [SerializeField] private bool enableDebugLog = false;

        #endregion

        #region 私有字段

        private bool _isDoorOpen = false;                    // 门当前是否处于开启状态
        private float _autoCloseTimer = 0f;                  // 自动关门计时器
        private float _detectionTimer = 0f;                  // 检测间隔计时器
        private readonly HashSet<Enemy> _enemiesInRange = new(); // 范围内的Enemy（使用HashSet避免重复）
        private bool _playerInRange = false;                 // 玩家是否在范围内（调试模式）
        private Quaternion _closedRotation;                  // 门关闭时的旋转
        private Quaternion _openRotation;                    // 门开启时的旋转
        private readonly Collider[] _detectionResults = new Collider[100]; // 球形检测结果缓存（避免GC）
        private NavMeshObstacle obstacle;

        #endregion

        #region 枚举定义

        /// <summary>检测模式</summary>
        public enum DetectionMode
        {
            /// <summary>使用触发器碰撞体检测（需要在门上添加Trigger）</summary>
            Trigger,
            /// <summary>使用球形范围检测（通过OverlapSphere）</summary>
            SphereCast
        }

        /// <summary>门控制模式</summary>
        public enum DoorControlMode
        {
            /// <summary>使用Animator动画控制</summary>
            Animation,
            /// <summary>使用程序化旋转控制</summary>
            ProceduralRotation
        }

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                Debug.Log("obstacle is not initialized");
            }
            else
            {
                Debug.Log("obstacle is initialized");
            }
            

            // 初始化验证
            if (doorTransform == null)
            {
                doorTransform = transform;
                Debug.LogWarning($"[SmartDoor] 未指定doorTransform，使用自身Transform");
            }

            if (controlMode == DoorControlMode.Animation && doorAnimator == null)
            {
                doorAnimator = doorTransform.GetComponent<Animator>();
                if (doorAnimator == null)
                {
                    Debug.LogError($"[SmartDoor] 动画模式下未找到Animator组件！");
                }
            }

            // 初始化程序化旋转参数
            if (controlMode == DoorControlMode.ProceduralRotation)
            {
                _closedRotation = doorTransform.localRotation;
                _openRotation = _closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);
            }

            // 调试模式提示
            if (debugModeDetectPlayer && enableDebugLog)
            {
                Debug.Log($"[SmartDoor] 调试模式已启用，门会同时检测 Enemy 和 Player");
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 间隔检测（降低性能开销）
            if (detectionMode == DetectionMode.SphereCast)
            {
                _detectionTimer += dt;
                if (_detectionTimer >= detectionInterval)
                {
                    _detectionTimer = 0f;
                    UpdateSphereDetection();
                }
            }

            // 判断是否有活着的Enemy或Player
            bool shouldOpen = HasAliveEnemyInRange() || (debugModeDetectPlayer && _playerInRange);

            // 开门逻辑
            if (shouldOpen)
            {
                if (!_isDoorOpen)
                {
                    OpenDoor();
                }
                _autoCloseTimer = 0f; // 重置关门计时器
            }
            // 关门逻辑（延迟）
            else if (_isDoorOpen)
            {
                _autoCloseTimer += dt;
                if (_autoCloseTimer >= autoCloseDelay)
                {
                    CloseDoor();
                    _autoCloseTimer = 0f;
                }
            }

            // 程序化旋转更新
            if (controlMode == DoorControlMode.ProceduralRotation)
            {
                UpdateProceduralRotation(dt);
            }
        }

        #endregion

        #region 检测逻辑

        /// <summary>
        /// 更新球形范围检测（仅在SphereCast模式下调用）
        /// </summary>
        private void UpdateSphereDetection()
        {
            Vector3 center = transform.position + transform.TransformDirection(detectionCenterOffset);

            // 使用NonAlloc避免GC
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                detectionRadius,
                _detectionResults,
                detectionLayer,
                QueryTriggerInteraction.Ignore
            );

            // 清空并重新收集
            _enemiesInRange.Clear();
            _playerInRange = false;

            for (int i = 0; i < hitCount; i++)
            {
                var collider = _detectionResults[i];

                // 检测 Enemy
                var enemy = collider.GetComponent<Enemy>();
                if (enemy != null)
                {
                    _enemiesInRange.Add(enemy);
                    continue;
                }

                // 调试模式：检测 Player（通过 Tag）
                if (debugModeDetectPlayer && collider.CompareTag("Player"))
                {
                    _playerInRange = true;
                    if (enableDebugLog)
                    {
                        Debug.Log($"[SmartDoor] 检测到 Player: {collider.name}");
                    }
                }
            }

            if (enableDebugLog)
            {
                Debug.Log($"[SmartDoor] 检测到 {_enemiesInRange.Count} 个Enemy" +
                    (debugModeDetectPlayer && _playerInRange ? " + 1 个Player" : ""));
            }
        }

        /// <summary>
        /// 检查范围内是否有活着的Enemy
        /// </summary>
        private bool HasAliveEnemyInRange()
        {
            // 使用 RemoveWhere 清除已销毁的Enemy（避免空引用）
            _enemiesInRange.RemoveWhere(e => e == null);

            foreach (var enemy in _enemiesInRange)
            {
                // 检查Enemy是否激活、组件启用、且未死亡
                if (enemy.gameObject.activeInHierarchy &&
                    enemy.enabled &&
                    !enemy.IsDead)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region 门控制逻辑

        /// <summary>
        /// 开门
        /// </summary>
        private void OpenDoor()
        {
            _isDoorOpen = true;

            if (controlMode == DoorControlMode.Animation && doorAnimator != null)
            {
                doorAnimator.SetBool(animationParameterName, true);
            }

            if (enableDebugLog)
            {
                string reason = _playerInRange && debugModeDetectPlayer ? "(Player触发)" : "(Enemy触发)";
                Debug.Log($"[SmartDoor] 门已开启 {reason}");
            }

            if (obstacle != null)
                obstacle.carving = !_isDoorOpen;
        }

        /// <summary>
        /// 关门
        /// </summary>
        private void CloseDoor()
        {
            _isDoorOpen = false;

            if (controlMode == DoorControlMode.Animation && doorAnimator != null)
            {
                doorAnimator.SetBool(animationParameterName, false);
            }

            if (enableDebugLog)
            {
                Debug.Log($"[SmartDoor] 门已关闭");
            }

            if (obstacle != null)
                obstacle.carving = !_isDoorOpen;
        }

        /// <summary>
        /// 更新程序化旋转（平滑插值）
        /// </summary>
        private void UpdateProceduralRotation(float dt)
        {
            Quaternion targetRotation = _isDoorOpen ? _openRotation : _closedRotation;
            doorTransform.localRotation = Quaternion.RotateTowards(
                doorTransform.localRotation,
                targetRotation,
                rotationSpeed * dt
            );
        }

        #endregion

        #region 触发器模式回调（需要在门上添加Trigger碰撞体）

        private void OnTriggerEnter(Collider other)
        {
            if (detectionMode != DetectionMode.Trigger) return;

            // 检测 Enemy
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                _enemiesInRange.Add(enemy);
                if (enableDebugLog)
                {
                    Debug.Log($"[SmartDoor] Enemy进入范围: {enemy.name}");
                }
                return;
            }

            // 调试模式：检测 Player
            if (debugModeDetectPlayer && other.CompareTag("Player"))
            {
                _playerInRange = true;
                if (enableDebugLog)
                {
                    Debug.Log($"[SmartDoor] Player进入范围: {other.name}");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (detectionMode != DetectionMode.Trigger) return;

            // Enemy 离开
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                _enemiesInRange.Remove(enemy);
                if (enableDebugLog)
                {
                    Debug.Log($"[SmartDoor] Enemy离开范围: {enemy.name}");
                }
                return;
            }

            // 调试模式：Player 离开
            if (debugModeDetectPlayer && other.CompareTag("Player"))
            {
                _playerInRange = false;
                if (enableDebugLog)
                {
                    Debug.Log($"[SmartDoor] Player离开范围: {other.name}");
                }
            }
        }

        #endregion

        #region 调试可视化

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            Vector3 center = transform.position + transform.TransformDirection(detectionCenterOffset);

            // 绘制检测范围
            Gizmos.color = _isDoorOpen ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(center, detectionRadius);

            // 调试模式：检测范围变为蓝色（表示同时检测 Player）
            if (debugModeDetectPlayer)
            {
                Gizmos.color = _isDoorOpen ? Color.cyan : Color.blue;
                Gizmos.DrawWireSphere(center, detectionRadius * 1.02f); // 略大一圈区分
            }

            // 绘制中心点
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(center, 0.1f);

            // 绘制门的开启方向（程序化模式）
            if (controlMode == DoorControlMode.ProceduralRotation)
            {
                Gizmos.color = Color.cyan;
                Vector3 direction = Quaternion.AngleAxis(openAngle, rotationAxis) * transform.forward;
                Gizmos.DrawRay(doorTransform.position, direction * 2f);
            }

            // 调试模式：显示当前检测状态
            if (Application.isPlaying && debugModeDetectPlayer)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.color = _playerInRange ? Color.green : Color.gray;
                UnityEditor.Handles.Label(
                    center + Vector3.up * (detectionRadius + 0.5f),
                    $"Player: {(_playerInRange ? "在范围内" : "不在范围")}\n" +
                    $"Enemy: {_enemiesInRange.Count} 个\n" +
                    $"门状态: {(_isDoorOpen ? "开启" : "关闭")}"
                );
#endif
            }
        }

        #endregion

        #region 公开API（可选：供外部调用）

        /// <summary>
        /// 手动强制开门（忽略检测）
        /// </summary>
        public void ForceOpen()
        {
            OpenDoor();
        }

        /// <summary>
        /// 手动强制关门（忽略检测）
        /// </summary>
        public void ForceClose()
        {
            CloseDoor();
        }

        /// <summary>
        /// 获取当前门的状态
        /// </summary>
        public bool IsDoorOpen => _isDoorOpen;

        /// <summary>
        /// 设置调试模式（运行时动态切换）
        /// </summary>
        /// <param name="enabled">是否启用Player检测</param>
        public void SetDebugMode(bool enabled)
        {
            debugModeDetectPlayer = enabled;
            if (enableDebugLog)
            {
                Debug.Log($"[SmartDoor] 调试模式 {(enabled ? "已启用" : "已禁用")}");
            }
        }

        #endregion
    }
}