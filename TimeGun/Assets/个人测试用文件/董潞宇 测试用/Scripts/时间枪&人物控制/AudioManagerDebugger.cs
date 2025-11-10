using UnityEngine;
using UnityEngine.Audio;

namespace TimeGun
{
    /// <summary>
    /// AudioManager 调试工具（附加组件）
    /// 用于诊断 AudioMixer 参数和信号路径问题
    /// </summary>
    [AddComponentMenu("TimeGun/Audio Manager Debugger")]
    public class AudioManagerDebugger : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource audioSource;

        [Header("参数名称")]
        [SerializeField] private string lowpassParameter = "BGMLowpassCutoff";
        [SerializeField] private string pitchParameter = "BGMPitch";
        [SerializeField] private string echoParameter = "BGMEchoWetMix";

        [Header("测试值")]
        [SerializeField] private float testLowpass = 800f;
        [SerializeField] private float testPitch = 0.85f;
        [SerializeField] private float testEcho = 0.3f;

        [ContextMenu("🎵 测试回溯效果")]
        private void TestRewindEffect()
        {
            if (audioMixer == null)
            {
                Debug.LogError("未配置 AudioMixer！");
                return;
            }

            Debug.Log("====== 测试回溯效果 ======");

            // 应用回溯参数
            audioMixer.SetFloat(lowpassParameter, testLowpass);
            Debug.Log($"✅ Lowpass → {testLowpass} Hz");

            audioMixer.SetFloat(pitchParameter, testPitch);
            Debug.Log($"✅ Pitch → {testPitch}");

            if (audioMixer.GetFloat(echoParameter, out _))
            {
                audioMixer.SetFloat(echoParameter, testEcho);
                Debug.Log($"✅ Echo → {testEcho}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Echo 参数 '{echoParameter}' 未找到");
            }

            Debug.Log("🎧 请听音乐是否变沉闷？");
        }

        [ContextMenu("🔄 恢复正常效果")]
        private void TestNormalEffect()
        {
            if (audioMixer == null)
            {
                Debug.LogError("未配置 AudioMixer！");
                return;
            }

            Debug.Log("====== 恢复正常效果 ======");

            audioMixer.SetFloat(lowpassParameter, 22000f);
            Debug.Log($"✅ Lowpass → 22000 Hz");

            audioMixer.SetFloat(pitchParameter, 1.0f);
            Debug.Log($"✅ Pitch → 1.0");

            if (audioMixer.GetFloat(echoParameter, out _))
            {
                audioMixer.SetFloat(echoParameter, 0f);
                Debug.Log($"✅ Echo → 0");
            }

            Debug.Log("🎧 请听音乐是否恢复清晰？");
        }

        [ContextMenu("📊 显示当前参数")]
        private void ShowCurrentParameters()
        {
            if (audioMixer == null)
            {
                Debug.LogError("未配置 AudioMixer！");
                return;
            }

            Debug.Log("====== AudioMixer 当前参数 ======");

            if (audioMixer.GetFloat(lowpassParameter, out float cutoff))
            {
                Debug.Log($"  Lowpass: {cutoff:F1} Hz");
            }
            else
            {
                Debug.LogError($"  ❌ 参数 '{lowpassParameter}' 未暴露");
            }

            if (audioMixer.GetFloat(pitchParameter, out float pitch))
            {
                Debug.Log($"  Pitch: {pitch:F2}");
            }
            else
            {
                Debug.LogError($"  ❌ 参数 '{pitchParameter}' 未暴露");
            }

            if (audioMixer.GetFloat(echoParameter, out float echo))
            {
                Debug.Log($"  Echo: {echo:F2}");
            }
            else
            {
                Debug.LogWarning($"  ⚠️ 参数 '{echoParameter}' 未暴露（可选）");
            }

            if (audioSource != null)
            {
                Debug.Log("====== AudioSource 状态 ======");
                Debug.Log($"  音量: {audioSource.volume:F2}");
                Debug.Log($"  播放中: {audioSource.isPlaying}");
                Debug.Log($"  输出到: {audioSource.outputAudioMixerGroup?.name ?? "未配置"}");
            }
        }

        [ContextMenu("🔍 检测组结构")]
        private void DetectGroupStructure()
        {
            if (audioMixer == null)
            {
                Debug.LogError("未配置 AudioMixer！");
                return;
            }

            Debug.Log("====== Mixer 组结构检测 ======");

            // 查找所有组
            var allGroups = audioMixer.FindMatchingGroups(string.Empty);
            if (allGroups == null || allGroups.Length == 0)
            {
                Debug.LogError("❌ 未找到任何组！");
                return;
            }

            Debug.Log($"找到 {allGroups.Length} 个组：");
            foreach (var group in allGroups)
            {
                Debug.Log($"  - {group.name}");
            }

            // 检查关键组
            var bgmGroups = audioMixer.FindMatchingGroups("BackgroundMusic");
            if (bgmGroups != null && bgmGroups.Length > 0)
            {
                Debug.Log($"✅ 找到 BackgroundMusic 组");
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到 BackgroundMusic 组");
            }

            var rewindGroups = audioMixer.FindMatchingGroups("RewindEffects");
            if (rewindGroups != null && rewindGroups.Length > 0)
            {
                Debug.Log($"✅ 找到 RewindEffects 组");
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到 RewindEffects 组");
            }
        }

        // 运行时实时显示
        private void OnGUI()
        {
            if (audioMixer == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Box("AudioMixer 实时参数", GUILayout.Width(290));

            if (audioMixer.GetFloat(lowpassParameter, out float cutoff))
            {
                GUILayout.Label($"Lowpass: {cutoff:F0} Hz");
            }

            if (audioMixer.GetFloat(pitchParameter, out float pitch))
            {
                GUILayout.Label($"Pitch: {pitch:F2}");
            }

            if (audioMixer.GetFloat(echoParameter, out float echo))
            {
                GUILayout.Label($"Echo: {echo:F2}");
            }

            if (audioSource != null)
            {
                GUILayout.Label($"Volume: {audioSource.volume:F2}");
                GUILayout.Label($"Playing: {audioSource.isPlaying}");
            }

            GUILayout.EndArea();
        }
    }
}
