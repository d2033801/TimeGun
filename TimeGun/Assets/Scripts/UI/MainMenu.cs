using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject controlsPanel;   // 操作指南（共用）
    public GameObject mainMenuPanel;  // 主菜单整体
    public GameObject gameHUDPanel;   // 游戏HUD
    public GameObject escMenuPanel;   // 暂停菜单

    [Header("摄像机")]
    public CinemachineCamera menuCam;
    public CinemachineCamera gameCam;

    [Header("HUD")]
    public TextMeshProUGUI ammoText;

    [Header("Input System")]
    [SerializeField]
    private InputActionReference callMenuAction;
    private InputAction _callMenuAction; // Inputsystem缓存

    private bool isInControls = false; // 是否在操作指南
    private bool isPaused = false;    // 是否在暂停菜单
    private bool isPlaying = false;  // 是否在游戏中
    private bool openedFromEsc = false; 

    private int currentAmmo = 1;
    private int maxAmmo = 2;
    private void Start()
    {
        // 初始化状态
        controlsPanel?.SetActive(false);
        gameHUDPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
        escMenuPanel?.SetActive(false);
        _callMenuAction ??= callMenuAction.action;

        /*
        // 初始相机优先级
        if (gameCam != null) gameCam.Priority = 10;
        if (menuCam != null) menuCam.Priority = 20;
         */
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 恢复时间流动
        Time.timeScale = 1;

        UpdateAmmoDisplay();
    }

    private void Update()
    {
        // 处理 ESC 键逻辑
        if (_callMenuAction.WasPressedThisFrame())
        {
            // 如果在操作指南中，返回来源菜单
            if (isInControls)
            {
                CloseControls();
                return;
            }
            Debug.Log("unPlaying ESC Pressed!");

            if (isPlaying)
            {
                Debug.Log("Playing ESC Pressed!");
                if (!isPaused)
                {
                    ShowEscMenu();
                    isPaused = true;
                }
                else
                {
                    ResumeGame();
                    isPaused = false;
                }
            }
        }
    }

    // ====== 主菜单 ======
    public void StartGame()
    {
        mainMenuPanel?.SetActive(false);
        gameHUDPanel?.SetActive(true);
        escMenuPanel?.SetActive(false);
        controlsPanel?.SetActive(false);

        /*
        gameCam.Priority = 20;
        menuCam.Priority = 10;
         */

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 确保时间流动
        Time.timeScale = 1;
        isPlaying = true;
        isPaused = false;
    }

    public void QuitGame()
    {
        Debug.Log("QuitGame called - Application.Quit() will work in build.");
        Application.Quit();
    }

    // ====== 操作指南（共用） ======
    public void OpenControls()
    {
        // 判断当前是从哪个菜单进入的
        if (mainMenuPanel.activeSelf)
        {
            openedFromEsc = false; // 从主菜单进入
            mainMenuPanel.SetActive(false);
        }
        else if (escMenuPanel.activeSelf)
        {
            openedFromEsc = true; // 从ESC菜单进入
            escMenuPanel.SetActive(false);
        }

        controlsPanel.SetActive(true);
        isInControls = true;
    }

    public void CloseControls()
    {
        controlsPanel.SetActive(false);

        // 返回来源菜单
        if (openedFromEsc)
        {
            escMenuPanel.SetActive(true);
        }
        else
        {
            mainMenuPanel.SetActive(true);
        }

        isInControls = false;
    }

    // ====== 游戏中（ESC菜单） ======
    public void ShowEscMenu()
    {
        escMenuPanel.SetActive(true);
        gameHUDPanel.SetActive(false);
        controlsPanel.SetActive(false);
        Time.timeScale = 0; // 暂停游戏

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void UpdateAmmoDisplay()
    {
        if (ammoText != null)
            ammoText.text = $"Ammo: {currentAmmo} / {maxAmmo}";
    }

    public void SetAmmo(int current, int max)
    {
        currentAmmo = current;
        maxAmmo = max;
        UpdateAmmoDisplay();
    }

    public void RestartGame()
    {
        
        Time.timeScale = 1;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
        Debug.Log("Game Restarted.");
    }

    public void ResumeGame()
    {
        escMenuPanel.SetActive(false);
        gameHUDPanel.SetActive(true);
        controlsPanel.SetActive(false);
        Time.timeScale = 1; // 继续游戏

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
