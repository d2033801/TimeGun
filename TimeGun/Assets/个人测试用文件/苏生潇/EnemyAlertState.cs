using UnityEngine;

public class EnemyAlertState : IEnemyState
{
    private float seePlayerTimer;
    private const float attack_Threshold = 2.0f;
    public void Enter(Enemy enemy)
    {
        enemy.navMeshAgent.isStopped = true;
        seePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", true);
    }

    public void Update(Enemy enemy)
    {
        bool canSee = enemy.CanSeePlayer();

        if (canSee)
        {
            seePlayerTimer += Time.deltaTime;
            if (seePlayerTimer >= attack_Threshold)
            {
                // enemy.stateMachine.ChangeState(enemy.attackState, enemy);
                seePlayerTimer = 0f;
            }
        }
        else
        {
            enemy.stateMachine.ChangeState(enemy.patrolState, enemy);
        }
    }

    public void Exit(Enemy enemy)
    {
        seePlayerTimer = 0f;
        enemy.modelAnimator?.SetBool("isAiming", false);
    }
}
