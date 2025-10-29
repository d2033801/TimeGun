using UnityEngine;

public class EnemyStateMachine
{
    private IEnemyState _currentState; // 当前激活的状态

    // 切换到新状态（先退出当前状态，再进入新状态）
    public void ChangeState(IEnemyState newState, Enemy enemy)
    {
        _currentState?.Exit(enemy); // 退出当前状态（?. 避免空引用）
        _currentState = newState;   // 更新当前状态
        _currentState?.Enter(enemy); // 进入新状态
    }

    // 每帧更新当前状态的逻辑
    public void Update(Enemy enemy)
    {
        _currentState?.Update(enemy); // 调用当前状态的Update
    }
}
