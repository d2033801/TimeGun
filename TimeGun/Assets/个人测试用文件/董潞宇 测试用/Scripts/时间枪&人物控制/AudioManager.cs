using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

namespace TimeGun
{
    /// <summary>
    /// 全局音频管理器（Unity 6.2 + DOTween）
    /// 
    /// 功能：
    /// - 管理不同场景的背景音乐（主菜单/游戏中/胜利）
    /// - 暂停菜单时平滑降低音量
    /// - 全局回溯时切换到沉闷音效
    /// - 支持音量淡入淡出
    /// 
    /// 使用场景：
    /// - 挂载在DontDestroyOnLoad的GameObject上（自动单例）
    /// - 通过静态方法控制音乐状态
    /// - 响应游戏状态变化（暂停/回溯/胜利）
    /// 
    /// 依赖：
    /// - DOTween插件（用于平滑音量过渡）
    /// - 可选：AudioMixer（用于更精细的音量控制）
    /// 
    /// 配置建议：
    /// 1. 创建AudioMixer，添加"MasterVolume"、"MusicVolume"暴露参数
    /// 2. 在Inspector中分配不同场景的音乐片段
    /// 3. 订阅全局回溯事件（自动切换音效）
    /// </summary>
    [AddComponentMenu("TimeGun/Audio Manager")]
    public class AudioManager : MonoBehaviour
    {
        #region 单例模式
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("AudioManager");
                        _instance = go.AddComponent<AudioManager>();
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
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        #endregion

        #region 检视器参数
        [Header("音乐片段")]
        [Tooltip("主菜单背景音乐")]
        [SerializeField] private AudioClip mainMenuMusic;

        [Tooltip("游戏中背景音乐")]
        [SerializeField] private AudioClip gameplayMusic;

        [Tooltip("胜利背景音乐")]
        [SerializeField] private AudioClip victoryMusic;

        [Tooltip("回溯音效（沉闷、低音）")]
        [SerializeField] private AudioClip rewindMusic;

        [Header("音量配置")]
        [Tooltip("默认音量"), Range(0f, 1f)]
        [SerializeField] private float defaultVolume = 0.7f;

        [Tooltip("暂停菜单时的音量"), Range(0f, 1f)]
        [SerializeField] private float pauseVolume = 0.3f;

        [Tooltip("回溯时的音量"), Range(0f, 1f)]
        [SerializeField] private float rewindVolume = 0.5f;

        [Header("过渡配置")]
        [Tooltip("音量淡入淡出时间（秒）")]
        [SerializeField] private float fadeTime = 1f;

        [Tooltip("音乐切换交叉淡入淡出时间（秒）")]
        [SerializeField] private float crossfadeTime = 2f;

        [Header("AudioMixer（可选）")]
        [Tooltip("主音频混合器（用于更精细的音量控制）")]
        [SerializeField] private AudioMixer audioMixer;

        [Tooltip("音乐音量参数名（在AudioMixer中暴露的参数）")]
        [SerializeField] private string musicVolumeParameter = "MusicVolume";

        [Header("调试信息")]
        [SerializeField, ReadOnly] private MusicState currentMusicState = MusicState.None;
        [SerializeField, ReadOnly] private bool isPaused = false;
        [SerializeField, ReadOnly] private bool isRewinding = false;
        #endregion

        #region 内部状态
        /// <summary>
        /// 音乐状态枚举
        /// </summary>
        private enum MusicState
        {
            None,
            MainMenu,
            Gameplay,
            Victory,
            Rewind
        }

        private AudioSource _mainAudioSource;    // 主音乐源
        private AudioSource _secondAudioSource;  // 用于交叉淡入淡出的第二音源
        private AudioSource _rewindAudioSource;  // 回溯音效专用
        private Coroutine _currentTransition;    // 当前正在进行的过渡协程
        private float _originalVolume;           // 暂停前的原始音量
        #endregion

        #region 公共接口 - 音乐控制
        /// <summary>
        /// 播放主菜单音乐
        /// </summary>
        public static void PlayMainMenuMusic()
        {
            Instance.PlayMusic(MusicState.MainMenu, Instance.mainMenuMusic);
        }

        /// <summary>
        /// 播放游戏中音乐
        /// </summary>
        public static void PlayGameplayMusic()
        {
            Instance.PlayMusic(MusicState.Gameplay, Instance.gameplayMusic);
        }

        /// <summary>
        /// 播放胜利音乐
        /// </summary>
        public static void PlayVictoryMusic()
        {
            Instance.PlayMusic(MusicState.Victory, Instance.victoryMusic);
        }

        /// <summary>
        /// 停止所有音乐
        /// </summary>
        public static void StopAllMusic()
        {
            Instance.StopMusic();
        }
        #endregion

        #region 公共接口 - 状态控制
        /// <summary>
        /// 进入暂停状态（音量降低）
        /// </summary>
        public static void EnterPauseState()
        {
            if (Instance.isPaused) return;
            Instance.isPaused = true;

            Instance._originalVolume = Instance._mainAudioSource.volume;
            Instance._mainAudioSource.DOFade(Instance.pauseVolume, Instance.fadeTime);
        }

        /// <summary>
        /// 退出暂停状态（音量恢复）
        /// </summary>
        public static void ExitPauseState()
        {
            if (!Instance.isPaused) return;
            Instance.isPaused = false;

            Instance._mainAudioSource.DOFade(Instance._originalVolume, Instance.fadeTime);
        }

        /// <summary>
        /// 设置主音量（0-1）
        /// </summary>
        public static void SetMasterVolume(float volume)
        {
            if (Instance.audioMixer != null)
            {
                // 使用AudioMixer（dB转换：线性0-1 -> dB）
                float db = volume > 0 ? 20f * Mathf.Log10(volume) : -80f;
                Instance.audioMixer.SetFloat(Instance.musicVolumeParameter, db);
            }
            else
            {
                // 直接控制AudioSource
                Instance._mainAudioSource.volume = volume;
            }
        }
        #endregion

        #region 内部逻辑 - 音乐播放
        private void InitializeAudioSources()
        {
            // 主音乐源
            _mainAudioSource = gameObject.AddComponent<AudioSource>();
            _mainAudioSource.loop = true;
            _mainAudioSource.playOnAwake = false;
            _mainAudioSource.volume = defaultVolume;

            // 第二音源（用于交叉淡入淡出）
            _secondAudioSource = gameObject.AddComponent<AudioSource>();
            _secondAudioSource.loop = true;
            _secondAudioSource.playOnAwake = false;
            _secondAudioSource.volume = 0f;

            // 回溯音源
            _rewindAudioSource = gameObject.AddComponent<AudioSource>();
            _rewindAudioSource.loop = true;
            _rewindAudioSource.playOnAwake = false;
            _rewindAudioSource.volume = 0f;
            _rewindAudioSource.clip = rewindMusic;

            // 如果有AudioMixer，分配给所有AudioSource
            if (audioMixer != null)
            {
                var musicGroup = audioMixer.FindMatchingGroups("Music");
                if (musicGroup != null && musicGroup.Length > 0)
                {
                    _mainAudioSource.outputAudioMixerGroup = musicGroup[0];
                    _secondAudioSource.outputAudioMixerGroup = musicGroup[0];
                    _rewindAudioSource.outputAudioMixerGroup = musicGroup[0];
                }
            }
        }

        private void PlayMusic(MusicState state, AudioClip clip)
        {
            if (currentMusicState == state && _mainAudioSource.isPlaying) return;
            if (clip == null) 
            {
                Debug.LogWarning($"[AudioManager] 未配置 {state} 的音乐片段");
                return;
            }

            currentMusicState = state;

            // 如果正在过渡，停止旧的协程
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(CrossfadeMusic(clip));
        }

        private void StopMusic()
        {
            currentMusicState = MusicState.None;

            _mainAudioSource.DOFade(0f, fadeTime).OnComplete(() => _mainAudioSource.Stop());
            _secondAudioSource.DOFade(0f, fadeTime).OnComplete(() => _secondAudioSource.Stop());
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            // 如果主音源正在播放，交叉淡入淡出
            if (_mainAudioSource.isPlaying)
            {
                _secondAudioSource.clip = newClip;
                _secondAudioSource.Play();
                _secondAudioSource.DOFade(defaultVolume, crossfadeTime);
                _mainAudioSource.DOFade(0f, crossfadeTime);

                yield return new WaitForSeconds(crossfadeTime);

                _mainAudioSource.Stop();
                (_mainAudioSource, _secondAudioSource) = (_secondAudioSource, _mainAudioSource);
            }
            else
            {
                // 直接播放
                _mainAudioSource.clip = newClip;
                _mainAudioSource.volume = 0f;
                _mainAudioSource.Play();
                _mainAudioSource.DOFade(defaultVolume, fadeTime);
            }

            _currentTransition = null;
        }
        #endregion

        #region 事件订阅 - 全局回溯
        private void SubscribeToEvents()
        {
            // 订阅全局回溯管理器的事件（如果存在）
            if (TimeRewind.GlobalTimeRewindManager.Instance != null)
            {
                // 注意：这里需要你在GlobalTimeRewindManager中添加回溯开始/结束事件
                // 临时方案：通过轮询检测（见Update方法）
            }
        }

        private void UnsubscribeFromEvents()
        {
            // 取消订阅事件
        }

        private void Update()
        {
            // 轮询检测全局回溯状态（临时方案，建议改为事件驱动）
            if (TimeRewind.GlobalTimeRewindManager.Instance != null)
            {
                bool isGlobalRewinding = TimeRewind.GlobalTimeRewindManager.Instance.IsGlobalRewinding;

                if (isGlobalRewinding && !isRewinding)
                {
                    // 进入回溯状态
                    EnterRewindState();
                }
                else if (!isGlobalRewinding && isRewinding)
                {
                    // 退出回溯状态
                    ExitRewindState();
                }
            }
        }

        private void EnterRewindState()
        {
            isRewinding = true;

            // 主音乐降低音量
            _mainAudioSource.DOFade(rewindVolume * 0.3f, fadeTime);

            // 回溯音效淡入
            if (_rewindAudioSource.clip != null)
            {
                if (!_rewindAudioSource.isPlaying)
                {
                    _rewindAudioSource.Play();
                }
                _rewindAudioSource.DOFade(rewindVolume, fadeTime);
            }
        }

        private void ExitRewindState()
        {
            isRewinding = false;

            // 回溯音效淡出
            _rewindAudioSource.DOFade(0f, fadeTime).OnComplete(() => _rewindAudioSource.Stop());

            // 主音乐恢复音量
            float targetVolume = isPaused ? pauseVolume : defaultVolume;
            _mainAudioSource.DOFade(targetVolume, fadeTime);
        }
        #endregion
    }
}
