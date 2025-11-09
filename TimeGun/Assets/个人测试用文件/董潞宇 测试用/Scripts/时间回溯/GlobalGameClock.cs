using UnityEngine;
using Utility;

namespace TimeRewind
{
    /// <summary>
    /// 全局游戏时钟（可被时间回溯）
    /// 功能：
    /// - 记录游戏经过的分钟和秒数
    /// - 支持时间回溯（使用 AbstractTimeRewindObject 的快照机制）
    /// - 提供公共接口供UI显示
    /// </summary>
    [AddComponentMenu("TimeRewind/Global Game Clock")]
    public class GlobalGameClock : AbstractTimeRewindObject
    {
        #region 单例模式
        private static GlobalGameClock _instance;
        public static GlobalGameClock Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GlobalGameClock>();
                    if (_instance == null)
                    {
                        var go = new GameObject("GlobalGameClock");
                        _instance = go.AddComponent<GlobalGameClock>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            MainInit();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region 公共接口
        /// <summary>
        /// 当前经过的总秒数（只读）
        /// </summary>
        public float TotalSeconds => _totalSeconds;

        /// <summary>
        /// 当前分钟数（只读）
        /// </summary>
        public int Minutes => Mathf.FloorToInt(_totalSeconds / 60f);

        /// <summary>
        /// 当前秒数（只读，0-59）
        /// </summary>
        public int Seconds => Mathf.FloorToInt(_totalSeconds % 60f);

        /// <summary>
        /// 格式化的时间字符串（MM:SS）
        /// </summary>
        public string FormattedTime => $"{Minutes:D2}:{Seconds:D2}";

        /// <summary>
        /// 重置时钟到0
        /// </summary>
        public void ResetClock()
        {
            _totalSeconds = 0f;
        }
        #endregion

        #region 内部状态
        /// <summary>
        /// 游戏经过的总秒数
        /// </summary>
        private float _totalSeconds = 0f;

        /// <summary>
        /// 时间快照历史（用于回溯）
        /// </summary>
        private RingBuffer<TimeSnapshot> _timeHistory;

        /// <summary>
        /// 时间快照结构
        /// </summary>
        private struct TimeSnapshot
        {
            public float TotalSeconds;
        }
        #endregion

        #region 初始化
        protected override void MainInit()
        {
            base.MainInit();
            _timeHistory = RewindInit<TimeSnapshot>(out _);
        }
        #endregion

        #region 更新逻辑（重写父类方法）
        protected override void FixedUpdate()
        {
            // 暂停或回溯时不累加时间
            if (IsPaused || isRewinding)
            {
                base.FixedUpdate();
                return;
            }

            // 正常录制模式：累加时间
            _totalSeconds += Time.fixedDeltaTime;

            base.FixedUpdate();
        }
        #endregion

        #region 快照记录与回溯（重写父类方法）
        protected override void RecordOneSnap()
        {
            base.RecordOneSnap();

            var snap = new TimeSnapshot
            {
                TotalSeconds = _totalSeconds
            };
            _timeHistory.Push(snap);
        }

        protected override void RewindOneSnap()
        {
            base.RewindOneSnap();

            var snap = _timeHistory.PopBack();
            _totalSeconds = snap.TotalSeconds;
        }
        #endregion

        #region 调试信息
        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            // 右上角显示时钟
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };

            int screenWidth = Screen.width; // 获取屏幕宽度
            GUI.Label(new Rect(screenWidth - 210, 10, 200, 30), $"游戏时间: {FormattedTime}", style);
        }
        #endregion
    }
}
