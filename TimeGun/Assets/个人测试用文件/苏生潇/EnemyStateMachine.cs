using UnityEngine;

public class EnemyStateMachine
{
    private IEnemyState _currentState; // 当前激活的状态

    public IEnemyState CurrentState
    {
        get => _currentState;
        set => _currentState = value;
    }

    // 切换到新状态（先退出当前状态，再进入新状态）
    public void ChangeState(IEnemyState newState, Enemy enemy)
    {
        CurrentState?.Exit(enemy); // 退出当前状态（?. 避免空引用）
        CurrentState = newState;   // 更新当前状态
        CurrentState?.Enter(enemy); // 进入新状态
    }

    // 每帧更新当前状态的逻辑
    public void Update(Enemy enemy)
    {
        CurrentState?.Update(enemy); // 调用当前状态的Update
    }
}
