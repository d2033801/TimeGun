using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Android;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("巡逻设置")]
    [Tooltip("敌人巡逻的路径点数组，按顺序循环巡逻")]
    public Transform[] patrolPoints;

    [Tooltip("敌人在每个巡逻点的停留时间（秒）")]
    public float waitTime = 2f;

    [Header("动画设置")]
    [Tooltip("敌人模型上的Animator组件，用于控制动画播放")]
    public Animator modelAnimator;

    [Tooltip("速度平滑过渡时间，值越小过渡越快")]
    public float smoothTime = 0.1f;

    [Header("视野设置")]
    [Tooltip("敌人的视野角度（度），以前方为中心的扇形角度")]
    public float viewAngle = 90f;

    [Tooltip("敌人的视野半径（米），超出此距离无法看到玩家")]
    public float viewRadius = 10f;

    [Tooltip("敌人头部Transform，作为视线检测的起点")]
    public Transform headTransform;

    [Tooltip("玩家所在的图层遮罩，用于视线检测")]
    public LayerMask playerMask;

    [Tooltip("障碍物图层遮罩，用于判断视线是否被遮挡")]
    public LayerMask obstacleMask;

    [SerializeField, Tooltip("发现玩家后的持续观察时间")]
    private float seePlayerTimer;

    [Header("死亡触发设置")]
    [Tooltip("敌人是否已死亡")]
    private bool isDead = false;

    [Tooltip("触发死亡所需的最小冲击力阈值")]
    private const float crushForceThreshold = 50f;



    // 【状态机与状态实例】
    internal EnemyStateMachine stateMachine; // 状态机（internal允许状态类访问）
    internal EnemyIdleState idleState;
    internal EnemyPatrolState patrolState;
    internal EnemyAlertState alertState;
    internal EnemyDeathState deathState;

    internal float stateTimer; // 状态计时器（如Idle的等待时间）
    internal int currentPointIndex = -1; // 当前巡逻点索引

    private float _currentSpeed; // 当前平滑后的移动速度
    private float _speedVelocity; // SmoothDamp内部使用的速度变化率


    internal Transform player; // 玩家引用
    internal NavMeshAgent navMeshAgent; // 导航组件

    /// <summary>
    /// 获取或设置敌人的死亡状态。
    /// </summary>
    /// <value>
    /// 如果敌人已死亡则为 <c>true</c>；否则为 <c>false</c>。
    /// </value>
    public bool IsDead
    {
        get => isDead;
        internal set => isDead = value;
    }

    /// <summary>
    /// 获取或设置发现玩家后的持续观察计时器（秒）。
    /// </summary>
    /// <value>
    /// 观察时间剩余秒数，值越大表示警戒状态持续越久。
    /// </value>
    public float SeePlayerTimer
    {
        get => seePlayerTimer;
        set => seePlayerTimer = value;
    }

    /// <summary>
    /// 获取或设置 SmoothDamp 内部使用的速度变化率。
    /// </summary>
    /// <value>
    /// 用于平滑过渡动画速度参数的辅助变量。
    /// </value>
    public float SpeedVelocity
    {
        get => _speedVelocity;
        set => _speedVelocity = value;
    }

    /// <summary>
    /// 获取或设置当前平滑后的移动速度（用于驱动动画）。
    /// </summary>
    /// <value>
    /// 经过平滑处理的速度值，会传递给 Animator 的 Speed 参数。
    /// </value>
    public float CurrentSpeed
    {
        get => _currentSpeed;
        set => _currentSpeed = value;
    }

    /// <summary>
    /// 获取或设置当前巡逻点索引。
    /// </summary>
    /// <value>
    /// 当前目标巡逻点在 <see cref="patrolPoints"/> 数组中的索引，-1 表示未初始化。
    /// </value>
    internal int CurrentPointIndex
    {
        get => currentPointIndex;
        set => currentPointIndex = value;
    }

    /// <summary>
    /// 获取或设置状态计时器（用于状态持续时间计算）。
    /// </summary>
    /// <value>
    /// 当前状态的计时器（秒），例如 Idle 状态的等待时间或 Alert 状态的追踪时间。
    /// </value>
    internal float StateTimer
    {
        get => stateTimer;
        set => stateTimer = value;
    }
    void Start()
    {
        // 初始化导航组件
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.autoBraking = true;
        navMeshAgent.updateRotation = false; // 禁用自动旋转

        // 初始化动画组件
        if (modelAnimator == null)
        {
            modelAnimator = GetComponentInChildren<Animator>();
        }

        // 初始化玩家引用
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) Debug.LogWarning("未找到Player标签的对象！");

        // 初始化状态机和状态
        stateMachine = new EnemyStateMachine();
        idleState = new EnemyIdleState();
        patrolState = new EnemyPatrolState();
        alertState = new EnemyAlertState();

        // 初始状态：Idle
        stateMachine.ChangeState(idleState, this);
    }

    void Update()
    {
        if (IsDead || patrolPoints.Length == 0)
            return;

        // 状态机更新（核心：调用当前状态的逻辑）
        stateMachine.Update(this);


        if (navMeshAgent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(navMeshAgent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }


        if (_currentSpeed < 0.05f) _currentSpeed = 0f;
        float targetSpeed = navMeshAgent.velocity.magnitude;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, smoothTime);
        modelAnimator?.SetFloat("Speed", _currentSpeed);
    }

    internal void GoToNextPoint()
    {
        if (patrolPoints.Length == 0)
            return;
        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        navMeshAgent.SetDestination(patrolPoints[currentPointIndex].position);
    }

    public bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 dirToPlayer = (player.position - headTransform.position).normalized;
        float distToPlayer = Vector3.Distance(headTransform.position, player.position);

        // 距离
        if (distToPlayer > viewRadius) return false;

        // 角度
        float angle = Vector3.Angle(headTransform.forward, dirToPlayer);
        if (angle > viewAngle / 2) return false;

        // 遮挡
        if (Physics.Raycast(headTransform.position, dirToPlayer, distToPlayer, obstacleMask))
            return false;

        return true;
    }

    public void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"{gameObject.name} 碰撞到 {collision.gameObject.name}, impulse={collision.impulse.magnitude}");

        // 如果已经死亡则忽略
        if (IsDead)
            return;

        // 忽略地面等轻微碰撞
        if (collision.impulse.magnitude < crushForceThreshold)
            return;

        Debug.Log(collision.impulse.magnitude);

        // 如果撞上来的是带刚体的重物
        if (collision.rigidbody != null)
        {
            Debug.Log($"{gameObject.name} 被 {collision.gameObject.name} 的冲击力砸死！");
            Die();
        }
    }

    public void Die()
    {
        if (IsDead)
            return; // 防止重复死亡

        IsDead = true;

        Debug.Log($"{gameObject.name} 死亡");

        // 关闭寻路
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.enabled = false;
        }

        // 禁用所有碰撞体（防止继续触发）
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;


        // 播放死亡动画
        modelAnimator?.SetTrigger("Die");

        /*
        // 切换状态机
        if (stateMachine != null && deathState != null)
            stateMachine.ChangeState(deathState, this);
        */
    }

    // 【Gizmos：Scene窗口绘制视野范围】
    private void OnDrawGizmosSelected()
    {
        if (headTransform == null) return;

        // 视野半径
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(headTransform.position, viewRadius);

        // 视野角度（锥形）
        Vector3 left = DirFromAngle(-viewAngle / 2);
        Vector3 right = DirFromAngle(viewAngle / 2);
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawLine(headTransform.position, headTransform.position + left * viewRadius);
        Gizmos.DrawLine(headTransform.position, headTransform.position + right * viewRadius);

        // 如果 Scene 运行中且能看到玩家，则画一条红线指向玩家
        if (Application.isPlaying && player != null)
        {
            if (CanSeePlayer())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(headTransform.position, player.position);
            }
        }
    }

    // 计算视野边界方向
    private Vector3 DirFromAngle(float angleDeg)
    {
        angleDeg += headTransform.eulerAngles.y;
        return new Vector3(
            Mathf.Sin(angleDeg * Mathf.Deg2Rad),
            0,
            Mathf.Cos(angleDeg * Mathf.Deg2Rad)
        );
    }
}