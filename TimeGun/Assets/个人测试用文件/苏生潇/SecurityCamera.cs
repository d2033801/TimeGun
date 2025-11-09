using UnityEngine;

/// <summary>
/// 闭路电视（CCTV）安全摄像头系统
/// 功能：
/// - 90度水平来回摆动
/// - 扇形视野检测（模仿Enemy系统）
/// - 发现玩家后持续观察0.5秒触发死亡
/// - 死亡摄像机第一人称视角支持（从lookAtTarget位置看向场景）
/// - 支持Gizmos可视化视野范围
/// </summary>
public class SecurityCamera : MonoBehaviour
{
    [Header("摆动设置")]
    [Tooltip("摄像头来回摆动的角度范围（总共90度，每侧45度）")]
    public float swingAngle = 45f;

    [Tooltip("摄像头摆动速度（度/秒）")]
    public float swingSpeed = 30f;

    [Header("视野设置")]
    [Tooltip("摄像头的视野角度（度），以前方为中心的扇形角度")]
    public float viewAngle = 60f;

    [Tooltip("摄像头的视野半径（米），超出此距离无法看到玩家")]
    public float viewRadius = 15f;

    [Tooltip("摄像头头部Transform，作为视线检测的起点（通常是摄像头镜头位置）")]
    public Transform cameraHead;

    [Tooltip("死亡摄像机的位置点（死亡时摄像机会从这个点看向玩家）\n如果为空，使用cameraHead\n建议设置为镜头位置以获得第一人称摄像头视角")]
    public Transform lookAtTarget;

    [Tooltip("玩家所在的图层遮罩，用于视线检测")]
    public LayerMask playerMask;

    [Tooltip("障碍物图层遮罩，用于判断视线是否被遮挡")]
    public LayerMask obstacleMask;

    [Header("检测设置")]
    [Tooltip("发现玩家后需要持续观察多久才触发警报（秒）")]
    public float detectDuration = 0.5f;

    [Header("视觉效果（可选）")]
    [Tooltip("摄像头镜头物体（用于旋转动画）")]
    public Transform cameraLens;

    [Tooltip("检测到玩家时的警报灯光")]
    public Light alarmLight;

    [Tooltip("正常状态灯光颜色")]
    public Color normalLightColor = Color.green;

    [Tooltip("警报状态灯光颜色")]
    public Color alarmLightColor = Color.red;

    // 内部状态
    private Transform _player;
    private float _currentAngle = 0f; // 当前摆动角度
    private bool _swingingRight = true; // 是否正在向右摆动
    private float _detectTimer = 0f; // 检测计时器
    private bool _playerDetected = false; // 是否检测到玩家
    private bool _alarmTriggered = false; // 是否已触发警报
    private Quaternion _initialRotation; // 初始旋转（用于计算摆动）

    void Start()
    {
        // 初始化玩家引用
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"[SecurityCamera] {gameObject.name} 未找到Player标签的对象！");
        }

        // 如果没有设置cameraHead，使用自身
        if (cameraHead == null)
        {
            cameraHead = transform;
        }

        // ? 如果没有设置lookAtTarget，使用cameraHead（死亡摄像机会从cameraHead的位置看向场景）
        if (lookAtTarget == null)
        {
            lookAtTarget = cameraHead;
            Debug.Log($"[SecurityCamera] {gameObject.name} 未设置lookAtTarget，自动使用cameraHead");
        }

        // 记录初始旋转
        _initialRotation = transform.rotation;

        // 初始化灯光
        if (alarmLight != null)
        {
            alarmLight.color = normalLightColor;
        }
    }

    void Update()
    {
        // 如果已触发警报，停止更新
        if (_alarmTriggered) return;

        // 更新摆动
        UpdateSwing();

        // 检测玩家
        UpdatePlayerDetection();
    }

    /// <summary>
    /// 更新摄像头摆动逻辑
    /// </summary>
    private void UpdateSwing()
    {
        // 计算摆动增量
        float swingDelta = swingSpeed * Time.deltaTime;

        if (_swingingRight)
        {
            _currentAngle += swingDelta;
            if (_currentAngle >= swingAngle)
            {
                _currentAngle = swingAngle;
                _swingingRight = false; // 反转方向
            }
        }
        else
        {
            _currentAngle -= swingDelta;
            if (_currentAngle <= -swingAngle)
            {
                _currentAngle = -swingAngle;
                _swingingRight = true; // 反转方向
            }
        }

        // 应用摆动旋转（仅Y轴）
        Quaternion swingRotation = Quaternion.Euler(0f, _currentAngle, 0f);
        transform.rotation = _initialRotation * swingRotation;

        // 如果设置了cameraLens，同步旋转
        if (cameraLens != null && cameraLens != transform)
        {
            cameraLens.rotation = transform.rotation;
        }
    }

    /// <summary>
    /// 更新玩家检测逻辑
    /// </summary>
    private void UpdatePlayerDetection()
    {
        if (_player == null) return;

        // 检查玩家是否死亡
        var playerController = _player.GetComponent<TimeGun.PlayerController>();
        if (playerController != null && playerController.IsDead)
        {
            // 玩家已死亡，重置检测
            _playerDetected = false;
            _detectTimer = 0f;
            if (alarmLight != null)
            {
                alarmLight.color = normalLightColor;
            }
            return;
        }

        bool canSee = CanSeePlayer();

        if (canSee)
        {
            if (!_playerDetected)
            {
                // 刚发现玩家
                _playerDetected = true;
                _detectTimer = 0f;
                Debug.Log($"[SecurityCamera] {gameObject.name} 发现玩家！");
            }

            // 累积检测时间
            _detectTimer += Time.deltaTime;

            // 更新灯光颜色（渐变效果）
            if (alarmLight != null)
            {
                float t = Mathf.Clamp01(_detectTimer / detectDuration);
                alarmLight.color = Color.Lerp(normalLightColor, alarmLightColor, t);
            }

            // 检查是否达到触发阈值
            if (_detectTimer >= detectDuration && !_alarmTriggered)
            {
                TriggerAlarm();
            }
        }
        else
        {
            // 玩家离开视野，重置检测
            if (_playerDetected)
            {
                Debug.Log($"[SecurityCamera] {gameObject.name} 玩家离开视野");
                _playerDetected = false;
                _detectTimer = 0f;

                if (alarmLight != null)
                {
                    alarmLight.color = normalLightColor;
                }
            }
        }
    }

    /// <summary>
    /// 检测是否能看到玩家（模仿Enemy.CanSeePlayer）
    /// </summary>
    public bool CanSeePlayer()
    {
        if (_player == null || cameraHead == null) return false;

        Vector3 dirToPlayer = (_player.position - cameraHead.position).normalized;
        float distToPlayer = Vector3.Distance(cameraHead.position, _player.position);

        // 1. 距离检查
        if (distToPlayer > viewRadius) return false;

        // 2. 角度检查
        float angle = Vector3.Angle(cameraHead.forward, dirToPlayer);
        if (angle > viewAngle / 2f) return false;

        // 3. 遮挡检查
        if (Physics.Raycast(cameraHead.position, dirToPlayer, distToPlayer, obstacleMask))
            return false;

        return true;
    }

    /// <summary>
    /// 触发警报，杀死玩家
    /// </summary>
    private void TriggerAlarm()
    {
        _alarmTriggered = true;
        Debug.Log($"[SecurityCamera] {gameObject.name} 触发警报！玩家被发现 {detectDuration} 秒！");

        // 灯光变红
        if (alarmLight != null)
        {
            alarmLight.color = alarmLightColor;
        }

        // 杀死玩家
        if (_player != null)
        {
            var playerController = _player.GetComponent<TimeGun.PlayerController>();
            if (playerController != null && !playerController.IsDead)
            {
                // ? 传递摄像头Transform作为击杀者
                // 死亡摄像机会从 lookAtTarget 的位置看向玩家死亡的区域
                playerController.Die(transform);
            }
        }
    }

    /// <summary>
    /// 获取当前检测进度（0-1）
    /// </summary>
    public float DetectionProgress
    {
        get { return Mathf.Clamp01(_detectTimer / detectDuration); }
    }

    /// <summary>
    /// 是否正在检测玩家
    /// </summary>
    public bool IsDetectingPlayer
    {
        get { return _playerDetected && !_alarmTriggered; }
    }

    // ========== Gizmos可视化 ==========
    private void OnDrawGizmosSelected()
    {
        Transform head = cameraHead != null ? cameraHead : transform;

        // 绘制视野半径
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(head.position, viewRadius);

        // 绘制视野锥形
        Vector3 leftBoundary = DirFromAngle(-viewAngle / 2f, head);
        Vector3 rightBoundary = DirFromAngle(viewAngle / 2f, head);
        
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(head.position, head.position + leftBoundary * viewRadius);
        Gizmos.DrawLine(head.position, head.position + rightBoundary * viewRadius);

        // 绘制视野扇形弧线
        Vector3 previousPoint = head.position + leftBoundary * viewRadius;
        int segments = 20;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-viewAngle / 2f, viewAngle / 2f, i / (float)segments);
            Vector3 dir = DirFromAngle(angle, head);
            Vector3 point = head.position + dir * viewRadius;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }

        // ? 绘制 lookAtTarget（死亡摄像机位置点）
        if (lookAtTarget != null && lookAtTarget != transform)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lookAtTarget.position, 0.2f);
            Gizmos.DrawLine(transform.position, lookAtTarget.position);
        }

        // 绘制摆动范围（运行时）
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Vector3 leftSwing = _initialRotation * Quaternion.Euler(0f, -swingAngle, 0f) * Vector3.forward;
            Vector3 rightSwing = _initialRotation * Quaternion.Euler(0f, swingAngle, 0f) * Vector3.forward;
            Gizmos.DrawLine(head.position, head.position + leftSwing * viewRadius * 0.8f);
            Gizmos.DrawLine(head.position, head.position + rightSwing * viewRadius * 0.8f);

            // 如果检测到玩家，绘制红线
            if (_player != null && CanSeePlayer())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(head.position, _player.position);
            }
        }
        else
        {
            // 编辑器模式下绘制摆动预览
            Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
            Vector3 leftSwing = transform.rotation * Quaternion.Euler(0f, -swingAngle, 0f) * Vector3.forward;
            Vector3 rightSwing = transform.rotation * Quaternion.Euler(0f, swingAngle, 0f) * Vector3.forward;
            Gizmos.DrawLine(head.position, head.position + leftSwing * viewRadius * 0.8f);
            Gizmos.DrawLine(head.position, head.position + rightSwing * viewRadius * 0.8f);
        }
    }

    /// <summary>
    /// 计算视野边界方向（模仿Enemy.DirFromAngle）
    /// </summary>
    private Vector3 DirFromAngle(float angleDeg, Transform reference)
    {
        angleDeg += reference.eulerAngles.y;
        return new Vector3(
            Mathf.Sin(angleDeg * Mathf.Deg2Rad),
            0f,
            Mathf.Cos(angleDeg * Mathf.Deg2Rad)
        );
    }
}
