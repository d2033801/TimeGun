using UnityEngine;

public class EnemyIdleState : IEnemyState
{
    // ����Idle��ֹͣ�ƶ������õȴ���ʱ��
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = true;
        enemy.navMeshAgent.velocity = Vector3.zero;
        enemy.stateTimer = enemy.waitTime; // ʹ�õ������õĵȴ�ʱ��
    }

    // ÿ֡���£���ʱ�����л���Ѳ�ߣ���������л�������
    public void Update(Enemy enemy)
    {
        enemy.stateTimer -= Time.deltaTime;
        if (enemy.stateTimer <= 0)
        {
            // �л���Ѳ��״̬��ͨ��״̬����
            enemy.stateMachine.ChangeState(enemy.patrolState, enemy);
        }

        // ��⵽��� �� �л�������
        if (enemy.CanSeePlayer())
        {
        }
    }

    // �˳�Idle�����������
    public void Exit(Enemy enemy) { }
}
