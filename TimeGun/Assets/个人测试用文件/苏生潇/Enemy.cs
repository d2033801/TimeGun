using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class Enemy : MonoBehaviour
{
    // 移动与寻路
    public Transform[] patrolPoints;
    private int currentPointIndex = 0;
    public float moveSpeed = 3f;
    private Rigidbody rb;

    // 视线检测
    public float viewAngle = 90f;
    public float viewRadius = 10f;
    public LayerMask pLayerMask;
    public LayerMask obstacleMask;

    // 敌人状态
    private enum State
    {
        Idle,
        Patrol,
        Alert,
        Attack
    }
    private State currentState = State.Idle;

    private Transform player;
    // 组件引用


    // 内部状态数据
    private bool isDead;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        if (player != null)
        {
            Debug.Log("player exist");
        }
        else
        {
            Debug.Log("player not exist");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (patrolPoints.Length == 0)
            return;

        PatrolToCurrentPoint();
        CanSeePlayer();
    }

    void PatrolToCurrentPoint()
    {
        Transform targetPoint = patrolPoints[currentPointIndex];
        Vector3 targetPosition = targetPoint.position;

        Vector3 moveDirection = new Vector3(
            targetPosition.x - transform.position.x,
            0,
            targetPosition.z - transform.position.z
            ).normalized;

        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed
        );

        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        float distanceToPoint = Vector3.Distance(transform.position, targetPosition);
        if (distanceToPoint < 0.5f)
        {
            currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        }
    }

    public bool CanSeePlayer()
    {
        if (player == null)
            return false;
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (distToPlayer < viewRadius)
        {
            float angleBetween = Vector3.Angle(transform.forward, dirToPlayer);
            if (angleBetween < viewAngle / 2f)
            {
                if (!Physics.Raycast(transform.position + Vector3.up, dirToPlayer, distToPlayer, obstacleMask))
                    return true;
            }
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        // 画出敌人的可视半径
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // 计算视角两条边界线
        Vector3 leftBoundary = DirFromAngle(-viewAngle / 2, false);
        Vector3 rightBoundary = DirFromAngle(viewAngle / 2, false);

        // 画出锥形范围
        Gizmos.color = new Color(0, 1, 1, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

        // 如果 Scene 运行中且能看到玩家，则画一条红线指向玩家
        if (Application.isPlaying && player != null)
        {
            if (CanSeePlayer())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up, player.position + Vector3.up);
            }
        }
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
