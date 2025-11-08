using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject controlsPanel;
    public GameObject mainMenuPanel;
    public GameObject gameHUDPanel;
    public GameObject escMenuPanel;
    public GameObject deadPanel;

    [Header("摄像机")]
    public CinemachineCamera menuCam;
    public CinemachineCamera gameCam;

    [Header("玩家控制")]
    [Tooltip("玩家控制器引用")]
    [SerializeField] private TimeGun.PlayerController playerController;

    [Header("相机混合设置")]
    [Tooltip("主相机的 CinemachineBrain 组件")]
    [SerializeField] private CinemachineBrain cinemachineBrain;

    [Tooltip("菜单到游戏的混合时间（秒）")]
    [SerializeField] private float menuToGameBlendTime = 1.5f;

    [Tooltip("游戏内相机切换混合时间（秒）")]
    [SerializeField] private float inGameBlendTime = 0.2f;

    [Header("HUD")]
    public TextMeshProUGUI ammoText;

    [Header("Input System")]
    [SerializeField]
    private InputActionReference callMenuAction;
    private InputAction _callMenuAction;

    private bool isInControls = false;
    private bool isPaused = false;
    private bool isPlaying = false;
    private bool openedFromEsc = false;

    private int currentAmmo = 1;
    private int maxAmmo = 2;

    private void Start()
    {
        // 自动获取 CinemachineBrain
        if (cinemachineBrain == null)
        {
            cinemachineBrain = Camera.main?.GetComponent<CinemachineBrain>();
        }

        // 初始化状态
        controlsPanel?.SetActive(false);
        gameHUDPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
        escMenuPanel?.SetActive(false);
        deadPanel?.SetActive(false);
        _callMenuAction ??= callMenuAction.action;

        // 初始化相机优先级（菜单相机优先级更高）
        if (menuCam != null) menuCam.Priority = 20;
        if (gameCam != null) gameCam.Priority = 10;

        // 禁用玩家控制
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1;

        UpdateAmmoDisplay();
    }

    private void Update()
    {
        if (playerController.IsDead)
        {
            // pass
        }
        else if (_callMenuAction.WasPressedThisFrame())
        {
            if (isInControls)
            {
                CloseControls();
                return;
            }

            if (isPlaying)
            {
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
        deadPanel?.SetActive(false);

        // 切换相机优先级（触发 Cinemachine 混合）
        if (menuCam != null) menuCam.Priority = 10;
        if (gameCam != null) gameCam.Priority = 20;

        // 临时设置较长的混合时间
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend.Time = menuToGameBlendTime;
        }

        // 延迟启用玩家控制，等待相机混合完成
        if (playerController != null)
        {
            StartCoroutine(EnablePlayerAfterDelay(menuToGameBlendTime));
        }
        menuCam.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1;
        isPlaying = true;
        isPaused = false;
    }

    /// <summary>
    /// 延迟启用玩家控制（等待相机混合完成）
    /// </summary>
    private System.Collections.IEnumerator EnablePlayerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // 恢复游戏内的快速混合时间
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend.Time = inGameBlendTime;
        }
    }

    public void QuitGame()
    {
        Debug.Log("QuitGame called - Application.Quit() will work in build.");
        Application.Quit();
    }

    // ====== 操作指南（共用） ======
    public void OpenControls()
    {
        if (mainMenuPanel.activeSelf)
        {
            openedFromEsc = false;
            mainMenuPanel.SetActive(false);
        }
        else if (escMenuPanel.activeSelf)
        {
            openedFromEsc = true;
            escMenuPanel.SetActive(false);
        }

        controlsPanel.SetActive(true);
        isInControls = true;
    }

    public void OpenDeadPanel()
    {

    }

    public void CloseControls()
    {
        controlsPanel.SetActive(false);

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
        deadPanel?.SetActive(false);
        Time.timeScale = 0;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 暂停时禁用玩家控制
        if (playerController != null)
        {
            playerController.enabled = false;
        }
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
        deadPanel?.SetActive(false);
        Time.timeScale = 1;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 恢复时启用玩家控制
        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }
}