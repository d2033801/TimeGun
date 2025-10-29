using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    // 【巡逻配置】
    [Header("巡逻设置")]
    public Transform[] patrolPoints; // 巡逻点数组
    public float waitTime = 2f; // 每个巡逻点停留时间

    // 【动画配置】
    [Header("动画设置")]
    public Animator modelAnimator;
    public float smoothTime = 0.1f;

    // 【视野配置】
    [Header("视野设置")]
    public float viewAngle = 90f;
    public float viewRadius = 10f;
    public Transform headTransform; // 视线起点（头部）
    public LayerMask playerMask; // 玩家图层
    public LayerMask obstacleMask; // 障碍物图层

    // 【状态机与状态实例】
    internal EnemyStateMachine stateMachine; // 状态机（internal允许状态类访问）
    internal EnemyIdleState idleState;
    internal EnemyPatrolState patrolState;
    /*
    internal EnemyAlertState alertState;
    internal EnemyAttackState attackState;
    */

    // 【内部数据（供状态类访问）】
    internal NavMeshAgent navMeshAgent; // 导航组件
    internal Transform player; // 玩家引用
    internal float stateTimer; // 状态计时器（如Idle的等待时间）
    internal bool isDead; // 死亡状态
    internal int currentPointIndex = -1; // 当前巡逻点索引

    // 【动画平滑参数】
    private float _currentSpeed;
    private float _speedVelocity;

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
        /*
        alertState = new EnemyAlertState();
        attackState = new EnemyAttackState();
        */

        // 初始状态：Idle
        stateMachine.ChangeState(idleState, this);
    }

    void Update()
    {
        if (isDead || patrolPoints.Length == 0) return;

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