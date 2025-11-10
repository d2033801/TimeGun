using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

namespace TimeGun
{
    /// <summary>
    /// 全局音频管理器 - 完全重构版本
    /// 
    /// 核心设计理念：
    /// - AudioSource 负责播放和音量控制
    /// - AudioMixer 仅负责音效处理（滤波、音调）
    /// - 清晰的状态管理：Normal / Paused / Rewinding
    /// - 使用 Unscaled Time：不受 Time.timeScale 影响
    /// 
    /// Unity 6.2 + URP + DOTween
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

            InitializeAudioSource();
            InitializeMixer();
        }

        private void OnDestroy()
        {
            _volumeTween?.Kill();
            _mixerTween?.Kill();
        }
        #endregion

        #region Inspector 配置
        [Header("音乐片段")]
        [SerializeField] private AudioClip mainMenuMusic;
        [SerializeField] private AudioClip gameplayMusic;
        [SerializeField] private AudioClip victoryMusic;

        [Header("音量设置")]
        [SerializeField, Range(0f, 1f)] private float normalVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float pauseVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float victoryVolume = 1.0f;
        [SerializeField, Range(0f, 1f)] private float rewindVolume = 0.5f;

        [Header("过渡时间")]
        [SerializeField] private float volumeFadeTime = 0.5f;
        [SerializeField] private float musicCrossfadeTime = 1f;

        [Header("AudioMixer（可选 - 用于回溯效果）")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string bgmGroupName = "BackgroundMusic";
        [SerializeField] private string effectsGroupName = "RewindEffects";
        [SerializeField] private string bgmLowpassParameter = "BGMLowpassCutoff";
        [SerializeField] private string bgmPitchParameter = "BGMPitch";
        [SerializeField] private string bgmEchoParameter = "BGMEchoWetMix";

        [Header("回溯效果参数")]
        [SerializeField, Range(100f, 5000f)] private float rewindLowpassCutoff = 800f;
        [SerializeField, Range(0.5f, 1.5f)] private float rewindPitch = 0.85f;
        [SerializeField, Range(0f, 1f)] private float rewindEchoWetMix = 0.3f;
        [SerializeField] private float normalLowpassCutoff = 22000f;
        [SerializeField] private float normalPitch = 1f;
        [SerializeField] private float normalEchoWetMix = 0f;

        [Header("调试")]
        [SerializeField] private bool showDebugLogs = true;
        #endregion

        #region 内部状态
        private enum MusicState { None, MainMenu, Gameplay, Victory }
        private enum VolumeState { Normal, Paused, Rewinding }

        private AudioSource _audioSource;
        private MusicState _currentMusic = MusicState.None;
        private VolumeState _volumeState = VolumeState.Normal;
        
        private bool _hasMixer = false;
        private Tween _volumeTween;
        private Tween _mixerTween;
        #endregion

        #region 初始化
        private void InitializeAudioSource()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.volume = normalVolume;
        }

        private void InitializeMixer()
        {
            if (audioMixer == null)
            {
                Log("未配置 AudioMixer，回溯效果将不可用");
                return;
            }

            // 查找 BGM 输出组（用于音频播放）
            var bgmGroups = audioMixer.FindMatchingGroups(bgmGroupName);
            if (bgmGroups != null && bgmGroups.Length > 0)
            {
                _audioSource.outputAudioMixerGroup = bgmGroups[0];
                _hasMixer = true;
                
                // 初始化 Mixer 参数到正常状态
                ResetMixerToNormal();
                
                Log($"AudioMixer 配置成功: {bgmGroups[0].name}");
                
                // 检查是否有独立的效果组
                var effectGroups = audioMixer.FindMatchingGroups(effectsGroupName);
                if (effectGroups != null && effectGroups.Length > 0)
                {
                    Log($"检测到独立效果组: {effectGroups[0].name}（推荐架构）");
                }
            }
            else
            {
                LogWarning($"未找到 '{bgmGroupName}' Mixer Group");
            }
        }
        #endregion

        #region 公共接口 - 音乐播放
        public static void PlayMainMenuMusic() => Instance.PlayMusic(MusicState.MainMenu, Instance.mainMenuMusic);
        public static void PlayGameplayMusic() => Instance.PlayMusic(MusicState.Gameplay, Instance.gameplayMusic);
        public static void PlayVictoryMusic() => Instance.PlayMusic(MusicState.Victory, Instance.victoryMusic);
        public static void StopAllMusic() => Instance.StopMusic();
        #endregion

        #region 公共接口 - 状态控制
        public static void EnterPauseState()
        {
            var instance = Instance;
            if (instance._volumeState == VolumeState.Paused) return;

            instance._volumeState = VolumeState.Paused;
            instance.SetVolume(instance.pauseVolume);
            instance.Log($"暂停状态: 音量 → {instance.pauseVolume}");
        }

        public static void ExitPauseState()
        {
            var instance = Instance;
            if (instance._volumeState != VolumeState.Paused) return;

            instance._volumeState = VolumeState.Normal;
            instance.SetVolume(instance.GetTargetVolume());
            instance.Log($"恢复播放: 音量 → {instance.GetTargetVolume()}");
        }

        public static void SetMasterVolume(float volume)
        {
            Instance.normalVolume = Mathf.Clamp01(volume);
            if (Instance._volumeState == VolumeState.Normal)
            {
                Instance.SetVolume(volume);
            }
        }

        public static void SetVictoryVolume(float volume)
        {
            Instance.victoryVolume = Mathf.Clamp01(volume);
            if (Instance._currentMusic == MusicState.Victory)
            {
                Instance.SetVolume(volume);
            }
        }
        #endregion

        #region 内部实现 - 音乐播放
        private void PlayMusic(MusicState state, AudioClip clip)
        {
            if (_currentMusic == state && _audioSource.isPlaying)
            {
                Log($"已在播放 {state} 音乐，跳过");
                return;
            }

            if (clip == null)
            {
                LogError($"未配置 {state} 音乐片段");
                return;
            }

            _currentMusic = state;
            StartCoroutine(CrossfadeToClip(clip));
        }

        private IEnumerator CrossfadeToClip(AudioClip newClip)
        {
            float targetVolume = GetTargetVolume();

            // 淡出旧音乐
            if (_audioSource.isPlaying)
            {
                SetVolume(0f, musicCrossfadeTime);
                // 使用 unscaled time 的 WaitForSecondsRealtime
                yield return new WaitForSecondsRealtime(musicCrossfadeTime);
                _audioSource.Stop();
            }

            // 播放新音乐并淡入
            _audioSource.clip = newClip;
            _audioSource.volume = 0f;
            _audioSource.Play();
            SetVolume(targetVolume, volumeFadeTime);

            Log($"播放音乐: {newClip.name}, 音量: {targetVolume}");
        }

        private void StopMusic()
        {
            _currentMusic = MusicState.None;
            SetVolume(0f, volumeFadeTime);
            
            // 使用 unscaled time 的延迟调用
            DOVirtual.DelayedCall(volumeFadeTime, () => _audioSource.Stop())
                .SetUpdate(true);
        }
        #endregion

        #region 内部实现 - 音量控制
        private void SetVolume(float target, float duration = -1f)
        {
            if (duration < 0f) duration = volumeFadeTime;

            _volumeTween?.Kill();
            
            // 🔑 关键修复：SetUpdate(true) 使 Tween 使用 unscaled time
            // 这样即使 Time.timeScale = 0（暂停/胜利），音量变化仍然会执行
            _volumeTween = _audioSource.DOFade(target, duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);  // 🎯 不受 timeScale 影响
        }

        private float GetTargetVolume()
        {
            return _volumeState switch
            {
                VolumeState.Paused => pauseVolume,
                VolumeState.Rewinding => rewindVolume,
                _ => _currentMusic == MusicState.Victory ? victoryVolume : normalVolume
            };
        }
        #endregion

        #region 回溯状态管理
        private void Update()
        {
            if (TimeRewind.GlobalTimeRewindManager.Instance == null) return;

            bool isRewinding = TimeRewind.GlobalTimeRewindManager.Instance.IsGlobalRewinding;

            if (isRewinding && _volumeState != VolumeState.Rewinding)
            {
                EnterRewindState();
            }
            else if (!isRewinding && _volumeState == VolumeState.Rewinding)
            {
                ExitRewindState();
            }
        }

        private void EnterRewindState()
        {
            _volumeState = VolumeState.Rewinding;
            SetVolume(rewindVolume);
            ApplyRewindEffect(true);
            Log("进入回溯状态");
        }

        private void ExitRewindState()
        {
            _volumeState = VolumeState.Normal;
            SetVolume(GetTargetVolume());
            ApplyRewindEffect(false);
            Log("退出回溯状态");
        }

        private void ApplyRewindEffect(bool enable)
        {
            if (!_hasMixer) return;

            _mixerTween?.Kill();

            float targetCutoff = enable ? rewindLowpassCutoff : normalLowpassCutoff;
            float targetPitch = enable ? rewindPitch : normalPitch;
            float targetEcho = enable ? rewindEchoWetMix : normalEchoWetMix;

            // 🔑 Lowpass（低通滤波器）
            if (audioMixer.GetFloat(bgmLowpassParameter, out float currentCutoff))
            {
                DOVirtual.Float(currentCutoff, targetCutoff, volumeFadeTime, value =>
                    audioMixer.SetFloat(bgmLowpassParameter, value)
                ).SetEase(Ease.InOutQuad)
                 .SetUpdate(true);
            }

            // 🔑 Pitch Shifter（音调变换）
            if (audioMixer.GetFloat(bgmPitchParameter, out float currentPitch))
            {
                DOVirtual.Float(currentPitch, targetPitch, volumeFadeTime, value =>
                    audioMixer.SetFloat(bgmPitchParameter, value)
                ).SetEase(Ease.InOutQuad)
                 .SetUpdate(true);
            }

            // 🔑 Echo（回声效果 - 新增）
            if (audioMixer.GetFloat(bgmEchoParameter, out float currentEcho))
            {
                DOVirtual.Float(currentEcho, targetEcho, volumeFadeTime, value =>
                    audioMixer.SetFloat(bgmEchoParameter, value)
                ).SetEase(Ease.InOutQuad)
                 .SetUpdate(true);
                
                Log($"Echo 效果: {currentEcho:F2} → {targetEcho:F2}");
            }
        }

        private void ResetMixerToNormal()
        {
            if (!_hasMixer) return;

            audioMixer.SetFloat(bgmLowpassParameter, normalLowpassCutoff);
            audioMixer.SetFloat(bgmPitchParameter, normalPitch);
            
            // 尝试重置 Echo（如果参数存在）
            if (audioMixer.GetFloat(bgmEchoParameter, out _))
            {
                audioMixer.SetFloat(bgmEchoParameter, normalEchoWetMix);
            }
        }
        #endregion

        #region 调试日志
        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[AudioManager] {message}");
        }

        private void LogWarning(string message)
        {
            if (showDebugLogs) Debug.LogWarning($"[AudioManager] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[AudioManager] {message}");
        }
        #endregion
    }
}
