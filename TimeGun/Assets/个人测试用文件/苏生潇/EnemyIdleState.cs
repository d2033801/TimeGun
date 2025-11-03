using UnityEngine;

public class EnemyIdleState : IEnemyState
{
    // 进入Idle：停止移动，重置等待计时器
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = true;
        enemy.navMeshAgent.velocity = Vector3.zero;
        enemy.StateTimer = enemy.waitTime; // 使用敌人配置的等待时间
    }

    // 每帧更新：计时结束切换到巡逻，看到玩家切换到警戒
    public void Update(Enemy enemy)
    {
        enemy.StateTimer -= Time.deltaTime;
        if (enemy.StateTimer <= 0)
        {
            // 切换到巡逻状态（通过状态机）
            enemy.stateMachine.ChangeState(enemy.patrolState, enemy);
        }

        // 检测到玩家 → 切换到警戒
        if (enemy.CanSeePlayer())
        {
        }
    }

    // 退出Idle：无特殊操作
    public void Exit(Enemy enemy) { }
}
