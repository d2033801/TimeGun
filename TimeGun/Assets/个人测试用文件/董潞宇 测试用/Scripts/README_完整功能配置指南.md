# TimeGun 时间回溯系统 - 完整配置指南

本文档包含所有新增功能的配置步骤和使用说明。

---

## 📋 目录

1. [全局时钟系统](#1-全局时钟系统)
2. [可回溯天花板掉落物体](#2-可回溯天花板掉落物体)
3. [弹药系统](#3-弹药系统)
4. [交互系统（按住F键胜利）](#4-交互系统按住f键胜利)
5. [音频管理器](#5-音频管理器)
6. [全局回溯改进](#6-全局回溯改进)
7. [常见问题](#7-常见问题)

---

## 1. 全局时钟系统

### 功能
- 记录游戏经过的分钟和秒数
- 支持时间回溯（计时器会随历史快照回溯）
- 提供公共接口供UI显示

### 配置步骤

#### 自动创建（推荐）
```csharp
// 脚本会在首次访问时自动创建，无需手动配置
var clock = GlobalGameClock.Instance;
```

#### 手动创建
1. 创建空 GameObject，命名为 `GlobalGameClock`
2. 添加组件：`Global Game Clock`
3. 勾选 `Don't Destroy On Load`（可选，脚本会自动处理）

### 配置参数
| 参数 | 说明 | 默认值 |
|------|------|--------|
| `recordSecondsConfig` | 录制时长（秒） | 20 |
| `recordFPSConfig` | 每秒录制帧率 | 自动 |
| `rewindSpeedConfig` | 回溯速度倍率 | 2 |

### UI集成示例

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeRewind;

public class ClockUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI clockText;

    private void Update()
    {
        if (GlobalGameClock.Instance != null)
        {
            // 方式1：使用格式化字符串
            clockText.text = GlobalGameClock.Instance.FormattedTime;

            // 方式2：自定义格式
            int minutes = GlobalGameClock.Instance.Minutes;
            int seconds = GlobalGameClock.Instance.Seconds;
            clockText.text = $"时间: {minutes}分{seconds}秒";
        }
    }
}
```

### API参考

```csharp
// 获取单例实例
GlobalGameClock clock = GlobalGameClock.Instance;

// 只读属性
float totalSeconds = clock.TotalSeconds;  // 总秒数
int minutes = clock.Minutes;              // 分钟数
int seconds = clock.Seconds;              // 秒数（0-59）
string formatted = clock.FormattedTime;   // "MM:SS" 格式

// 方法
clock.ResetClock();  // 重置时钟到0
```

---

## 2. 可回溯天花板掉落物体

### 功能
- 在指定倒计时后从天花板掉落（通过启用重力实现）
- 倒计时可被时间回溯
- 支持手动触发掉落

### 配置步骤

1. 选择要设置为掉落物体的 GameObject（必须有 Rigidbody）
2. 添加组件：`Rewindable Falling Object`
3. 配置 Rigidbody：
   - **初始状态**：禁用 `Use Gravity`
   - **可选**：勾选 `Is Kinematic`（防止物理抖动）
4. 配置掉落参数（见下表）

### 配置参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `dropDelay` | 掉落倒计时（秒），0表示不自动掉落 | 5 |
| `autoStartTimer` | 是否在游戏开始时自动启动倒计时 | true |
| `dropSound` | 掉落时播放的音效 | null |
| `dropSoundVolume` | 掉落音效音量（0-1） | 0.5 |

### 使用场景

#### 场景1：定时掉落陷阱
```csharp
// 在关卡开始时自动掉落
// 配置：autoStartTimer = true, dropDelay = 5
```

#### 场景2：按钮触发掉落
```csharp
public class ButtonTrigger : MonoBehaviour
{
    [SerializeField] private RewindableFallingObject fallingObject;

    public void OnButtonPressed()
    {
        fallingObject.TriggerDrop();
    }
}
```

#### 场景3：玩家回溯重置掉落
```csharp
// 配置：只需确保物体添加了 RewindableFallingObject 组件
// 玩家使用时间枪回溯时，物体会自动恢复到未掉落状态
```

### API参考

```csharp
RewindableFallingObject fallingObj = GetComponent<RewindableFallingObject>();

// 只读属性
bool hasFallen = fallingObj.HasFallen;                // 是否已掉落
float remainingTime = fallingObj.RemainingTime;        // 剩余倒计时

// 方法
fallingObj.TriggerDrop();           // 立即掉落
fallingObj.ResetToInitialState();   // 重置到初始状态
```

---

## 3. 弹药系统

### 功能
- 管理子弹和榴弹的备弹量和CD
- 支持自动恢复备弹（基于时间）
- 提供UI接口（当前/最大弹药数、CD进度）
- 事件驱动（弹药变化时触发全局事件，UI可订阅）

### 配置步骤

1. 在玩家 GameObject 上添加组件：`Ammo System`
2. 配置弹药参数（见下表）
3. UI订阅事件（见UI集成示例）

### 配置参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `maxBullets` | 子弹最大备弹量 | 2 |
| `bulletRestoreTime` | 子弹恢复CD（秒） | 40 |
| `maxGrenades` | 榴弹最大备弹量 | 1 |
| `grenadeRestoreTime` | 榴弹恢复CD（秒） | 40 |

### UI集成示例

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeGun;

public class AmmoUI : MonoBehaviour
{
    [Header("子弹UI")]
    [SerializeField] private TextMeshProUGUI bulletText;
    [SerializeField] private Image bulletCooldownBar;

    [Header("榴弹UI")]
    [SerializeField] private TextMeshProUGUI grenadeText;
    [SerializeField] private Image grenadeCooldownBar;

    private AmmoSystem _ammoSystem;

    private void Start()
    {
        // 获取弹药系统（通常挂在玩家身上）
        _ammoSystem = FindFirstObjectByType<AmmoSystem>();

        // 订阅弹药变化事件
        AmmoSystem.OnAmmoChanged += OnAmmoChanged;
    }

    private void OnDestroy()
    {
        // 取消订阅（防止内存泄漏）
        AmmoSystem.OnAmmoChanged -= OnAmmoChanged;
    }

    private void Update()
    {
        if (_ammoSystem == null) return;

        // 更新CD进度条（方式1：每帧更新）
        if (bulletCooldownBar != null)
        {
            bulletCooldownBar.fillAmount = _ammoSystem.BulletRestoreProgress;
        }

        if (grenadeCooldownBar != null)
        {
            grenadeCooldownBar.fillAmount = _ammoSystem.GrenadeRestoreProgress;
        }
    }

    /// <summary>
    /// 弹药变化事件处理（通过事件驱动更新UI）
    /// </summary>
    private void OnAmmoChanged(AmmoSystem.AmmoType ammoType, int current, int max)
    {
        switch (ammoType)
        {
            case AmmoSystem.AmmoType.Bullet:
                if (bulletText != null)
                {
                    bulletText.text = $"子弹: {current}/{max}";
                }
                break;

            case AmmoSystem.AmmoType.Grenade:
                if (grenadeText != null)
                {
                    grenadeText.text = $"榴弹: {current}/{max}";
                }
                break;
        }
    }
}
```

### API参考

```csharp
AmmoSystem ammo = GetComponent<AmmoSystem>();

// 只读属性（状态查询）
int currentBullets = ammo.CurrentBullets;      // 当前子弹数
int maxBullets = ammo.MaxBullets;              // 最大子弹数
float bulletProgress = ammo.BulletRestoreProgress;  // 子弹恢复进度（0-1）
float bulletRemaining = ammo.BulletRestoreRemainingTime;  // 子弹剩余恢复时间

// 同理，榴弹也有对应的属性
// CurrentGrenades, MaxGrenades, GrenadeRestoreProgress, GrenadeRestoreRemainingTime

// 方法（消耗弹药）
bool success = ammo.TryConsumeBullet();   // 尝试消耗一发子弹，返回是否成功
bool success2 = ammo.TryConsumeGrenade(); // 尝试消耗一发榴弹，返回是否成功

// 方法（管理弹药）
ammo.RefillAll();   // 立即补满所有弹药
ammo.ResetAmmo();   // 重置到初始满弹状态

// 事件订阅
AmmoSystem.OnAmmoChanged += (ammoType, current, max) => 
{
    Debug.Log($"{ammoType} 变化: {current}/{max}");
};

AmmoSystem.OnAmmoRestored += (ammoType) => 
{
    Debug.Log($"{ammoType} 恢复了一发");
};
```

---

## 4. 交互系统（按住F键胜利）

### 功能
- 玩家进入触发器区域后按住F键蓄力
- 达到指定时长后触发交互完成事件
- 提供进度查询接口供UI显示

### 配置步骤

1. 创建触发器对象（例如游戏终点）
2. 添加 `Collider` 组件并勾选 `Is Trigger`
3. 添加组件：`Hold To Interact`
4. 配置参数（见下表）
5. 在Input System中创建"Interact"动作（建议绑定F键）
6. 订阅事件实现胜利逻辑（见使用示例）

### 配置参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `holdDuration` | 按住时长（秒） | 2 |
| `interactAction` | 交互输入动作（通常绑定F键） | null |
| `playerLayer` | 玩家层掩码 | Layer 3 |
| `interactPrompt` | 交互提示文本 | "按住 F 键激活" |
| `showDebugGizmo` | 是否显示调试Gizmo | true |

### 使用示例

#### 场景1：游戏胜利条件

```csharp
using UnityEngine;
using TimeGun;

public class VictoryZone : MonoBehaviour
{
    private void Start()
    {
        // 订阅交互完成事件
        HoldToInteract.OnInteractionComplete += OnVictoryTriggered;
    }

    private void OnDestroy()
    {
        HoldToInteract.OnInteractionComplete -= OnVictoryTriggered;
    }

    private void OnVictoryTriggered(HoldToInteract interact)
    {
        // 只处理本对象的交互
        if (interact.gameObject != gameObject) return;

        Debug.Log("游戏胜利！");
        
        // 播放胜利音乐
        AudioManager.PlayVictoryMusic();

        // 显示胜利UI
        // VictoryUI.Instance.Show();

        // 停止玩家控制
        // PlayerController.Instance.enabled = false;
    }
}
```

#### 场景2：进度条UI

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeGun;

public class InteractUI : MonoBehaviour
{
    [SerializeField] private GameObject interactPanel;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI promptText;

    private HoldToInteract _currentInteract;

    private void Update()
    {
        // 查找玩家附近的交互对象
        var interacts = FindObjectsByType<HoldToInteract>(FindObjectsSortMode.None);
        _currentInteract = null;

        foreach (var interact in interacts)
        {
            if (interact.IsPlayerInRange && !interact.IsCompleted)
            {
                _currentInteract = interact;
                break;
            }
        }

        // 更新UI显示
        if (_currentInteract != null)
        {
            interactPanel.SetActive(true);
            promptText.text = _currentInteract.InteractPrompt;
            progressBar.fillAmount = _currentInteract.Progress;
        }
        else
        {
            interactPanel.SetActive(false);
        }
    }
}
```

### API参考

```csharp
HoldToInteract interact = GetComponent<HoldToInteract>();

// 只读属性
bool isPlayerInRange = interact.IsPlayerInRange;    // 玩家是否在范围内
float progress = interact.Progress;                 // 当前进度（0-1）
float remainingTime = interact.RemainingTime;       // 剩余按住时间
bool isCompleted = interact.IsCompleted;            // 是否已完成交互
string prompt = interact.InteractPrompt;            // 交互提示文本

// 方法
interact.ResetInteraction();  // 重置交互状态（允许再次交互）

// 事件订阅
HoldToInteract.OnInteractionComplete += (interact) => 
{
    Debug.Log($"交互完成: {interact.gameObject.name}");
};
```

---

## 5. 音频管理器

### 功能
- 管理不同场景的背景音乐（主菜单/游戏中/胜利）
- 暂停菜单时平滑降低音量
- 全局回溯时切换到沉闷音效
- 支持音量淡入淡出

### 配置步骤

#### 必需步骤
1. 创建空 GameObject，命名为 `AudioManager`
2. 添加组件：`Audio Manager`
3. 勾选 `Don't Destroy On Load`（可选，脚本会自动处理）
4. 分配音乐片段（见配置参数）

#### 可选步骤：使用AudioMixer（推荐）
1. 创建 AudioMixer：`Assets > Create > Audio Mixer`
2. 在Mixer中添加"Music"组，并暴露"MusicVolume"参数：
   - 右键点击"Music"组的Volume → "Expose 'Volume (of Music)' to script"
   - 在Mixer窗口右上角的"Exposed Parameters"中，将参数重命名为"MusicVolume"
3. 在AudioManager的Inspector中分配AudioMixer

### 配置参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| **音乐片段** | | |
| `mainMenuMusic` | 主菜单背景音乐 | null |
| `gameplayMusic` | 游戏中背景音乐 | null |
| `victoryMusic` | 胜利背景音乐 | null |
| `rewindMusic` | 回溯音效（沉闷、低音） | null |
| **音量配置** | | |
| `defaultVolume` | 默认音量（0-1） | 0.7 |
| `pauseVolume` | 暂停菜单时的音量（0-1） | 0.3 |
| `rewindVolume` | 回溯时的音量（0-1） | 0.5 |
| **过渡配置** | | |
| `fadeTime` | 音量淡入淡出时间（秒） | 1 |
| `crossfadeTime` | 音乐切换交叉淡入淡出时间（秒） | 2 |
| **AudioMixer（可选）** | | |
| `audioMixer` | 主音频混合器 | null |
| `musicVolumeParameter` | 音乐音量参数名 | "MusicVolume" |

### 使用示例

#### 场景1：主菜单播放音乐

```csharp
using UnityEngine;
using TimeGun;

public class MainMenu : MonoBehaviour
{
    private void Start()
    {
        // 播放主菜单音乐
        AudioManager.PlayMainMenuMusic();
    }

    public void OnStartGameButtonClicked()
    {
        // 切换到游戏音乐
        AudioManager.PlayGameplayMusic();
    }
}
```

#### 场景2：暂停菜单音量控制

```csharp
using UnityEngine;
using TimeGun;

public class PauseMenu : MonoBehaviour
{
    public void PauseGame()
    {
        Time.timeScale = 0;
        AudioManager.EnterPauseState();  // 降低音量
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;
        AudioManager.ExitPauseState();   // 恢复音量
    }
}
```

#### 场景3：胜利音乐

```csharp
using UnityEngine;
using TimeGun;

public class VictoryController : MonoBehaviour
{
    private void OnVictory()
    {
        AudioManager.PlayVictoryMusic();
    }
}
```

### API参考

```csharp
// 播放音乐
AudioManager.PlayMainMenuMusic();   // 播放主菜单音乐
AudioManager.PlayGameplayMusic();   // 播放游戏音乐
AudioManager.PlayVictoryMusic();    // 播放胜利音乐
AudioManager.StopAllMusic();        // 停止所有音乐

// 状态控制
AudioManager.EnterPauseState();     // 进入暂停状态（音量降低）
AudioManager.ExitPauseState();      // 退出暂停状态（音量恢复）
AudioManager.SetMasterVolume(0.5f); // 设置主音量（0-1）
```

### 自动功能

以下功能会自动处理，无需手动调用：

1. **全局回溯时的音效切换**：AudioManager会自动检测`GlobalTimeRewindManager`的状态，在回溯时切换到沉闷音效
2. **音乐平滑过渡**：所有音乐切换都使用交叉淡入淡出，避免突兀的切换

---

## 6. 全局回溯改进

### 新增功能

1. **自动停止机制**：当所有可回溯物体的历史记录为空时，自动停止全局回溯
2. **全局事件**：添加`OnGlobalRewindStarted`和`OnGlobalRewindStopped`事件
3. **玩家输入禁用**：回溯期间自动禁用玩家输入

### 使用示例

#### 订阅全局回溯事件

```csharp
using UnityEngine;
using TimeRewind;

public class RewindEffectController : MonoBehaviour
{
    private void Start()
    {
        GlobalTimeRewindManager.OnGlobalRewindStarted += OnRewindStarted;
        GlobalTimeRewindManager.OnGlobalRewindStopped += OnRewindStopped;
    }

    private void OnDestroy()
    {
        GlobalTimeRewindManager.OnGlobalRewindStarted -= OnRewindStarted;
        GlobalTimeRewindManager.OnGlobalRewindStopped -= OnRewindStopped;
    }

    private void OnRewindStarted()
    {
        Debug.Log("全局回溯开始！");
        // 例如：播放特效、改变后处理、显示UI等
    }

    private void OnRewindStopped()
    {
        Debug.Log("全局回溯结束！");
        // 例如：停止特效、恢复后处理、隐藏UI等
    }
}
```

---

## 7. 常见问题

### Q1：时钟不显示在左上角？
**A**：`GlobalGameClock`的`OnGUI`方法默认会在左上角显示时钟。如果不显示，请检查：
1. 是否在Play模式下
2. 是否有其他UI遮挡
3. 如果不需要调试显示，可以注释掉`OnGUI`方法

### Q2：弹药系统不工作？
**A**：请确保：
1. `AmmoSystem`组件挂载在玩家身上
2. `WeaponManager`和`AmmoSystem`在同一个GameObject上
3. `WeaponManager`的`Awake`方法中成功获取了`AmmoSystem`

### Q3：掉落物体不掉落？
**A**：请检查：
1. Rigidbody的初始状态是否禁用了`Use Gravity`
2. `autoStartTimer`是否为true
3. `dropDelay`是否大于0

### Q4：音乐不切换？
**A**：请确保：
1. `AudioManager`已创建并分配了音乐片段
2. 调用了正确的静态方法（例如`AudioManager.PlayGameplayMusic()`）
3. 音乐片段不为null

### Q5：交互不触发？
**A**：请检查：
1. Collider是否勾选了`Is Trigger`
2. `playerLayer`是否正确（默认Layer 3）
3. 玩家GameObject的Layer是否匹配
4. Input Action是否正确配置并启用

### Q6：全局回溯时玩家还能移动？
**A**：这个问题已在`PlayerController`中修复，如果仍然可以移动，请确保：
1. 使用的是修改后的`PlayerController`
2. `GlobalTimeRewindManager`存在于场景中
3. 事件订阅成功（检查Awake方法）

---

## 📝 更新日志

### 2024-XX-XX
- ✅ 添加全局时钟系统（可回溯）
- ✅ 添加可回溯掉落物体
- ✅ 添加弹药系统（40秒CD，子弹x2，榴弹x1）
- ✅ 添加交互系统（按住F键胜利）
- ✅ 添加音频管理器（主菜单/游戏/胜利/回溯音乐）
- ✅ 改进全局回溯管理器（自动停止 + 事件驱动）
- ✅ 改进玩家控制器（回溯时禁用输入）

---

## 🎮 快速测试清单

测试所有新功能：

- [ ] 时钟是否在左上角显示并正常计时
- [ ] 时间枪回溯时，时钟是否回溯
- [ ] 掉落物体是否在指定时间后掉落
- [ ] 掉落物体回溯后是否回到天花板
- [ ] 开火时是否消耗子弹
- [ ] 子弹是否在40秒后恢复
- [ ] 投掷榴弹是否消耗榴弹
- [ ] 榴弹是否在40秒后恢复
- [ ] 进入交互区域是否显示提示
- [ ] 按住F键是否显示进度条
- [ ] 按住F键达到时间后是否触发胜利
- [ ] 主菜单是否播放主菜单音乐
- [ ] 开始游戏是否切换到游戏音乐
- [ ] 暂停时音量是否降低
- [ ] 恢复时音量是否恢复
- [ ] 全局回溯时是否播放沉闷音效
- [ ] 全局回溯时玩家是否无法移动
- [ ] 全局回溯结束后玩家是否恢复控制

---

## 📚 参考资料

- [Unity Input System 官方文档](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/index.html)
- [DOTween 官方文档](http://dotween.demigiant.com/documentation.php)
- [URP Volume 官方文档](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/Volumes.html)

---

**注意**：本配置指南基于 Unity 6.2 + URP + Input System + DOTween。如果使用不同版本，部分API可能需要调整。
