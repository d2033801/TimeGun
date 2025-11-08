using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace TimeGun
{
    /// <summary>
    /// 简单的第三人称/第一人称混合角色控制器（使用 Unity 新输入系统）
    /// - 基于摄像机方向移动与旋转
    /// - 支持冲刺、跳跃、蹲下（保持或切换）、瞄准状态
    /// - 使用 CharacterController 处理碰撞与重力
    /// - Cinemachine 3.1.5：建议将 cameraRoot 作为虚拟机位的 Follow/LookAt 目标；
    ///   若由本脚本驱动视角，请在 Cinemachine 中禁用其自身输入，避免相互争抢旋转。
    /// 
    /// 注意：IK 功能已迁移到 WeaponIKHandler 组件，请将其挂在带有 Animator 的模型上。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(WeaponManager))]
    [AddComponentMenu("TimeGun/Player Control")]
    public class PlayerController : MonoBehaviour
    {
        #region 全局事件（事件驱动架构）

        /// <summary>
        /// 全局瞄准状态变化事件（任何 PlayerController 的瞄准状态变化都会触发）
        /// 参数：bool isAiming - 新的瞄准状态
        /// </summary>
        public static event Action<bool> OnAnyPlayerAimStateChanged;

        #endregion

        #region 检视器参数（Inspector）

        [Header("References")]
        [Tooltip("计算射击目标的相机, 通常用主相机")]
        public Camera shootCamera;
        public Transform cameraRoot;
        [SerializeField, Tooltip("武器管理器, 正常情况下挂载在角色身上")] private WeaponManager weaponManager;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 7.5f;
        public float rotationSpeed = 12f;
        public float gravity = -20f;
        [Tooltip("是否允许跳跃")]
        public bool enableJump = true;
        public float jumpHeight = 1.2f;

        [Header("Crouch")]
        public bool holdToCrouch = false;
        public float standHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public float heightLerpSpeed = 12f;
        public LayerMask ceilingMask = ~0;
        [Tooltip("站立状态时 CharacterController.center.y")]
        public float standCenterY = 0.0f;
        [Tooltip("蹲下状态时 CharacterController.center.y")]
        public float crouchCenterY = -0.3f;

        [Header("摄像机控制")]
        [Tooltip("鼠标灵敏度")] public float mouseSensitivity = 0.1f;
        [Tooltip("手柄灵敏度")] public float gamepadSensitivity = 120f;
        [Tooltip("反转Y轴")] public bool invertY = false;
        [Tooltip("俯仰角限制范围")] public Vector2 pitchClamp = new Vector2(-40f, 80f);

        [Header("Input (New Input System)")]
        [Tooltip("移动输入（Vector2）。通常映射为 WASD / 左摇杆，x: 右/左, y: 前/后")]
        public InputActionReference moveAction;
        [Tooltip("跳跃按键。按下触发（仅在地面且未蹲下时生效）")]
        public InputActionReference jumpAction;
        [Tooltip("冲刺按键。按住并有移动输入时生效，增加移动速度")]
        public InputActionReference sprintAction;
        [Tooltip("蹲下/站立。根据 holdToCrouch 决定为按住（Hold）或切换（Toggle）")]
        public InputActionReference crouchAction;
        [Tooltip("瞄准（ADS）按键。按住进入瞄准状态，会影响朝向与射击行为")]
        public InputActionReference aimAction;
        [Tooltip("视角输入（鼠标 / 右摇杆）。x: 偏航 (yaw), y: 俯仰 (pitch)")]
        public InputActionReference lookAction;
        [Tooltip("投掷/发射榴弹输入。触发时由 WeaponManager 处理投掷逻辑")]
        public InputActionReference grenadeLaunchAction;
        [Tooltip("开火输入。触发当前武器的 Fire()（短按/长按行为由武器实现）")]
        public InputActionReference fireAction;

        [Header("Death Settings")]
        [Tooltip("死亡后延迟重生时间(秒), 0表示不自动重生")]
        [SerializeField] private float respawnDelay = 0f;
        [Tooltip("重生点Transform(如果为null则在当前位置重生)")]
        [SerializeField] private Transform respawnPoint;

        [Header("Death Events")]
        [Tooltip("死亡时触发的事件(可绑定UI/音效/特效)")]
        public UnityEvent OnDeath;
        [Tooltip("重生时触发的事件")]
        public UnityEvent OnRespawn;

        [Header("Animation")]
        [Tooltip("动画控制器（通常在子物体上）")]
        private Animator animator;

        #endregion

        #region 状态属性与私有字段

        // 瞄准状态 - 使用属性自动触发事件
        private bool _isAiming;
        public bool IsAiming
        {
            get => _isAiming;
            private set
            {
                if (_isAiming != value)
                {
                    _isAiming = value;
                    // 触发全局事件
                    OnAnyPlayerAimStateChanged?.Invoke(_isAiming);
                }
            }
        }

        public bool IsCrouching { get; private set; }
        public bool IsDead { get; private set; }

        // 组件与运行时状态
        private CharacterController _characterController;
        private float _verticalVelocity;
        private const float _cameraRootVerticalOffset = 0.3f;
        private Transform _shootCameraTransform;
        private int _maxShootPointRange = 100;

        // 相机旋转相关
        private float _cameraYaw;
        private float _cameraPitch;

        // 顶部检测的临时缓存
        private readonly RaycastHit[] _ceilingHits = new RaycastHit[2];

        // 缓存后的输入动作
        private InputAction _move, _jump, _sprint, _crouch, _aim, _look, _grendeLaunch, _fire;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            _move = moveAction ? moveAction.action : null;
            _jump = jumpAction ? jumpAction.action : null;
            _sprint = sprintAction ? sprintAction.action : null;
            _crouch = crouchAction ? crouchAction.action : null;
            _aim = aimAction ? aimAction.action : null;
            _look = lookAction ? lookAction.action : null;
            _grendeLaunch = grenadeLaunchAction ? grenadeLaunchAction.action : null;
            _fire = fireAction ? fireAction.action : null;

            if (shootCamera == null) shootCamera = Camera.main;
            _shootCameraTransform = shootCamera?.transform ?? transform;

            ApplyControllerDimensionsInstant();
            weaponManager = weaponManager ? weaponManager : GetComponent<WeaponManager>();

            if (cameraRoot != null)
            {
                _cameraYaw = transform.eulerAngles.y;
                cameraRoot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
            }
        }

        private void Start()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        private void OnEnable()
        {
            EnableAction(_move);
            EnableAction(_jump);
            EnableAction(_sprint);
            EnableAction(_crouch);
            EnableAction(_aim);
            EnableAction(_look);
            EnableAction(_grendeLaunch);
            EnableAction(_fire);
        }

        private void OnDisable()
        {
            DisableAction(_move);
            DisableAction(_jump);
            DisableAction(_sprint);
            DisableAction(_crouch);
            DisableAction(_aim);
            DisableAction(_look);
            DisableAction(_grendeLaunch);
            DisableAction(_fire);
        }

        private void Update()
        {
            if (IsDead) return;

            float dt = Time.deltaTime;
            Vector2 move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            bool hasMoveInput = move.sqrMagnitude > 0.0001f;
            bool wantsSprint = _sprint != null && _sprint.IsPressed() && hasMoveInput;
            bool crouchPressed = _crouch != null && _crouch.WasPressedThisFrame();
            bool crouchHeld = _crouch != null && _crouch.IsPressed();
            
            // 使用属性设置，自动触发事件
            IsAiming = _aim != null && _aim.IsPressed();

            HandleCrouch(crouchPressed, crouchHeld, dt);
            HandleMovement(move, wantsSprint, dt);
            HandleRotation(move, dt);
            HandleWeapon();

            UpdateAnimator();
        }

        private void LateUpdate()
        {
            if (IsDead) return;

            Vector2 lookDelta = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
            float dt = Time.deltaTime;
            CameraRootRot(lookDelta, dt);
        }

        #endregion

        #region 输入辅助

        private static void EnableAction(InputAction a)
        {
            if (a != null && !a.enabled) a.Enable();
        }

        private static void DisableAction(InputAction a)
        {
            if (a != null && a.enabled) a.Disable();
        }

        #endregion

        #region 角色移动、姿态、射击

        private void HandleCrouch(bool crouchPressed, bool crouchHeld, float dt)
        {
            bool desiredCrouch = holdToCrouch ? crouchHeld : (crouchPressed ? !IsCrouching : IsCrouching);

            if (IsCrouching && !desiredCrouch)
            {
                if (!CanStandUp())
                    desiredCrouch = true;
            }

            IsCrouching = desiredCrouch;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            float targetCenterY = IsCrouching ? crouchCenterY : standCenterY;

            _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, dt * heightLerpSpeed);

            Vector3 center = _characterController.center;
            center.y = Mathf.Lerp(center.y, targetCenterY, dt * heightLerpSpeed);
            _characterController.center = center;

            Vector3 camLocal = center;
            camLocal.y += IsCrouching ? 0f : _cameraRootVerticalOffset;
            ChangeCameraRootPosition(camLocal);
        }

        private void HandleMovement(Vector2 move, bool wantsSprint, float dt)
        {
            Vector3 camForward = _shootCameraTransform.forward;
            Vector3 camRight = _shootCameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 inputWorld = camForward * move.y + camRight * move.x;
            if (inputWorld.sqrMagnitude > 1f)
                inputWorld.Normalize();

            float speed = IsCrouching ? moveSpeed * 0.6f : (wantsSprint ? sprintSpeed : moveSpeed);
            Vector3 horizontalVel = inputWorld * speed;

            if (_characterController.isGrounded)
            {
                _verticalVelocity = -2f;

                if (enableJump && _jump != null && _jump.WasPressedThisFrame() && !IsCrouching)
                {
                    _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                }
            }
            else
            {
                _verticalVelocity += gravity * dt;
            }

            Vector3 total = horizontalVel + Vector3.up * _verticalVelocity;
            _characterController.Move(total * dt);
        }

        private void HandleRotation(Vector2 move, float dt)
        {
            if (IsAiming)
            {
                Quaternion targetRot = Quaternion.Euler(0f, _cameraYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * rotationSpeed);
            }
            else if (move.sqrMagnitude > 0.001f)
            {
                Vector3 forward = cameraRoot ? cameraRoot.forward : transform.forward;
                Vector3 right = cameraRoot ? cameraRoot.right : transform.right;
                Vector3 direction = forward * move.y + right * move.x;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    transform.forward = Vector3.Slerp(transform.forward, direction, dt * rotationSpeed);
                }
            }
        }

        private void HandleWeapon()
        {
            if (!IsAiming) return;

            var target = ShootTargetPoint;

            if (_fire?.IsPressed() == true)
                weaponManager.TryFireWeapon(target);

            if (_grendeLaunch?.IsPressed() == true)
                weaponManager.TryThrow(target);
        }

        private void ApplyControllerDimensionsInstant()
        {
            _characterController.height = standHeight;

            Vector3 center = _characterController.center;
            center.y = standCenterY;
            _characterController.center = center;

            Vector3 camLocal = center;
            camLocal.y += _cameraRootVerticalOffset;
            ChangeCameraRootPosition(camLocal);
        }

        #endregion

        #region 碰撞与检测

        private bool CanStandUp()
        {
            if (!_characterController) return false;

            const float clearance = 0.15f;
            float halfHeight = standHeight * 0.5f;
            Vector3 centerWorld = transform.TransformPoint(new Vector3(_characterController.center.x, standCenterY, _characterController.center.z));
            Vector3 headTop = centerWorld + Vector3.up * halfHeight;
            Vector3 origin = headTop + Vector3.down * 0.02f;
            float sphereRadius = Mathf.Max(0.05f, _characterController.radius * 0.9f);

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                sphereRadius,
                Vector3.up,
                _ceilingHits,
                clearance + 0.02f,
                ceilingMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _ceilingHits[i];
                if (!hit.collider) continue;

                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                return false;
            }

            return true;
        }

        private Vector3 ShootTargetPoint
        {
            get
            {
                var origin = _shootCameraTransform?.position ?? transform.position;
                var dir = _shootCameraTransform?.forward ?? transform.forward;

                if (Physics.Raycast(origin, dir, out var hit, _maxShootPointRange, ~0, QueryTriggerInteraction.Ignore))
                {
                    return hit.point;
                }
                return origin + dir * _maxShootPointRange;
            }
        }

        #endregion

        #region CameraRoot 工具

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        private void ChangeCameraRootPosition(Vector3 target)
        {
            if (cameraRoot != null)
            {
                cameraRoot.localPosition = target;
            }
        }

        private void CameraRootRot(Vector2 lookDelta, float dt)
        {
            if (cameraRoot == null) return;

            var device = _look?.activeControl?.device;
            bool isMouse = device is UnityEngine.InputSystem.Mouse;

            float sens = isMouse ? mouseSensitivity : gamepadSensitivity * dt;
            float ySign = invertY ? 1f : -1f;

            _cameraYaw += lookDelta.x * sens;
            _cameraYaw = NormalizeAngle(_cameraYaw);

            _cameraPitch += lookDelta.y * ySign * sens;
            _cameraPitch = Mathf.Clamp(_cameraPitch, pitchClamp.x, pitchClamp.y);

            cameraRoot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        }

        #endregion

        #region 死亡系统

        public void Die()
        {
            if (IsDead) return;

            IsDead = true;

            if (_characterController != null)
                _characterController.enabled = false;

            OnDeath?.Invoke();

            Debug.Log("[PlayerController] 玩家死亡");

            if (respawnDelay > 0)
            {
                Invoke(nameof(Respawn), respawnDelay);
            }
        }

        public void Respawn()
        {
            IsDead = false;

            if (respawnPoint != null)
            {
                transform.position = respawnPoint.position;
                transform.rotation = respawnPoint.rotation;
            }

            _verticalVelocity = 0f;

            if (_characterController != null)
                _characterController.enabled = true;

            OnRespawn?.Invoke();

            Debug.Log("[PlayerController] 玩家重生");
        }

        #endregion

        #region 动画更新

        private void UpdateAnimator()
        {
            if (animator == null) return;

            animator.SetFloat("MoveX", _characterController.velocity.x);
            animator.SetFloat("MoveZ", _characterController.velocity.z);
            animator.SetFloat("Speed", _characterController.velocity.magnitude);
            animator.SetBool("isCrouching", IsCrouching);
        }

        #endregion
    }
}