using UnityEngine;

public class EnemyPatrolState : IEnemyState
{
    // 进入Patrol：开始移动到下一个巡逻点
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = false;
        enemy.GoToNextPoint(); // 调用敌人的巡逻点切换方法
    }

    // 每帧更新：到达巡逻点切换到Idle，看到玩家切换到警戒
    public void Update(Enemy enemy)
    {
        // 检查是否到达目标点
        if (!enemy.navMeshAgent.pathPending &&
            enemy.navMeshAgent.remainingDistance <= enemy.navMeshAgent.stoppingDistance)
        {
            enemy.stateMachine.ChangeState(enemy.idleState, enemy);
        }

        /*
        // 检测到玩家 → 切换到警戒
        if (enemy.CanSeePlayer())
        {
            enemy.stateMachine.ChangeState(enemy.alertState, enemy);
        }
        */
    }

    // 退出Patrol：无特殊操作
    public void Exit(Enemy enemy) { }
}