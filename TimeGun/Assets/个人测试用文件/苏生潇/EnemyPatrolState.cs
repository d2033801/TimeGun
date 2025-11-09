using UnityEngine;

public class EnemyPatrolState : IEnemyState
{
    // 进入Patrol：开始移动到下一个巡逻点
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = false;
        
        // 检查是否从警戒状态返回
        if (enemy.isReturningFromAlert)
        {
            // 从警戒状态返回，保持当前巡逻点不变，继续前往该点
            Debug.Log($"[{enemy.name}] 从警戒状态返回，继续前往当前巡逻点 [{enemy.CurrentPointIndex}]");
            
            // 重新设置目标（防止 NavMeshAgent 状态丢失）
            if (enemy.CurrentPointIndex >= 0 && enemy.CurrentPointIndex < enemy.patrolPoints.Length)
            {
                enemy.navMeshAgent.SetDestination(enemy.patrolPoints[enemy.CurrentPointIndex].position);
            }
            else
            {
                // 如果索引无效，则初始化到第一个巡逻点
                enemy.CurrentPointIndex = 0;
                enemy.navMeshAgent.SetDestination(enemy.patrolPoints[0].position);
            }
            
            // 重置标志
            enemy.isReturningFromAlert = false;
        }
        else
        {
            // 正常情况：从 Idle 进入 Patrol，前往下一个巡逻点
            enemy.GoToNextPoint();
        }
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

        // 检测到玩家 → 切换到警戒
        if (enemy.CanSeePlayer())
        {
            enemy.stateMachine.ChangeState(enemy.alertState, enemy);
        }
    }

    // 退出Patrol：无特殊操作
    public void Exit(Enemy enemy) { }
}