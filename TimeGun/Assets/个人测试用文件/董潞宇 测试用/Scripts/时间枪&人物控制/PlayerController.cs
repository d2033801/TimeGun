using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeGun
{
    /// <summary>
    /// 简单的第三人称/第一人称混合角色控制器（使用 Unity 新输入系统）
    /// - 基于摄像机方向移动与旋转
    /// - 支持冲刺、跳跃、蹲下（保持或切换）、瞄准状态
    /// - 使用 CharacterController 处理碰撞与重力
    /// - Cinemachine 3.1.5：建议将 cameraRoot 作为虚拟机位的 Follow/LookAt 目标；
    ///   若由本脚本驱动视角，请在 Cinemachine 中禁用其自身输入，避免相互争抢旋转。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(WeaponManager))]
    [AddComponentMenu("TimeGun/Player Control")]
    public class PlayerController : MonoBehaviour
    {
        #region 检视器参数（Inspector）

        [Header("References")]
        [Tooltip("计算射击目标的相机, 通常用主相机")]
        public Camera shootCamera; // 通常使用 Camera.main
        public Transform cameraRoot;      // Cinemachine Follow/LookAt 目标（挂在角色身上：胸口/头部附近）
        [SerializeField,Tooltip("武器管理器, 正常情况下挂载在角色身上")] private WeaponManager weaponManager;

        [Header("Movement")]
        public float moveSpeed = 5f;      // 普通移动速度（m/s）
        public float sprintSpeed = 7.5f;  // 冲刺速度（m/s）
        public float rotationSpeed = 12f; // 旋转插值速度（越大越快）
        public float gravity = -20f;      // 重力加速度（负数，m/s^2）
        public float jumpHeight = 1.2f;   // 跳跃高度（m）

        /// <summary>蹲下相关</summary>
        [Header("Crouch")]
        public bool holdToCrouch = false;   // true：按住才蹲下；false：按一下切换
        public float standHeight = 1.8f;    // 站立时 CharacterController.height
        public float crouchHeight = 1.2f;   // 蹲下时 CharacterController.height
        public float heightLerpSpeed = 12f; // 高度/中心插值速度（平滑过渡）
        public LayerMask ceilingMask = ~0;  // 顶部检测的层（用于检查能否起身）

        [Tooltip("站立状态时 CharacterController.center.y")]
        public float standCenterY = 0.0f;

        [Tooltip("蹲下状态时 CharacterController.center.y")]
        public float crouchCenterY = -0.3f;

        [Header("摄像机控制")]
        [Tooltip("鼠标灵敏度")] public float mouseSensitivity = 0.1f;        // 鼠标灵敏度（像素增量/帧）
        [Tooltip("手柄灵敏度")] public float gamepadSensitivity = 120f;      // 手柄灵敏度（度/秒）
        [Tooltip("反转Y轴")] public bool invertY = false;                     // 是否反转Y
        [Tooltip("俯仰角限制范围")] public Vector2 pitchClamp = new Vector2(-40f, 80f); // 俯仰夹角

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

        #endregion

        #region 状态属性与私有字段

        // 对外状态
        public bool IsAiming { get; private set; }
        public bool IsCrouching { get; private set; }

        // 组件与运行时状态
        private CharacterController _characterController;
        private float _verticalVelocity;                       // y 方向速度（用于重力与跳跃）
        private const float _cameraRootVerticalOffset = 0.3f;  // 摄像机朝向根节点的站立状态额外垂直偏移
        private Transform _shootCameraTransform; // 通常使用 Camera.main.transform
        private int _maxShootPointRange = 100;                 // 最大射击点距离, 用于射线检测, 超出这个距离则改为瞄准相机正前方

        // 相机旋转相关（记录 cameraRoot 的世界旋转角）
        private float _cameraYaw;       // 世界坐标下的相机偏航角
        private float _cameraPitch;     // 世界坐标下的相机俯仰角

        // 顶部检测的临时缓存，使用 NonAlloc API 避免 GC
        private readonly RaycastHit[] _ceilingHits = new RaycastHit[2];

        // 缓存后的输入动作（避免每帧从 Reference 间接取用）
        private InputAction _move, _jump, _sprint, _crouch, _aim, _look, _grendeLaunch, _fire;
      

        #endregion

        #region Unity 生命周期（Awake/OnEnable/OnDisable/Update）

        /// <summary>
        /// 初始化组件与输入动作，集中完成所有初始化，避免分散。
        /// </summary>
        private void Awake()
        {
            // 缓存组件与输入动作
            _characterController = GetComponent<CharacterController>();

            _move = moveAction ? moveAction.action : null;
            _jump = jumpAction ? jumpAction.action : null;
            _sprint = sprintAction ? sprintAction.action : null;
            _crouch = crouchAction ? crouchAction.action : null;
            _aim = aimAction ? aimAction.action : null;
            _look = lookAction ? lookAction.action : null;
            _grendeLaunch = grenadeLaunchAction? grenadeLaunchAction.action : null;
            _fire = fireAction? fireAction : null;

            // _shootCameraTransform 统一回退，避免每帧判空（若需运行时替换可自行赋值覆盖）
            if (shootCamera == null) shootCamera = Camera.main;
            _shootCameraTransform = shootCamera?.transform?? transform;    // 若存在主相机则使用主相机，否则使用自身Transform

            // 初始化 CharacterController 为站立尺寸，并同步 cameraRoot 局部位置
            ApplyControllerDimensionsInstant();

            weaponManager = weaponManager ? weaponManager : GetComponent<WeaponManager>();

            // 初始化相机朝向（与角色朝向对齐）
            if (cameraRoot != null)
            {
                _cameraYaw = transform.eulerAngles.y;
                cameraRoot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
            }

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        /// <summary>
        /// 启用输入动作。
        /// </summary>
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

        /// <summary>
        /// 禁用输入动作。
        /// </summary>
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

        /// <summary>
        /// 每帧更新：读取输入，驱动相机根旋转以及角色的蹲下、移动与旋转。
        /// </summary>
        private void Update()
        {
            // 统一获取时间增量 dt
            float dt = Time.deltaTime;

            // 读取输入
            Vector2 move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            bool hasMoveInput = move.sqrMagnitude > 0.0001f;
            bool wantsSprint = _sprint != null && _sprint.IsPressed() && hasMoveInput;
            bool crouchPressed = _crouch != null && _crouch.WasPressedThisFrame();
            bool crouchHeld = _crouch != null && _crouch.IsPressed();
            IsAiming = _aim != null && _aim.IsPressed();

            // 依次处理：蹲下、移动、旋转（传入同一帧时基，行为更可预测）
            HandleCrouch(crouchPressed, crouchHeld, dt);
            HandleMovement(move, wantsSprint, dt);
            HandleRotation(move, dt);
            HandleWeapon();


        }

        private void LateUpdate()
        {
            Vector2 lookDelta = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
            float dt = Time.deltaTime;
            // 相机根节点旋转（由本脚本外部驱动）
            CameraRootRot(lookDelta, dt);
            weaponManager.UpdateWeaponPitch( _cameraPitch);
        }

        #endregion

        #region 输入辅助

        /// <summary>
        /// 启用某个输入动作（带状态检查）。
        /// </summary>
        /// <param name="a">要启用的 InputAction 实例；允许为 null。</param>
        private static void EnableAction(InputAction a)
        {
            if (a != null && !a.enabled) a.Enable();
        }

        /// <summary>
        /// 禁用某个输入动作（带状态检查）。
        /// </summary>
        /// <param name="a">要禁用的 InputAction 实例；允许为 null。</param>
        private static void DisableAction(InputAction a)
        {
            if (a != null && a.enabled) a.Disable();
        }

        #endregion

        #region 角色移动、姿态（蹲下/移动/旋转/尺寸初始化）、射击

        /// <summary>
        /// 处理蹲下逻辑（支持按住或切换）
        /// - 起身时会检测头顶是否有遮挡，若被挡则保持蹲下
        /// - 使用插值平滑调整 CharacterController 的 height 与 center.y
        /// </summary>
        /// <param name="crouchPressed">是否在本帧“按下”蹲下键（用于切换模式）</param>
        /// <param name="crouchHeld">是否“按住”蹲下键（用于按住模式）</param>
        /// <param name="dt">本帧的时间步长（秒）</param>
        private void HandleCrouch(bool crouchPressed, bool crouchHeld, float dt)
        {
            // 计算期望蹲下状态, true表示希望蹲下
            bool desiredCrouch = holdToCrouch ? crouchHeld : (crouchPressed ? !IsCrouching : IsCrouching);

            if (IsCrouching && !desiredCrouch)
            {
                // 试图起身：检测头顶是否有遮挡物
                if (!CanStandUp())
                    desiredCrouch = true; // 无法起身（有碰撞），仍保持蹲下
            }

            IsCrouching = desiredCrouch;

            // 即时计算目标高度与中心
            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            float targetCenterY = IsCrouching ? crouchCenterY : standCenterY;

            // 平滑插值 CharacterController 的高度与中心（使过渡看起来自然）
            _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, dt * heightLerpSpeed);

            // 注意：center 是局部坐标
            Vector3 center = _characterController.center;
            center.y = Mathf.Lerp(center.y, targetCenterY, dt * heightLerpSpeed);
            _characterController.center = center;

            // 计算 cameraRoot 的局部位置（与 center 解耦，职责更清晰）
            Vector3 camLocal = center;
            camLocal.y += IsCrouching ? 0f : _cameraRootVerticalOffset;
            ChangeCameraRootPosition(camLocal);
        }

        /// <summary>
        /// 处理移动、重力与跳跃。
        /// - 移动方向基于摄像机在水平面上的投影
        /// - 在地面时轻微压住以保证稳定的 isGrounded 检测
        /// - 跳跃通过计算初速度实现特定高度
        /// </summary>
        /// <param name="move">移动输入（x: 右/左, y: 前/后），已在 Input System 中映射</param>
        /// <param name="wantsSprint">是否希望冲刺（仅在有移动输入时有效）</param>
        /// <param name="dt">本帧的时间步长（秒）</param>
        private void HandleMovement(Vector2 move, bool wantsSprint, float dt)
        {
            // 依据摄像机方向决定前后左右向量（投影到水平面）
            Vector3 camForward = _shootCameraTransform.forward;
            Vector3 camRight = _shootCameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // 将输入映射到世界方向（保留模拟量：仅当长度>1时单位化）
            Vector3 inputWorld = camForward * move.y + camRight * move.x;
            if (inputWorld.sqrMagnitude > 1f)
                inputWorld.Normalize();

            // 根据状态选择速度（蹲下时减速，冲刺时加速）
            float speed = IsCrouching ? moveSpeed * 0.6f : (wantsSprint ? sprintSpeed : moveSpeed);

            // 仅水平速度（不包含 y）
            Vector3 horizontalVel = inputWorld * speed;

            // 重力与跳跃逻辑
            if (_characterController.isGrounded)
            {
                // 在地面时设置一个小的负速度来“压住”角色，避免短时间内反复触发跳跃和浮空
                _verticalVelocity = -2f;

                // 跳跃：仅在未蹲下且在本帧按下跳跃时触发
                if (_jump != null && _jump.WasPressedThisFrame() && !IsCrouching)
                {
                    // v = sqrt(-2 * g * h)
                    _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                }
            }
            else
            {
                // 在空中持续施加重力
                _verticalVelocity += gravity * dt;
            }

            // 合成最终速度并移动 CharacterController（Move 会处理碰撞与斜坡）
            Vector3 total = horizontalVel + Vector3.up * _verticalVelocity;
            _characterController.Move(total * dt);
        }

        /// <summary>
        /// 处理角色朝向：
        /// - 在瞄准时，使角色面向相机的水平朝向（使用 _cameraYaw）
        /// - 非瞄准且有移动输入时，使角色面向“相对相机”的输入方向
        /// </summary>
        /// <param name="move">移动输入（用于判断是否需要根据输入方向旋转）</param>
        /// <param name="dt">本帧的时间步长（秒）</param>
        private void HandleRotation(Vector2 move, float dt)
        {
            if (IsAiming)
            {
                // 瞄准时直接对齐到相机水平朝向
                Quaternion targetRot = Quaternion.Euler(0f, _cameraYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * rotationSpeed);
            }
            else if (move.sqrMagnitude > 0.001f) // 非瞄准状态下根据移动方向旋转角色
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

        /// <summary>
        /// 处理开火/扔榴弹等武器相关动作 
        /// </summary>
        private void HandleWeapon()
        {
            if (!IsAiming) return;

            var target = ShootTargetPoint;

            if (_fire?.IsPressed() == true)
                weaponManager.TryFireWeapon(target);

            if (_grendeLaunch?.IsPressed() == true)
                weaponManager.TryThrow(target);
        }

        /// <summary>
        /// 立即将 CharacterController 设置为站立时参数（用于初始化）。
        /// </summary>
        private void ApplyControllerDimensionsInstant()
        {
            _characterController.height = standHeight;

            Vector3 center = _characterController.center;
            center.y = standCenterY;
            _characterController.center = center;

            // 初始化 cameraRoot 的局部位置（若未设置 cameraRoot，内部有 null 保护）
            Vector3 camLocal = center;
            camLocal.y += _cameraRootVerticalOffset;
            ChangeCameraRootPosition(camLocal);
        }

        #endregion

        #region 碰撞与检测

        /// <summary>
        /// 检查角色能否起身（头顶是否被阻挡）。
        /// 使用从站立时头顶向上的短距离 SphereCast。
        /// </summary>
        /// <returns>若头顶无障碍可起身，返回 true；否则返回 false。</returns>
        private bool CanStandUp()
        {
            if (!_characterController) return false;

            // 小段检测距离，足够判断是否被天花板阻挡
            const float clearance = 0.15f;

            // 站立时的半高与头顶位置（注意这里以“站立 center”为基准）
            float halfHeight = standHeight * 0.5f;
            Vector3 centerWorld = transform.TransformPoint(new Vector3(_characterController.center.x, standCenterY, _characterController.center.z));
            Vector3 headTop = centerWorld + Vector3.up * halfHeight;

            // SphereCast 源点略低于头顶，避免与自身胶囊顶部微重叠
            Vector3 origin = headTop + Vector3.down * 0.02f;

            // 探测半径：以 controller.radius 的 90% 为基准，但保证有最小值
            float sphereRadius = Mathf.Max(0.05f, _characterController.radius * 0.9f);

            // 使用非分配接口避免 GC
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

                // 忽略角色自身或子对象
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                // 找到外部障碍，无法起身
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取射击目标点
        /// </summary>
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

        /// <summary>
        /// 将角度规范化到 [-180°, 180°] 区间。
        /// </summary>
        /// <param name="angle">任意角度（度）</param>
        /// <returns>规范化后的角度（度），范围 [-180, 180]</returns>
        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 更改 CameraRoot 的局部位置（带 null 保护）。
        /// </summary>
        /// <param name="target">目标局部坐标（单位：米）</param>
        private void ChangeCameraRootPosition(Vector3 target)
        {
            if (cameraRoot != null)
            {
                cameraRoot.localPosition = target;
            }
        }

        /// <summary>
        /// 旋转相机朝向点（外部驱动 Cinemachine 的 LookAt/Follow 目标朝向）。
        /// </summary>
        /// <param name="lookDelta">视角输入增量（x: yaw, y: pitch）</param>
        /// <param name="dt">本帧的时间步长（秒）</param>
        private void CameraRootRot(Vector2 lookDelta, float dt)
        {
            if (cameraRoot == null) return;

            var device = _look?.activeControl?.device;
            bool isMouse = device is UnityEngine.InputSystem.Mouse;

            float sens = isMouse ? mouseSensitivity : gamepadSensitivity * dt;
            float ySign = invertY ? 1f : -1f;

            _cameraYaw += lookDelta.x * sens;
            _cameraYaw = NormalizeAngle(_cameraYaw); // 防止角度无限增大导致精度问题

            _cameraPitch += lookDelta.y * ySign * sens;
            _cameraPitch = Mathf.Clamp(_cameraPitch, pitchClamp.x, pitchClamp.y);

            
            // 直接设置“世界旋转”，Unity 会换算为 localRotation
            cameraRoot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        }



        #endregion
    }
}