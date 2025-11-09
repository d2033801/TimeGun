using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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
    
    [Tooltip("死亡摄像机（用于看向击杀玩家的敌人）")]
    public CinemachineCamera deathCam;

    [Header("玩家控制")]
    [Tooltip("玩家控制器引用")]
    [SerializeField] private TimeGun.PlayerController playerController;

    [Tooltip("武器管理器引用（用于获取弹药信息）")]
    [SerializeField] private TimeGun.WeaponManager weaponManager;

    [Header("相机混合设置")]
    [Tooltip("主相机的 CinemachineBrain 组件")]
    [SerializeField] private CinemachineBrain cinemachineBrain;

    [Tooltip("菜单到游戏的混合时间（秒）")]
    [SerializeField] private float menuToGameBlendTime = 1.5f;

    [Tooltip("游戏内相机切换混合时间（秒）")]
    [SerializeField] private float inGameBlendTime = 0.2f;

    [Header("死亡摄像机设置")]
    [Tooltip("切换到死亡摄像机的混合时间（秒）")]
    [SerializeField] private float deathCameraBlendTime = 1.5f;

    [Tooltip("死亡摄像机停留时间（秒），之后显示死亡面板")]
    [SerializeField] private float deathCameraHoldTime = 2.0f;

    [Header("HUD")]
    [Tooltip("子弹数量显示文本")]
    public TextMeshProUGUI ammoText;
    
    [Tooltip("榴弹数量显示文本")]
    public TextMeshProUGUI grenadeText;

    [Tooltip("游戏时间显示文本")]
    public TextMeshProUGUI gameTimeText;

    [Header("Input System")]
    [SerializeField]
    private InputActionReference callMenuAction;
    private InputAction _callMenuAction;

    private bool isInControls = false;
    private bool isPaused = false;
    private bool isPlaying = false;
    private bool openedFromEsc = false;

    private TimeGun.AmmoSystem _ammoSystem;

    private void Start()
    {
        // 自动获取 CinemachineBrain
        if (cinemachineBrain == null)
        {
            cinemachineBrain = Camera.main?.GetComponent<CinemachineBrain>();
        }

        // 获取弹药系统
        if (weaponManager != null)
        {
            _ammoSystem = weaponManager.GetComponent<TimeGun.AmmoSystem>();
            if (_ammoSystem == null)
            {
                Debug.LogWarning("[MainMenu] WeaponManager 上未找到 AmmoSystem 组件！");
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] 未设置 WeaponManager 引用！");
        }

        // 设置游戏时钟的 UI 文本
        if (gameTimeText != null)
        {
            var gameClock = TimeRewind.GlobalGameClock.Instance;
            if (gameClock != null)
            {
                gameClock.SetTimeText(gameTimeText);
            }
            else
            {
                Debug.LogWarning("[MainMenu] 未找到 GlobalGameClock 实例！");
            }
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
        if (deathCam != null) deathCam.Priority = 5; // 初始优先级较低

        // 禁用玩家控制
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1;

        // 延迟初始化显示，确保 AmmoSystem 已经初始化
        StartCoroutine(InitializeAmmoDisplayDelayed());
    }

    /// <summary>
    /// 延迟初始化弹药显示（等待 AmmoSystem 初始化完成）
    /// </summary>
    private System.Collections.IEnumerator InitializeAmmoDisplayDelayed()
    {
        yield return null; // 等待一帧，确保所有 Start() 都执行完毕
        UpdateAmmoDisplay();
        UpdateGrenadeDisplay();
    }

    private void OnEnable()
    {
        // 订阅弹药变化事件
        TimeGun.AmmoSystem.OnAmmoChanged += HandleAmmoChanged;
    }

    private void OnDisable()
    {
        // 取消订阅弹药变化事件
        TimeGun.AmmoSystem.OnAmmoChanged -= HandleAmmoChanged;
    }

    /// <summary>
    /// 处理弹药变化事件（实时更新HUD）
    /// </summary>
    private void HandleAmmoChanged(TimeGun.AmmoSystem.AmmoType ammoType, int current, int max)
    {
        // 根据弹药类型更新对应的显示
        if (ammoType == TimeGun.AmmoSystem.AmmoType.Bullet)
        {
            UpdateAmmoDisplay();
        }
        else if (ammoType == TimeGun.AmmoSystem.AmmoType.Grenade)
        {
            UpdateGrenadeDisplay();
        }
    }

    private void Update()
    {
        // 持续更新重装填时间显示（每帧更新）
        if (isPlaying && !isPaused && _ammoSystem != null)
        {
            UpdateAmmoDisplay();
            UpdateGrenadeDisplay();
        }

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

    /// <summary>
    /// 显示死亡面板（带摄像机过渡到敌人的效果）
    /// </summary>
    /// <param name="killerEnemy">击杀玩家的敌人Transform</param>
    public void OpenDeadPanel(Transform killerEnemy = null)
    {
        if (killerEnemy != null)
        {
            // 如果有击杀者，先切换摄像机到敌人，延迟显示死亡面板
            StartCoroutine(DeathCameraSequence(killerEnemy));
        }
        else
        {
            // 没有击杀者，直接显示死亡面板
            ShowDeadPanelImmediate();
        }
    }

    /// <summary>
    /// 死亡摄像机序列：切换到敌人 → 停留 → 显示死亡面板
    /// </summary>
    private System.Collections.IEnumerator DeathCameraSequence(Transform killerEnemy)
    {
        Debug.Log($"[MainMenu] 开始死亡摄像机序列，目标敌人: {killerEnemy.name}");

        // 隐藏游戏 HUD
        gameHUDPanel?.SetActive(false);

        // ✅ 禁用 TPSCameraController，防止它干扰死亡摄像机
        var tpsController = UnityEngine.Object.FindFirstObjectByType<TimeGun.TPSCameraController>();
        if (tpsController != null)
        {
            tpsController.enabled = false;
            Debug.Log("[MainMenu] 已临时禁用 TPSCameraController");
        }

        // 选择最佳 LookAt 目标：优先 Enemy.headTransform → 名为"Headeye" → 名为"Head" → 敌人自身
        Transform lookAtTarget = killerEnemy;
        var enemyComp = killerEnemy.GetComponentInParent<Enemy>();
        if (enemyComp != null && enemyComp.headTransform != null)
        {
            lookAtTarget = enemyComp.headTransform;
            Debug.Log($"[MainMenu] 使用敌人的 headTransform: {lookAtTarget.name}");
        }
        else
        {
            var headEye = FindChildRecursive(killerEnemy, "Headeye");
            if (headEye != null)
            {
                lookAtTarget = headEye;
                Debug.Log($"[MainMenu] 找到 Headeye: {lookAtTarget.name}");
            }
            else
            {
                var head = FindChildRecursive(killerEnemy, "Head");
                if (head != null)
                {
                    lookAtTarget = head;
                    Debug.Log($"[MainMenu] 找到 Head: {lookAtTarget.name}");
                }
                else
                {
                    Debug.Log($"[MainMenu] 未找到头部节点，使用敌人根节点: {killerEnemy.name}");
                }
            }
        }

        // 设置死亡摄像机的 LookAt 目标为敌人
        if (deathCam != null)
        {
            deathCam.gameObject.SetActive(true);
            deathCam.LookAt = lookAtTarget;

            // ✅ 使用固定的高优先级（确保高于所有游戏摄像机）
            int deathCamPriority = 100;

            // 设置混合时间并提升死亡摄像机优先级
            if (cinemachineBrain != null)
            {
                cinemachineBrain.DefaultBlend.Time = deathCameraBlendTime;
            }

            deathCam.Priority = deathCamPriority;
            Debug.Log($"[MainMenu] 死亡摄像机优先级设置为: {deathCamPriority}");
        }

        // 等待摄像机混合完成 + 额外停留时间（使用未缩放时间，避免被暂停影响）
        float totalWaitTime = deathCameraBlendTime + deathCameraHoldTime;
        float elapsed = 0f;
        while (elapsed < totalWaitTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 恢复混合时间
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend.Time = inGameBlendTime;
        }

        // ✅ 降低死亡摄像机优先级，但不重新启用 TPSCameraController（因为游戏已暂停）
        if (deathCam != null)
        {
            deathCam.Priority = 5;
        }

        // 显示死亡面板
        ShowDeadPanelImmediate();
    }

    /// <summary>
    /// 递归查找子物体（支持深层查找）
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string name)
    {
        // 先检查直接子物体
        var child = parent.Find(name);
        if (child != null) return child;

        // 递归检查所有子物体
        foreach (Transform child2 in parent)
        {
            var result = FindChildRecursive(child2, name);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// 立即显示死亡面板（无摄像机过渡）
    /// </summary>
    private void ShowDeadPanelImmediate()
    {
        // 显示死亡面板，隐藏其他面板
        deadPanel?.SetActive(true);
        gameHUDPanel?.SetActive(false);
        mainMenuPanel?.SetActive(false);
        escMenuPanel?.SetActive(false);
        controlsPanel?.SetActive(false);

        // 暂停游戏
        Time.timeScale = 0;

        // 显示鼠标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 禁用玩家控制
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        Debug.Log("[MainMenu] 已显示死亡面板");
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

    /// <summary>
    /// 更新子弹数量显示（包含重装填时间）
    /// </summary>
    public void UpdateAmmoDisplay()
    {
        if (ammoText != null && _ammoSystem != null)
        {
            // 从 AmmoSystem 获取实时子弹数据
            int currentBullets = _ammoSystem.CurrentBullets;
            int maxBullets = _ammoSystem.MaxBullets;
            
            // 获取重装填剩余时间
            float reloadTime = _ammoSystem.BulletRestoreRemainingTime;
            
            // 如果弹药未满且正在重装填，显示重装填时间
            if (currentBullets < maxBullets && reloadTime > 0f)
            {
                ammoText.text = $"Bullets: {currentBullets} / {maxBullets}   {reloadTime:F1}s";
            }
            else
            {
                // 弹药满了或没有在重装填
                ammoText.text = $"Bullets: {currentBullets} / {maxBullets}";
            }
        }
        else if (ammoText != null)
        {
            // 如果 AmmoSystem 还没初始化，显示占位符
            ammoText.text = "Bullets: -- / --";
        }
    }

    /// <summary>
    /// 更新榴弹数量显示（包含重装填时间）
    /// </summary>
    public void UpdateGrenadeDisplay()
    {
        if (grenadeText != null && _ammoSystem != null)
        {
            // 从 AmmoSystem 获取实时榴弹数据
            int currentGrenades = _ammoSystem.CurrentGrenades;
            int maxGrenades = _ammoSystem.MaxGrenades;
            
            // 获取重装填剩余时间
            float reloadTime = _ammoSystem.GrenadeRestoreRemainingTime;
            
            // 如果弹药未满且正在重装填，显示重装填时间
            if (currentGrenades < maxGrenades && reloadTime > 0f)
            {
                grenadeText.text = $"Grenades: {currentGrenades} / {maxGrenades}   {reloadTime:F1}s";
            }
            else
            {
                // 弹药满了或没有在重装填
                grenadeText.text = $"Grenades: {currentGrenades} / {maxGrenades}";
            }
        }
        else if (grenadeText != null)
        {
            // 如果 AmmoSystem 还没初始化，显示占位符
            grenadeText.text = "Grenades: -- / --";
        }
    }

    /// <summary>
    /// [已弃用] 手动设置弹药显示（现在由 AmmoSystem 事件自动更新）
    /// </summary>
    [System.Obsolete("使用 AmmoSystem 的事件系统自动更新，无需手动调用此方法")]
    public void SetAmmo(int current, int max)
    {
        Debug.LogWarning("[MainMenu] SetAmmo 已弃用，弹药显示由 AmmoSystem 事件自动更新");
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