using UnityEngine;

public class EnemyStateMachine
{
    private IEnemyState _currentState; // ��ǰ�����״̬

    // �л�����״̬�����˳���ǰ״̬���ٽ�����״̬��
    public void ChangeState(IEnemyState newState, Enemy enemy)
    {
        _currentState?.Exit(enemy); // �˳���ǰ״̬��?. ��������ã�
        _currentState = newState;   // ���µ�ǰ״̬
        _currentState?.Enter(enemy); // ������״̬
    }

    // ÿ֡���µ�ǰ״̬���߼�
    public void Update(Enemy enemy)
    {
        _currentState?.Update(enemy); // ���õ�ǰ״̬��Update
    }
}
