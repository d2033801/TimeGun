using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject settingsPanel;   // 拖拽引用
    public Slider volumeSlider;        // 拖拽引用

    private void Start()
    {
        // 默认隐藏设置界面
        settingsPanel.SetActive(false);

        // 初始化音量（如果你有保存过音量，就加载，否则用默认值1）
        float savedVolume = PlayerPrefs.GetFloat("Volume", 1f);
        volumeSlider.value = savedVolume;
        AudioListener.volume = savedVolume;

        // 监听滑块变化
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    // 在按钮 OnClick 中调用
    public void StartGame()
    {
        // 场景名必须与项目中场景文件名一致
        SceneManager.LoadScene("GameScene");
    }

    public void QuitGame()
    {
        // 编辑器里不会退出，但会在控制台打印
        Debug.Log("QuitGame called - Application.Quit() will work in build.");
        Application.Quit();
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("Volume", value);
    }
}
