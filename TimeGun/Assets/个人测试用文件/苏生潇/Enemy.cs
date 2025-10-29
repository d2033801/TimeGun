using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class Enemy : MonoBehaviour
{
    // �ƶ���Ѱ·
    public Transform[] patrolPoints;
    private int currentPointIndex = 0;
    public float waitTime = 2f;
    
    private NavMeshAgent navMeshAgent;
    private float waitCounter = 0f;
    private bool waiting = false;

    // ���߼��
    public float viewAngle = 90f;
    public float viewRadius = 10f;
    public LayerMask pLayerMask;
    public LayerMask obstacleMask;

    // ����״̬
    private enum State
    {
        Idle,
        Patrol,
        Alert,
        Attack
    }
    private State currentState = State.Idle;

    private Transform player;
    // �������


    // �ڲ�״̬����
    private bool isDead;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.autoBraking = false;
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

        if (waiting)
        {
            waitCounter -= Time.deltaTime;
            if (waitCounter <= 0f)
            {
                waiting = false;
                GoToNextPoint();
            }
        }
        else if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f)
        {
            // ����Ѳ�ߵ�
            waiting = true;
            waitCounter = waitTime;
        }

        CanSeePlayer();
    }

    void GoToNextPoint()
    {
        if(patrolPoints.Length == 0)
            return;

        navMeshAgent.destination = patrolPoints[currentPointIndex].position;
        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
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
        // �������˵Ŀ��Ӱ뾶
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // �����ӽ������߽���
        Vector3 leftBoundary = DirFromAngle(-viewAngle / 2, false);
        Vector3 rightBoundary = DirFromAngle(viewAngle / 2, false);

        // ����׶�η�Χ
        Gizmos.color = new Color(0, 1, 1, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

        // ��� Scene ���������ܿ�����ң���һ������ָ�����
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
