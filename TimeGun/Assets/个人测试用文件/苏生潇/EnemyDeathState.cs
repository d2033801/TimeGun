using UnityEngine;

public class EnemyDeathState : IEnemyState
{
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = true;
        enemy.navMeshAgent.velocity = Vector3.zero;

    }
    public void Update(Enemy enemy)
    {
        // ����״̬��������߼�
    }
    public void Exit(Enemy enemy)
    {
        // ����״̬�����˳��߼�
    }
}
