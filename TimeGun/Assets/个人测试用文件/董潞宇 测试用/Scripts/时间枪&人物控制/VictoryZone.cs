using UnityEngine;
using UnityEngine.Events;

namespace TimeGun
{
    public class VictoryZone : MonoBehaviour
    {
        public UnityEvent OnVictory;
        private void Start()
        {
            // 订阅交互完成事件
            HoldToInteract.OnInteractionComplete += OnVictoryTriggered;
        }

        private void OnDestroy()
        {
            HoldToInteract.OnInteractionComplete -= OnVictoryTriggered;
        }

        private void OnVictoryTriggered(HoldToInteract interact)
        {
            // 只处理本对象的交互
            if (interact.gameObject != gameObject) return;
            OnVictory?.Invoke();
            Debug.Log("游戏胜利！");

            // 播放胜利音乐
            //AudioManager.PlayVictoryMusic();

            // 显示胜利UI
            // VictoryUI.Instance.Show();

            // 停止玩家控制
            // PlayerController.Instance.enabled = false;
        }
    }
}