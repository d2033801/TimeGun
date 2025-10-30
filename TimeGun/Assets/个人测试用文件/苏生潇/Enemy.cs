using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("Ѳ������")]
    public Transform[] patrolPoints; // Ѳ�ߵ�����
    public float waitTime = 2f; // ÿ��Ѳ�ߵ�ͣ��ʱ��

    [Header("��������")]
    public Animator modelAnimator;
    public float smoothTime = 0.1f;

    [Header("��Ұ����")]
    public float viewAngle = 90f;
    public float viewRadius = 10f;
    public Transform headTransform; // ������㣨ͷ����
    public LayerMask playerMask; // ���ͼ��
    public LayerMask obstacleMask; // �ϰ���ͼ��

    [Header("������������")]
    internal bool isDead = false; // ����״̬
    private const float crushForceThreshold = 1f;

    // ��״̬����״̬ʵ����
    internal EnemyStateMachine stateMachine; // ״̬����internal����״̬����ʣ�
    internal EnemyIdleState idleState;
    internal EnemyPatrolState patrolState;
    internal EnemyAlertState alertState;
    internal EnemyDeathState deathState;
    /*
    internal EnemyAttackState attackState;
    */

    internal NavMeshAgent navMeshAgent; // �������
    internal Transform player; // �������
    internal float stateTimer; // ״̬��ʱ������Idle�ĵȴ�ʱ�䣩
    internal int currentPointIndex = -1; // ��ǰѲ�ߵ�����

    private float _currentSpeed;
    private float _speedVelocity;

    void Start()
    {
        // ��ʼ���������
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.autoBraking = true;
        navMeshAgent.updateRotation = false; // �����Զ���ת

        // ��ʼ���������
        if (modelAnimator == null)
        {
            modelAnimator = GetComponentInChildren<Animator>();
        }

        // ��ʼ���������
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) Debug.LogWarning("δ�ҵ�Player��ǩ�Ķ���");

        // ��ʼ��״̬����״̬
        stateMachine = new EnemyStateMachine();
        idleState = new EnemyIdleState();
        patrolState = new EnemyPatrolState();
        alertState = new EnemyAlertState();
        /*
        attackState = new EnemyAttackState();
        */

        // ��ʼ״̬��Idle
        stateMachine.ChangeState(idleState, this);
    }

    void Update()
    {
        if (isDead || patrolPoints.Length == 0) 
            return;

        // ״̬�����£����ģ����õ�ǰ״̬���߼���
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

        // ����
        if (distToPlayer > viewRadius) return false;

        // �Ƕ�
        float angle = Vector3.Angle(headTransform.forward, dirToPlayer);
        if (angle > viewAngle / 2) return false;

        // �ڵ�
        if (Physics.Raycast(headTransform.position, dirToPlayer, distToPlayer, obstacleMask))
            return false;

        return true;
    }

    public void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"{gameObject.name} ��ײ�� {collision.gameObject.name}, impulse={collision.impulse.magnitude}");

        // ����Ѿ����������
        if (isDead) 
            return;

        // ���Ե������΢��ײ
        if (collision.impulse.magnitude < crushForceThreshold)
            return;

        Debug.Log(collision.impulse.magnitude);

        // ���ײ�������Ǵ����������
        if (collision.rigidbody != null)
        {
            Debug.Log($"{gameObject.name} �� {collision.gameObject.name} �ĳ����������");
            Die();
        }
    }

    public void Die()
    {
        if (isDead) 
            return; // ��ֹ�ظ�����

        isDead = true;

        Debug.Log($"{gameObject.name} ����");

        // �ر�Ѱ·
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.enabled = false;
        }

        // ����������ײ�壨��ֹ����������
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

        /*
        // ֹͣ�����˶�����������壩
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        */

        // ������������
        modelAnimator?.SetTrigger("Die");

        /*
        // �л�״̬������������ã�
        if (stateMachine != null && deathState != null)
            stateMachine.ChangeState(deathState, this);
        */
    }

    // ��Gizmos��Scene���ڻ�����Ұ��Χ��
    private void OnDrawGizmosSelected()
    {
        if (headTransform == null) return;

        // ��Ұ�뾶
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(headTransform.position, viewRadius);

        // ��Ұ�Ƕȣ�׶�Σ�
        Vector3 left = DirFromAngle(-viewAngle / 2);
        Vector3 right = DirFromAngle(viewAngle / 2);
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawLine(headTransform.position, headTransform.position + left * viewRadius);
        Gizmos.DrawLine(headTransform.position, headTransform.position + right * viewRadius);

        // ��� Scene ���������ܿ�����ң���һ������ָ�����
        if (Application.isPlaying && player != null)
        {
            if (CanSeePlayer()) 
            { 
                Gizmos.color = Color.red; 
                Gizmos.DrawLine(headTransform.position, player.position);
            }
        }
    }

    // ������Ұ�߽緽��
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