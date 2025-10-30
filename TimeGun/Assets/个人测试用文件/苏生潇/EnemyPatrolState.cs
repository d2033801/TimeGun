using UnityEngine;

public class EnemyPatrolState : IEnemyState
{
    // ����Patrol����ʼ�ƶ�����һ��Ѳ�ߵ�
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = false;
        enemy.GoToNextPoint(); // ���õ��˵�Ѳ�ߵ��л�����
    }

    // ÿ֡���£�����Ѳ�ߵ��л���Idle����������л�������
    public void Update(Enemy enemy)
    {
        // ����Ƿ񵽴�Ŀ���
        if (!enemy.navMeshAgent.pathPending &&
            enemy.navMeshAgent.remainingDistance <= enemy.navMeshAgent.stoppingDistance)
        {
            enemy.stateMachine.ChangeState(enemy.idleState, enemy);
        }

        /*
        // ��⵽��� �� �л�������
        if (enemy.CanSeePlayer())
        {
            enemy.stateMachine.ChangeState(enemy.alertState, enemy);
        }
        */
    }

    // �˳�Patrol�����������
    public void Exit(Enemy enemy) { }
}