using UnityEngine;

public class EnemyAlertState : IEnemyState
{
    private const float attack_Threshold = 2.0f;
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = true;
        enemy.seePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", true);
    }

    public void Update(Enemy enemy)
    {
        bool canSee = enemy.CanSeePlayer();

        if (canSee)
        {
            enemy.seePlayerTimer += Time.deltaTime;
            if (enemy.seePlayerTimer >= attack_Threshold)
            {
                // enemy.stateMachine.ChangeState(enemy.attackState, enemy);
                enemy.seePlayerTimer = 0f;
            }
        }
        else
        {
            enemy.stateMachine.ChangeState(enemy.patrolState, enemy);
        }
    }

    public void Exit(Enemy enemy)
    {
        enemy.seePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", false);
    }
}
