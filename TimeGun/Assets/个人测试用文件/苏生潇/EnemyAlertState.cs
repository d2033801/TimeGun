using UnityEngine;

public class EnemyAlertState : IEnemyState
{
    // 发现玩家多少秒后杀死玩家
    private const float killPlayerDelay = 0.5f;
    
    // 旋转速度（度/秒）
    private const float rotationSpeed = 10f;
    
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
            
            // ✅ 让敌人平滑旋转朝向玩家
            RotateTowardsPlayer(enemy);
            
            // 发现玩家 0.5 秒后杀死玩家
            if (enemy.SeePlayerTimer >= killPlayerDelay)
            {
                //KillPlayer(enemy);
            }
        }
        else
        {
            // ✅ 失去视线，标记为"从警戒状态返回"，然后返回巡逻状态
            enemy.isReturningFromAlert = true;
            enemy.stateMachine.ChangeState(enemy.patrolState, enemy);
        }
    }

    public void Exit(Enemy enemy)
    {
        enemy.SeePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", false);
        
        Debug.Log($"[{enemy.name}] 退出警戒状态");
    }

    /// <summary>
    /// 让敌人平滑旋转朝向玩家
    /// </summary>
    private void RotateTowardsPlayer(Enemy enemy)
    {
        if (enemy.player == null) return;

        // 计算指向玩家的方向（仅在水平面上旋转，忽略Y轴高度差）
        Vector3 directionToPlayer = enemy.player.position - enemy.transform.position;
        directionToPlayer.y = 0f; // 保持水平旋转

        // 如果方向向量太小，跳过旋转（避免抖动）
        if (directionToPlayer.sqrMagnitude < 0.01f)
            return;

        // 计算目标旋转
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer.normalized);

        // 平滑插值旋转
        enemy.transform.rotation = Quaternion.Slerp(
            enemy.transform.rotation, 
            targetRotation, 
            Time.deltaTime * rotationSpeed
        );
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
