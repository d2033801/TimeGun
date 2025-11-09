using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

namespace TimeGun
{
    /// <summary>
    /// 全局音频管理器（Unity 6.2 + DOTween + AudioMixer）
    /// 
    /// 功能：
    /// - 管理不同场景的背景音乐（主菜单/游戏中/胜利）
    /// - 暂停菜单时平滑降低音量
    /// - 全局回溯时对BGM应用动态音效处理（低通滤波+音调变换）
    /// - 支持音量淡入淡出和交叉淡入淡出
    /// 
    /// 回溯音效实现方式：
    /// - 对当前播放的BGM应用实时音效处理
    /// - 无需切换音轨，保持音乐连贯性
    /// - 使用AudioMixer效果器（Lowpass + Pitch Shifter）
    /// 
    /// 使用场景：
    /// - 挂载在DontDestroyOnLoad的GameObject上（自动单例）
    /// - 通过静态方法控制音乐状态
    /// - 响应游戏状态变化（暂停/回溯/胜利）
    /// 
    /// 依赖：
    /// - DOTween插件（用于平滑音量过渡）
    /// - AudioMixer Asset（用于动态音效处理，可选）
    /// 
    /// 配置步骤：
    /// 1. 创建AudioMixer，配置Music/BackgroundMusic分组（可选）
    /// 2. 为BackgroundMusic组添加Lowpass、Pitch Shifter效果器（可选）
    /// 3. 暴露参数：MusicVolume、BGMVolume、BGMLowpassCutoff、BGMPitch（可选）
    /// 4. 在Inspector中分配音乐片段（必需）
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
            ValidateAudioMixerSetup();
            SubscribeToEvents();
        }

        private void Start()
        {
            Debug.Log("[AudioManager] ==================== Start() 开始 ====================");
            Debug.Log($"[AudioManager] Gameplay Music: {(gameplayMusic != null ? gameplayMusic.name : "NULL")}");
            Debug.Log($"[AudioManager] Main Audio Source: {(_mainAudioSource != null ? "已创建" : "NULL")}");
            Debug.Log($"[AudioManager] Audio Mixer Configured: {_audioMixerConfigured}");
            Debug.Log($"[AudioManager] Default Volume: {defaultVolume}");
            
            PlayGameplayMusic();
            
            Debug.Log("[AudioManager] ==================== Start() 结束 ====================");
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

        [Header("音量配置")]
        [Tooltip("默认音量"), Range(0f, 1f)]
        [SerializeField] private float defaultVolume = 0.7f;

        [Tooltip("暂停菜单时的音量"), Range(0f, 1f)]
        [SerializeField] private float pauseVolume = 0.3f;

        [Tooltip("回溯时背景音乐音量"), Range(0f, 1f)]
        [SerializeField] private float rewindBGMVolume = 0.5f;

        [Header("过渡配置")]
        [Tooltip("音量淡入淡出时间（秒）")]
        [SerializeField] private float fadeTime = 1f;

        [Tooltip("音乐切换交叉淡入淡出时间（秒）")]
        [SerializeField] private float crossfadeTime = 2f;

        [Header("AudioMixer配置（可选）")]
        [Tooltip("主音频混合器 - 用于动态音效处理（可选）")]
        [SerializeField] private AudioMixer audioMixer;

        [Tooltip("主音量参数名")]
        [SerializeField] private string masterVolumeParameter = "MasterVolume";

        [Tooltip("音乐音量参数名")]
        [SerializeField] private string musicVolumeParameter = "MusicVolume";

        [Tooltip("背景音乐音量参数名")]
        [SerializeField] private string bgmVolumeParameter = "BGMVolume";

        [Tooltip("背景音乐低通滤波器频率参数名（关键效果）")]
        [SerializeField] private string bgmLowpassParameter = "BGMLowpassCutoff";

        [Tooltip("背景音乐音调参数名（可选）")]
        [SerializeField] private string bgmPitchParameter = "BGMPitch";

        [Header("回溯音效参数")]
        [Tooltip("回溯时的低通滤波器截止频率（Hz）- 越低越沉闷")]
        [SerializeField, Range(100f, 5000f)] private float rewindLowpassCutoff = 800f;

        [Tooltip("回溯时的音调（0.5 = 低一个八度）")]
        [SerializeField, Range(0.5f, 1.5f)] private float rewindPitch = 0.85f;

        [Tooltip("正常状态的低通滤波器频率（全频段）")]
        [SerializeField] private float normalLowpassCutoff = 22000f;

        [Tooltip("正常状态的音调")]
        [SerializeField] private float normalPitch = 1f;

        [Header("调试工具")]
        [Tooltip("启用调试模式（实时调整参数）")]
        [SerializeField] private bool enableDebugMode = false;

        [Tooltip("调试：回溯强度（0=无效果，1=完全效果）")]
        [SerializeField, Range(0f, 1f)] private float debugRewindIntensity = 1f;

        [Header("调试信息")]
        [SerializeField, ReadOnly] private MusicState currentMusicState = MusicState.None;
        [SerializeField, ReadOnly] private bool isPaused = false;
        [SerializeField, ReadOnly] private bool isRewinding = false;
        [SerializeField, ReadOnly] private bool audioMixerValid = false;
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
            Victory
        }

        private AudioSource _mainAudioSource;    // 主音乐源（背景音乐）
        private AudioSource _secondAudioSource;  // 用于交叉淡入淡出的第二音源
        private Coroutine _currentTransition;    // 当前正在进行的过渡协程
        private float _originalVolume;           // 暂停前的原始音量
        
        private bool _audioMixerConfigured = false;
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
            
            if (Instance._audioMixerConfigured)
            {
                float currentDB;
                Instance.audioMixer.GetFloat(Instance.bgmVolumeParameter, out currentDB);
                float targetDB = Instance.LinearToDecibel(Instance.pauseVolume);
                DOTween.To(() => currentDB, x => Instance.audioMixer.SetFloat(Instance.bgmVolumeParameter, x), 
                    targetDB, Instance.fadeTime);
            }
            else
            {
                Instance._mainAudioSource.DOFade(Instance.pauseVolume, Instance.fadeTime);
            }
        }

        /// <summary>
        /// 退出暂停状态（音量恢复）
        /// </summary>
        public static void ExitPauseState()
        {
            if (!Instance.isPaused) return;
            Instance.isPaused = false;

            if (Instance._audioMixerConfigured)
            {
                float currentDB;
                Instance.audioMixer.GetFloat(Instance.bgmVolumeParameter, out currentDB);
                float targetDB = Instance.LinearToDecibel(Instance._originalVolume);
                DOTween.To(() => currentDB, x => Instance.audioMixer.SetFloat(Instance.bgmVolumeParameter, x), 
                    targetDB, Instance.fadeTime);
            }
            else
            {
                Instance._mainAudioSource.DOFade(Instance._originalVolume, Instance.fadeTime);
            }
        }

        /// <summary>
        /// 设置主音量（0-1）
        /// </summary>
        public static void SetMasterVolume(float volume)
        {
            if (Instance._audioMixerConfigured)
            {
                float db = Instance.LinearToDecibel(volume);
                Instance.audioMixer.SetFloat(Instance.masterVolumeParameter, db);
            }
            else
            {
                Instance._mainAudioSource.volume = volume;
            }
        }

        /// <summary>
        /// 设置回溯音效强度（0-1，用于动态调整）
        /// </summary>
        public static void SetRewindIntensity(float intensity)
        {
            if (!Instance._audioMixerConfigured || !Instance.isRewinding) return;
            
            intensity = Mathf.Clamp01(intensity);
            
            // 动态插值滤波器频率
            float targetCutoff = Mathf.Lerp(Instance.normalLowpassCutoff, Instance.rewindLowpassCutoff, intensity);
            Instance.audioMixer.SetFloat(Instance.bgmLowpassParameter, targetCutoff);
            
            // 动态插值音调
            float targetPitch = Mathf.Lerp(Instance.normalPitch, Instance.rewindPitch, intensity);
            Instance.audioMixer.SetFloat(Instance.bgmPitchParameter, targetPitch);
            
            // 动态音量（可选）
            float targetVolume = Mathf.Lerp(Instance.defaultVolume, Instance.rewindBGMVolume, intensity);
            float targetDB = Instance.LinearToDecibel(targetVolume);
            Instance.audioMixer.SetFloat(Instance.bgmVolumeParameter, targetDB);
        }
        #endregion

        #region 内部逻辑 - 音乐播放
        private void InitializeAudioSources()
        {
            // 主音乐源（背景音乐）
            _mainAudioSource = gameObject.AddComponent<AudioSource>();
            _mainAudioSource.loop = true;
            _mainAudioSource.playOnAwake = false;
            _mainAudioSource.volume = defaultVolume;

            // 第二音源（用于交叉淡入淡出）
            _secondAudioSource = gameObject.AddComponent<AudioSource>();
            _secondAudioSource.loop = true;
            _secondAudioSource.playOnAwake = false;
            _secondAudioSource.volume = 0f;

            // 分配AudioMixer Group
            if (audioMixer != null)
            {
                var bgmGroups = audioMixer.FindMatchingGroups("BackgroundMusic");
                if (bgmGroups != null && bgmGroups.Length > 0)
                {
                    _mainAudioSource.outputAudioMixerGroup = bgmGroups[0];
                    _secondAudioSource.outputAudioMixerGroup = bgmGroups[0];
                    Debug.Log($"[AudioManager] BGM Mixer Group 已分配: {bgmGroups[0].name}");
                }
                else
                {
                    Debug.LogWarning("[AudioManager] 未找到 'BackgroundMusic' Mixer Group，将使用默认输出");
                }
            }
        }

        private void ValidateAudioMixerSetup()
        {
            if (audioMixer == null)
            {
                Debug.LogWarning("[AudioManager] 未分配AudioMixer，将使用基础音量控制（无法实现回溯音效）");
                audioMixerValid = false;
                _audioMixerConfigured = false;
                return;
            }

            float testValue;
            bool allValid = true;

            // 验证必需参数
            if (!audioMixer.GetFloat(bgmVolumeParameter, out testValue))
            {
                Debug.LogError($"[AudioManager] 参数 '{bgmVolumeParameter}' 不存在！请在AudioMixer中暴露此参数。");
                allValid = false;
            }

            if (!audioMixer.GetFloat(bgmLowpassParameter, out testValue))
            {
                Debug.LogWarning($"[AudioManager] 参数 '{bgmLowpassParameter}' 不存在，回溯滤波效果将无法使用。");
            }

            if (!audioMixer.GetFloat(bgmPitchParameter, out testValue))
            {
                Debug.LogWarning($"[AudioManager] 参数 '{bgmPitchParameter}' 不存在，回溯音调变换将无法使用。");
            }

            audioMixerValid = allValid;
            _audioMixerConfigured = allValid;

            if (allValid)
            {
                Debug.Log("[AudioManager] ✅ AudioMixer 配置验证成功！");
            }
            else
            {
                Debug.LogError("[AudioManager] ❌ AudioMixer 配置不完整，部分功能将无法使用。");
            }
        }

        private void PlayMusic(MusicState state, AudioClip clip)
        {
            Debug.Log($"[AudioManager] PlayMusic() 调用 - State: {state}, Clip: {(clip != null ? clip.name : "NULL")}");
            Debug.Log($"[AudioManager] 当前状态: {currentMusicState}, 主音源播放中: {_mainAudioSource.isPlaying}");
            
            if (currentMusicState == state && _mainAudioSource.isPlaying)
            {
                Debug.LogWarning($"[AudioManager] 已经在播放 {state} 音乐，跳过");
                return;
            }
            
            if (clip == null) 
            {
                Debug.LogError($"[AudioManager] ❌ 未配置 {state} 的音乐片段！请在 Inspector 中分配音频文件。");
                return;
            }

            currentMusicState = state;
            Debug.Log($"[AudioManager] ✅ 开始播放 {state} 音乐: {clip.name}");

            // 如果正在过渡，停止旧的协程
            if (_currentTransition != null)
            {
                Debug.Log("[AudioManager] 停止旧的过渡协程");
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(CrossfadeMusic(clip));
        }

        private void StopMusic()
        {
            currentMusicState = MusicState.None;

            if (_audioMixerConfigured)
            {
                float currentDB;
                audioMixer.GetFloat(bgmVolumeParameter, out currentDB);
                DOTween.To(() => currentDB, x => audioMixer.SetFloat(bgmVolumeParameter, x), 
                    -80f, fadeTime).OnComplete(() =>
                {
                    _mainAudioSource.Stop();
                    _secondAudioSource.Stop();
                });
            }
            else
            {
                _mainAudioSource.DOFade(0f, fadeTime).OnComplete(() => _mainAudioSource.Stop());
                _secondAudioSource.DOFade(0f, fadeTime).OnComplete(() => _secondAudioSource.Stop());
            }
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            Debug.Log($"[AudioManager] CrossfadeMusic() 协程开始 - Clip: {newClip.name}");
            
            // 如果主音源正在播放，交叉淡入淡出
            if (_mainAudioSource.isPlaying)
            {
                Debug.Log("[AudioManager] 主音源正在播放，执行交叉淡入淡出");
                _secondAudioSource.clip = newClip;
                _secondAudioSource.Play();
                
                if (_audioMixerConfigured)
                {
                    float currentDB;
                    audioMixer.GetFloat(bgmVolumeParameter, out currentDB);
                    float targetDB = LinearToDecibel(defaultVolume);
                    Debug.Log($"[AudioManager] AudioMixer 交叉淡入淡出: {currentDB} dB → {targetDB} dB");
                    
                    DOTween.Sequence()
                        .Append(DOTween.To(() => currentDB, x => audioMixer.SetFloat(bgmVolumeParameter, x), 
                            -80f, crossfadeTime))
                        .Join(DOTween.To(() => -80f, x => audioMixer.SetFloat(bgmVolumeParameter, x), 
                            targetDB, crossfadeTime).SetDelay(crossfadeTime * 0.5f));
                }
                else
                {
                    Debug.Log("[AudioManager] 使用 AudioSource 音量淡入淡出");
                    _secondAudioSource.DOFade(defaultVolume, crossfadeTime);
                    _mainAudioSource.DOFade(0f, crossfadeTime);
                }

                yield return new WaitForSeconds(crossfadeTime);

                _mainAudioSource.Stop();
                (_mainAudioSource, _secondAudioSource) = (_secondAudioSource, _mainAudioSource);
                Debug.Log("[AudioManager] 交叉淡入淡出完成，交换音源");
            }
            else
            {
                // 直接播放
                Debug.Log($"[AudioManager] 主音源未播放，直接播放新音乐: {newClip.name}");
                _mainAudioSource.clip = newClip;
                _mainAudioSource.Play();
                
                Debug.Log($"[AudioManager] AudioSource 状态: Playing={_mainAudioSource.isPlaying}, Clip={_mainAudioSource.clip.name}");
                
                if (_audioMixerConfigured)
                {
                    float targetDB = LinearToDecibel(defaultVolume);
                    Debug.Log($"[AudioManager] 使用 AudioMixer 淡入: -80 dB → {targetDB} dB");
                    
                    // 先设置初始音量为-80dB
                    audioMixer.SetFloat(bgmVolumeParameter, -80f);
                    
                    DOTween.To(() => -80f, x => 
                    {
                        audioMixer.SetFloat(bgmVolumeParameter, x);
                        Debug.Log($"[AudioManager] AudioMixer 音量: {x} dB");
                    }, targetDB, fadeTime)
                    .OnComplete(() => Debug.Log($"[AudioManager] ✅ AudioMixer 淡入完成"));
                }
                else
                {
                    Debug.Log($"[AudioManager] 使用 AudioSource 音量淡入: 0 → {defaultVolume}");
                    _mainAudioSource.volume = 0f;
                    _mainAudioSource.DOFade(defaultVolume, fadeTime)
                        .OnUpdate(() => Debug.Log($"[AudioManager] AudioSource 音量: {_mainAudioSource.volume}"))
                        .OnComplete(() => Debug.Log($"[AudioManager] ✅ 音量淡入完成: {_mainAudioSource.volume}"));
                }
            }

            _currentTransition = null;
            Debug.Log("[AudioManager] CrossfadeMusic() 协程结束");
        }
        #endregion

        #region 事件订阅 - 全局回溯
        private void SubscribeToEvents()
        {
        }

        private void UnsubscribeFromEvents()
        {
        }

        private void Update()
        {
            // 调试模式：实时调整参数
            if (enableDebugMode && isRewinding && _audioMixerConfigured)
            {
                SetRewindIntensity(debugRewindIntensity);
            }

            // 轮询检测全局回溯状态
            if (TimeRewind.GlobalTimeRewindManager.Instance != null)
            {
                bool isGlobalRewinding = TimeRewind.GlobalTimeRewindManager.Instance.IsGlobalRewinding;

                if (isGlobalRewinding && !isRewinding)
                {
                    EnterRewindState();
                }
                else if (!isGlobalRewinding && isRewinding)
                {
                    ExitRewindState();
                }
            }
        }

        private void EnterRewindState()
        {
            isRewinding = true;
            Debug.Log("[AudioManager] 🔄 进入回溯状态 - 对BGM应用音效处理");

            if (_audioMixerConfigured)
            {
                // 降低BGM音量
                float currentDB;
                audioMixer.GetFloat(bgmVolumeParameter, out currentDB);
                float targetDB = LinearToDecibel(rewindBGMVolume);
                DOTween.To(() => currentDB, x => audioMixer.SetFloat(bgmVolumeParameter, x), 
                    targetDB, fadeTime);

                // 应用低通滤波器（沉闷效果）
                float currentCutoff;
                audioMixer.GetFloat(bgmLowpassParameter, out currentCutoff);
                DOTween.To(() => currentCutoff, x => audioMixer.SetFloat(bgmLowpassParameter, x), 
                    rewindLowpassCutoff, fadeTime)
                    .OnComplete(() => Debug.Log($"[AudioManager] 低通滤波器已设置为 {rewindLowpassCutoff} Hz"));

                // 应用音调变换（低沉效果）
                float currentPitch;
                if (audioMixer.GetFloat(bgmPitchParameter, out currentPitch))
                {
                    DOTween.To(() => currentPitch, x => audioMixer.SetFloat(bgmPitchParameter, x), 
                        rewindPitch, fadeTime)
                        .OnComplete(() => Debug.Log($"[AudioManager] 音调已设置为 {rewindPitch}"));
                }
            }
            else
            {
                Debug.LogWarning("[AudioManager] AudioMixer未配置，只能降低音量");
                _mainAudioSource.DOFade(rewindBGMVolume, fadeTime);
            }
        }

        private void ExitRewindState()
        {
            isRewinding = false;
            Debug.Log("[AudioManager] ⏹️ 退出回溯状态 - 恢复BGM正常播放");

            if (_audioMixerConfigured)
            {
                // 恢复BGM音量
                float targetVolume = isPaused ? pauseVolume : defaultVolume;
                float targetDB = LinearToDecibel(targetVolume);
                float currentDB;
                audioMixer.GetFloat(bgmVolumeParameter, out currentDB);
                DOTween.To(() => currentDB, x => audioMixer.SetFloat(bgmVolumeParameter, x), 
                    targetDB, fadeTime);

                // 恢复低通滤波器（全频段）
                float currentCutoff;
                audioMixer.GetFloat(bgmLowpassParameter, out currentCutoff);
                DOTween.To(() => currentCutoff, x => audioMixer.SetFloat(bgmLowpassParameter, x), 
                    normalLowpassCutoff, fadeTime)
                    .OnComplete(() => Debug.Log($"[AudioManager] 低通滤波器已恢复为 {normalLowpassCutoff} Hz"));

                // 恢复正常音调
                float currentPitch;
                if (audioMixer.GetFloat(bgmPitchParameter, out currentPitch))
                {
                    DOTween.To(() => currentPitch, x => audioMixer.SetFloat(bgmPitchParameter, x), 
                        normalPitch, fadeTime)
                        .OnComplete(() => Debug.Log($"[AudioManager] 音调已恢复为 {normalPitch}"));
                }
            }
            else
            {
                float targetVolume = isPaused ? pauseVolume : defaultVolume;
                _mainAudioSource.DOFade(targetVolume, fadeTime);
            }
        }
        #endregion

        #region 辅助方法 - AudioMixer控制
        /// <summary>
        /// 线性音量（0-1）转换为分贝（-80 to 0）
        /// </summary>
        private float LinearToDecibel(float linear)
        {
            return linear > 0 ? 20f * Mathf.Log10(linear) : -80f;
        }

        /// <summary>
        /// 分贝转换为线性音量
        /// </summary>
        private float DecibelToLinear(float decibel)
        {
            return Mathf.Pow(10f, decibel / 20f);
        }
        #endregion
    }
}
