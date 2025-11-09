# 快速入门 - 10分钟配置指南

本文档提供最快速的配置步骤，帮助你快速上手所有新功能。

---

## ✅ 必需配置（必做）

### 1. 玩家配置（5分钟）

**在已有的Player GameObject上：**

1. **添加弹药系统**：
   - 选择玩家GameObject
   - 添加组件：`Ammo System`
   - 保持默认配置（子弹x2，榴弹x1，40秒CD）

2. **确认WeaponManager配置**：
   - 检查`WeaponManager`是否存在
   - 确认`AmmoSystem`和`WeaponManager`在同一GameObject上

3. **确认PlayerController配置**：
   - 检查`PlayerController`组件是否存在
   - 无需额外配置（已自动订阅全局回溯事件）

✅ **测试**：运行游戏，按住瞄准键，点击开火，检查Console是否显示"子弹不足"（开火2次后）

---

### 2. 时钟系统（2分钟）

**自动创建（推荐）：**
- 无需手动操作，脚本会自动创建

**手动创建（可选）：**
1. 创建空GameObject，命名为`GlobalGameClock`
2. 添加组件：`Global Game Clock`
3. 保持默认配置

✅ **测试**：运行游戏，检查左上角是否显示"游戏时间: 00:00"

---

### 3. 音频管理器（3分钟）

**步骤：**
1. 创建空GameObject，命名为`AudioManager`
2. 添加组件：`Audio Manager`
3. **分配音乐片段**（必需）：
   - Main Menu Music：主菜单音乐
   - Gameplay Music：游戏中音乐
   - Victory Music：胜利音乐
   - Rewind Music：回溯音效（建议低沉、沉闷的音效）
4. 保持其他默认配置

✅ **测试**：
- 主菜单调用：`AudioManager.PlayMainMenuMusic()`
- 开始游戏调用：`AudioManager.PlayGameplayMusic()`

---

## 🎨 可选配置（按需选择）

### 4. 掉落物体（天花板陷阱）

**如果需要掉落陷阱功能：**

1. 选择要掉落的GameObject（必须有Rigidbody）
2. **配置Rigidbody**：
   - 禁用 `Use Gravity`（初始状态）
   - 勾选 `Is Kinematic`（可选，防止抖动）
3. 添加组件：`Rewindable Falling Object`
4. 配置参数：
   - Drop Delay：5秒（默认）
   - Auto Start Timer：勾选
   - Drop Sound：可选音效

✅ **测试**：运行游戏，等待5秒，物体应该掉落

---

### 5. 交互系统（按住F键胜利）

**如果需要胜利条件：**

1. 创建触发器GameObject（例如：终点区域）
2. **添加Collider**：
   - 添加Box Collider或Sphere Collider
   - **勾选 `Is Trigger`**（重要！）
3. 添加组件：`Hold To Interact`
4. 配置参数：
   - Hold Duration：2秒（默认）
   - Interact Action：选择Input Action（绑定F键）
   - Player Layer：选择玩家层（默认Layer 3）

5. **订阅胜利事件**（在其他脚本中）：

```csharp
void Start()
{
    HoldToInteract.OnInteractionComplete += OnVictory;
}

void OnVictory(HoldToInteract interact)
{
    Debug.Log("游戏胜利！");
    AudioManager.PlayVictoryMusic();
}
```

✅ **测试**：
- 进入触发器区域
- 按住F键2秒
- 检查是否触发胜利事件

---

## 🎯 集成到现有UI

### 弹药UI（建议）

在你的HUD脚本中添加：

```csharp
using TimeGun;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI bulletText;
    [SerializeField] private TextMeshProUGUI grenadeText;

    void Start()
    {
        // 订阅弹药变化事件
        AmmoSystem.OnAmmoChanged += OnAmmoChanged;
    }

    void OnDestroy()
    {
        AmmoSystem.OnAmmoChanged -= OnAmmoChanged;
    }

    void OnAmmoChanged(AmmoSystem.AmmoType type, int current, int max)
    {
        if (type == AmmoSystem.AmmoType.Bullet)
            bulletText.text = $"子弹: {current}/{max}";
        else if (type == AmmoSystem.AmmoType.Grenade)
            grenadeText.text = $"榴弹: {current}/{max}";
    }
}
```

---

### 时钟UI（建议）

在你的HUD脚本中添加：

```csharp
using TimeRewind;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI clockText;

    void Update()
    {
        if (GlobalGameClock.Instance != null)
        {
            clockText.text = GlobalGameClock.Instance.FormattedTime;
        }
    }
}
```

---

### 交互提示UI（建议）

```csharp
using TimeGun;

public class InteractUI : MonoBehaviour
{
    [SerializeField] private GameObject interactPanel;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI promptText;

    private HoldToInteract _currentInteract;

    void Update()
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

        // 更新UI
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

---

## 🔧 MainMenu集成示例

修改你的`MainMenu.cs`：

```csharp
using TimeGun;

public class MainMenu : MonoBehaviour
{
    void Start()
    {
        // 播放主菜单音乐
        AudioManager.PlayMainMenuMusic();
    }

    public void StartGame()
    {
        // ... 现有代码 ...

        // 切换到游戏音乐
        AudioManager.PlayGameplayMusic();
    }

    public void ShowEscMenu()
    {
        // ... 现有代码 ...
        Time.timeScale = 0;

        // 降低音量
        AudioManager.EnterPauseState();
    }

    public void ResumeGame()
    {
        // ... 现有代码 ...
        Time.timeScale = 1;

        // 恢复音量
        AudioManager.ExitPauseState();
    }
}
```

---

## 📝 Input System配置

### 创建Interact动作（交互系统需要）

1. 打开Input Actions资源
2. 创建新动作：`Interact`
3. 绑定键位：`F`（Keyboard）
4. 动作类型：`Button`
5. 保存

### 在HoldToInteract中分配

- 在Inspector中，`Interact Action`字段选择刚创建的动作

---

## ⚠️ 常见问题快速修复

### 问题1：弹药不消耗
**检查清单**：
- [ ] `AmmoSystem`组件存在吗？
- [ ] `WeaponManager`和`AmmoSystem`在同一GameObject上吗？
- [ ] `WeaponManager`的`Awake`方法中成功获取`AmmoSystem`吗？（检查Console）

### 问题2：时钟不显示
**解决方案**：
- 时钟会在左上角自动显示
- 如果不需要调试显示，可以注释掉`GlobalGameClock.OnGUI`方法

### 问题3：音乐不播放
**检查清单**：
- [ ] `AudioManager`存在吗？
- [ ] 音乐片段已分配吗？（不能为null）
- [ ] 调用了正确的静态方法吗？（例如`AudioManager.PlayGameplayMusic()`）
- [ ] 音量是否为0？（检查AudioMixer设置）

### 问题4：交互不触发
**检查清单**：
- [ ] Collider勾选了`Is Trigger`吗？
- [ ] 玩家Layer和`playerLayer`匹配吗？
- [ ] Input Action已分配并启用吗？
- [ ] 玩家真的进入了触发器区域吗？（检查Gizmo）

### 问题5：全局回溯时玩家还能移动
**解决方案**：
- 确保使用的是修改后的`PlayerController`
- 检查`GlobalTimeRewindManager`是否存在于场景中
- 检查Console是否有事件订阅错误

---

## 🚀 快速测试流程

### 测试1：基础功能（2分钟）
1. 运行游戏
2. ✅ 左上角显示时钟
3. ✅ 按住瞄准键，开火2次，第3次应该无法开火（弹药不足）
4. ✅ 等待40秒，弹药应该恢复
5. ✅ 音乐正常播放

### 测试2：回溯功能（2分钟）
1. 移动玩家，观察时钟累加
2. 触发全局回溯
3. ✅ 时钟倒退
4. ✅ 玩家无法移动
5. ✅ 音乐切换到沉闷音效
6. 停止回溯
7. ✅ 玩家恢复控制
8. ✅ 音乐恢复正常

### 测试3：掉落物体（1分钟）
1. 场景中有掉落物体
2. 运行游戏，等待5秒
3. ✅ 物体掉落
4. 触发全局回溯
5. ✅ 物体回到天花板

### 测试4：交互系统（1分钟）
1. 进入交互区域
2. ✅ 显示提示UI
3. 按住F键2秒
4. ✅ 触发胜利事件
5. ✅ 播放胜利音乐

---

## 📚 下一步

完成快速配置后，建议阅读：
1. [完整功能配置指南](./README_完整功能配置指南.md) - 详细配置步骤
2. [技术实现说明](./README_技术实现说明.md) - 架构设计和扩展建议

---

## 💡 提示

- 所有新增组件都有Inspector中的调试信息（只读字段）
- 使用Gizmo可以可视化触发器范围（`showDebugGizmo`选项）
- 建议创建独立测试场景，避免污染主场景

---

**祝你配置顺利！如有问题请查阅完整文档或检查Console输出。**
