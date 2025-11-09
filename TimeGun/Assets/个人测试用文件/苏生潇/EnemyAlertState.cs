using UnityEngine;

public class EnemyAlertState : IEnemyState
{
    // 发现玩家多少秒后杀死玩家
    private const float killPlayerDelay = 0.5f;
    
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = true;
        enemy.SeePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", true);
        
        Debug.Log($"[{enemy.name}] 进入警戒状态，开始追踪玩家");
    }

    public void Update(Enemy enemy)
    {
        bool canSee = enemy.CanSeePlayer();

        if (canSee)
        {
            enemy.SeePlayerTimer += Time.deltaTime;
            
            // 发现玩家 0.5 秒后杀死玩家
            if (enemy.SeePlayerTimer >= killPlayerDelay)
            {
                KillPlayer(enemy);
            }
        }
        else
        {
            // 失去视线，返回巡逻状态
            enemy.stateMachine.ChangeState(enemy.patrolState, enemy);
        }
    }

    public void Exit(Enemy enemy)
    {
        enemy.SeePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", false);
    }

    /// <summary>
    /// 杀死玩家
    /// </summary>
    private void KillPlayer(Enemy enemy)
    {
        if (enemy.player == null) return;

        var playerController = enemy.player.GetComponent<TimeGun.PlayerController>();
        if (playerController != null && !playerController.IsDead)
        {
            Debug.Log($"[{enemy.name}] 发现玩家 {killPlayerDelay} 秒，杀死玩家！");
            
            // 传递敌人的 Transform 给玩家死亡方法
            playerController.Die(enemy.transform);
        }
    }
}
