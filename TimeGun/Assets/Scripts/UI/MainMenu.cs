using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject settingsPanel;   // 拖拽引用
    public Slider volumeSlider;        // 拖拽引用
    public GameObject mainMenuCanvas;
    public CinemachineCamera menuCam;
    public CinemachineCamera gameCam;

    private void Start()
    {
        // 默认隐藏设置界面
        settingsPanel.SetActive(false);

        gameCam.Priority = 10;
        menuCam.Priority = 20;

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
        /*
        // 1. 提升游戏镜头优先级
        gameCam.Priority = 20;
        menuCam.Priority = 5;
        */

        // 2. 隐藏主菜单
        mainMenuCanvas.SetActive(false);
        gameCam.Priority = 20;
        menuCam.Priority = 10;
        // 3. TODO : 显示游戏UI
        // gameHUDCanvas.SetActive(true);

        // 4. 锁定并隐藏鼠标光标
        Cursor.lockState = CursorLockMode.Locked;  // 锁定鼠标到屏幕中心
        Cursor.visible = false;                    // 隐藏鼠标
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
