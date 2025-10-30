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
        // 死亡状态无需更新逻辑
    }
    public void Exit(Enemy enemy)
    {
        // 死亡状态无需退出逻辑
    }
}
