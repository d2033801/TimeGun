# HoldToInteract 交互系统配置指南

## 📋 系统简介

**HoldToInteract** 是基于 Unity 6.2 + Input System 的按住交互系统，支持玩家按住F键进行长按蓄力交互，并自动在物体旁边显示**3D世界空间UI提示**。

---

## ✨ 新增功能：世界空间UI显示

### 🎨 自动显示交互UI

- ✅ **无需手动配置Canvas**：使用 `OnGUI` 自动渲染
- ✅ **3D世界空间定位**：UI自动跟随物体位置
- ✅ **实时进度条**：显示按键蓄力进度（0-100%）
- ✅ **文本阴影效果**：增强可读性
- ✅ **颜色状态反馈**：完成时进度条变绿色
- ✅ **完全可配置**：字体大小、颜色、偏移量等

### 🎯 UI包含元素

1. **提示文本**：显示交互提示（例如"按住 F 键激活"）
2. **进度条**：背景条 + 填充条 + 百分比数字
3. **状态颜色**：
   - 按住中：青蓝色填充
   - 完成时：绿色填充

---

## ✅ 主要改进（Tag版本）

### 🔄 从 LayerMask 迁移到 Tag

- ❌ **旧方式**：需要创建专门的 Player Layer
- ✅ **新方式**：使用 Unity 内置的 `"Player"` 标签
- 🎯 **优势**：
  - 无需额外配置 Layer
  - 利用 Unity 内置功能
  - 降低系统耦合度
  - 更符合 Unity 标准实践

### 🆕 现代化语法升级

```csharp
// C# 9.0 模式匹配
if (_interactAction is { enabled: false })

// TryGetComponent 模式
if (TryGetComponent<Collider>(out var col))

// Switch 表达式（Gizmo颜色）
// 更简洁的写法
Gizmos.color = isPlayerInRange ? Color.yellow : Color.cyan;

switch (col)
{
    case BoxCollider box:
        // 处理盒型碰撞体
        break;
    case SphereCollider sphere:
        // 处理球型碰撞体
        break;
}
```

---

## 🛠️ 必需的手动配置

### 1️⃣ 为玩家GameObject设置标签

**操作步骤：**

1. 在 **Hierarchy** 窗口选择玩家 GameObject
2. 在 **Inspector** 顶部找到 **Tag** 下拉菜单
3. 选择 **"Player"**（Unity 内置标签）

```
玩家对象
├─ Tag: Player ← 必须设置！
├─ Layer: 任意
└─ 其他组件...
```

> ⚠️ **重要提示**：如果您的玩家对象是嵌套结构（例如角色控制器是子对象），确保**带有 Collider 的那个对象**设置了 `"Player"` 标签。

---

### 2️⃣ 配置 Input Action

**方法A：使用现有 Input Action Asset**

如果您已经有 Input Action Asset（例如 `PlayerInputActions.inputactions`）：

1. 打开 Input Action Asset
2. 找到或创建 **"Interact"** Action
3. 绑定键位（建议：**F 键**）
4. 设置 Action Type 为 **"Button"**
5. 在 `HoldToInteract` 组件的 Inspector 中，将此 Action 拖入 **Interact Action** 字段

**方法B：创建独立的 Input Action Reference**

如果还没有 Input Action：

1. **Project** 窗口右键 → **Create** → **Input Actions**
2. 命名为 `InteractActions`
3. 添加 Action Map（例如 `"Player"`）
4. 添加 Action：`"Interact"` → 绑定 **Keyboard → F**
5. 保存并在 `HoldToInteract` 组件引用

**推荐配置：**

```
Input Action 配置
├─ Action Name: Interact
├─ Action Type: Button
├─ Control Type: Button
└─ Bindings:
   └─ Keyboard: F (或其他按键)
```

---

### 3️⃣ 在场景中使用

1. 创建交互触发器对象（例如 `EndPoint`、`Door`、`Terminal` 等）
2. 添加 `HoldToInteract` 组件
3. 添加 **Collider** 组件（自动设置为 Trigger）
4. 配置参数（详见下方）

---

## ⚙️ Inspector 参数详解

### 📦 交互配置

| 参数 | 说明 | 默认值 |
|------|------|--------|
| **Hold Duration** | 按住时长（秒） | 2.0 |
| **Interact Action** | Input Action 引用 | 无（必须分配） |
| **Player Tag** | 玩家标签 | "Player" |

### 🎨 世界空间UI配置

| 参数 | 说明 | 默认值 | 推荐范围 |
|------|------|--------|----------|
| **Show World UI** | 是否显示世界空间UI | ✅ true | - |
| **UI Offset** | UI位置偏移（相对物体中心） | (0, 1.5, 0) | Y轴建议 1-3 |
| **Interact Prompt** | 提示文本 | "按住 F 键激活" | 自定义 |
| **Font Size** | 字体大小 | 16 | 12-48 |
| **Progress Bar Width** | 进度条宽度（像素） | 120 | 50-300 |
| **Progress Bar Height** | 进度条高度（像素） | 12 | 5-30 |

### 🎨 UI颜色配置

| 参数 | 说明 | 默认值 |
|------|------|--------|
| **Text Color** | 提示文本颜色 | 白色 |
| **Progress Bar Background** | 进度条背景色 | 深灰色（半透明） |
| **Progress Bar Fill** | 进度条填充色 | 青蓝色 |
| **Completed Color** | 完成时的颜色 | 绿色 |

### 🔧 可选配置

| 参数 | 说明 | 默认值 |
|------|------|--------|
| **Show Debug Gizmo** | 显示调试线框 | ✅ true |

---

## 🎮 使用示例

### 示例1：游戏胜利终点

```csharp
using UnityEngine;
using TimeGun;

public class GameVictoryManager : MonoBehaviour
{
    private void OnEnable()
    {
        HoldToInteract.OnInteractionComplete += OnEndPointActivated;
    }

    private void OnDisable()
    {
        HoldToInteract.OnInteractionComplete -= OnEndPointActivated;
    }

    private void OnEndPointActivated(HoldToInteract interact)
    {
        // 检查是否是终点触发器
        if (interact.CompareTag("EndPoint"))
        {
            Debug.Log("玩家到达终点！游戏胜利！");
            ShowVictoryUI();
        }
    }

    private void ShowVictoryUI()
    {
        // 显示胜利界面逻辑
    }
}
```

### 示例2：UI进度条显示

```csharp
using UnityEngine;
using UnityEngine.UI;
using TimeGun;

public class InteractProgressUI : MonoBehaviour
{
    [SerializeField] private HoldToInteract interactObject;
    [SerializeField] private Image progressBar;
    [SerializeField] private Text promptText;

    private void Update()
    {
        if (interactObject.IsPlayerInRange && !interactObject.IsCompleted)
        {
            // 显示UI
            promptText.text = interactObject.InteractPrompt;
            progressBar.fillAmount = interactObject.Progress;
            
            promptText.gameObject.SetActive(true);
            progressBar.gameObject.SetActive(true);
        }
        else
        {
            // 隐藏UI
            promptText.gameObject.SetActive(false);
            progressBar.gameObject.SetActive(false);
        }
    }
}
```

---

## 🎨 UI效果预览

### 玩家未进入范围
- ❌ 不显示任何UI

### 玩家进入范围（未按键）
```
┌─────────────────────┐
│   按住 F 键激活      │ ← 提示文本（白色+阴影）
└─────────────────────┘
```

### 玩家按住F键（蓄力中）
```
┌─────────────────────┐
│   按住 F 键激活      │ ← 提示文本
└─────────────────────┘
╔════════════════════╗
║████████████░░░░░░░░║ 60%  ← 进度条（青蓝色填充）
╚════════════════════╝
```

### 交互完成
```
┌─────────────────────┐
│   按住 F 键激活      │
└─────────────────────┘
╔════════════════════╗
║████████████████████║ 100% ← 进度条（绿色填充）
╚════════════════════╝
```

---

## 🔍 调试技巧

### Gizmo可视化

在 **Scene** 视图中可以看到：

1. **触发器范围**：
   - 🔵 **青色**：待交互状态
   - 🟡 **黄色**：玩家在范围内
   - 🟢 **绿色**：交互已完成

2. **UI位置标记**（白色）：
   - 小球体：UI显示位置
   - 连线：从物体中心到UI位置

### Inspector调试信息

在运行时查看 `HoldToInteract` 组件的 **调试信息** 面板：
- `Is Player In Range`：玩家是否在范围内
- `Current Hold Time`：当前按住时间
- `Is Completed`：是否已完成交互

### Scene视图预览UI位置

1. 选中带有 `HoldToInteract` 的物体
2. 在 Scene 视图中看到白色的小球和连线
3. 调整 `UI Offset` 参数，实时看到位置变化

---

## 🎯 常见问题排查

### ❌ 问题：UI不显示

**检查清单：**
1. ✅ `Show World UI` 是否勾选？
2. ✅ 场景中是否有 **MainCamera**（标记为 "MainCamera"）？
3. ✅ 玩家是否进入了触发器范围？
4. ✅ UI位置是否在相机视野内？（调整 `UI Offset`）
5. ✅ 是否已完成交互？（完成后UI会隐藏）

### ❌ 问题：UI位置不对

**解决方案：**
- 调整 `UI Offset` 参数
- 对于高大的物体，增大 Y 轴偏移（例如 Y = 3）
- 对于小物体，减小 Y 轴偏移（例如 Y = 0.5）

**推荐配置：**
```
小型物体（宝箱）:    UI Offset = (0, 0.8, 0)
中型物体（门）:      UI Offset = (0, 1.5, 0)
大型物体（终点柱）:  UI Offset = (0, 2.5, 0)
```

### ❌ 问题：按F键没有反应

**检查清单：**
1. ✅ 玩家对象是否设置了 `"Player"` 标签？
2. ✅ Input Action 是否已分配？
3. ✅ Input Action 是否已启用（Enable）？
4. ✅ 玩家的 Collider 是否进入了触发器范围？
5. ✅ `HoldToInteract` 的 Collider 是否勾选了 **Is Trigger**？

### ❌ 问题：UI文字模糊

**解决方案：**
- 增大 `Font Size` 参数（推荐 18-24）
- 确保玩家距离物体不太远
- 如果仍然模糊，考虑使用 TextMeshPro 版本（未来可扩展）

### ❌ 问题：UI在墙后面仍然显示

这是 `OnGUI` 的限制，目前版本不支持深度遮挡。

**解决方案：**
- 在 `OnGUI` 的 `WorldToScreenPoint` 后添加射线检测
- 或者使用 Canvas（需要额外配置）

---

## 📦 公共API参考

```csharp
// 属性
bool IsPlayerInRange      // 玩家是否在范围内
float Progress           // 当前进度 (0-1)
float RemainingTime      // 剩余时间（秒）
bool IsCompleted         // 是否已完成
string InteractPrompt    // 提示文本

// 方法
void ResetInteraction()  // 重置交互状态（允许再次交互）

// 事件
static event Action<HoldToInteract> OnInteractionComplete;
```

---

## 🎨 UI自定义建议

### 颜色主题

**科幻蓝色主题**（默认）：
```
Text Color:              白色
Progress Bar Fill:       青蓝色 (0.3, 0.8, 1.0)
Completed Color:         绿色
```

**警告红色主题**（用于危险交互）：
```
Text Color:              黄色 (1.0, 0.9, 0.2)
Progress Bar Fill:       橙红色 (1.0, 0.4, 0.1)
Completed Color:         深红色 (0.8, 0.1, 0.1)
```

**治疗绿色主题**（用于恢复点）：
```
Text Color:              淡绿色 (0.7, 1.0, 0.7)
Progress Bar Fill:       绿色 (0.3, 1.0, 0.3)
Completed Color:         亮绿色 (0.5, 1.0, 0.5)
```

---

## 📝 设计理念

### 低耦合设计
- 使用 **Tag** 而非 **Layer**，减少依赖
- 事件驱动架构（`OnInteractionComplete`）
- 不依赖特定的玩家控制器
- **OnGUI 渲染**：无需手动配置 Canvas

### 可扩展性
- 支持自定义标签（不限于 `"Player"`）
- 灵活的进度查询接口
- 可重置的交互状态
- 完全可配置的UI样式

### Unity 6.2 最佳实践
- 使用 Input System 而非旧版 Input Manager
- 使用 C# 9.0 现代语法
- 符合 URP 渲染管线要求
- OnGUI 适用于简单UI（复杂UI建议用 Canvas）

---

## 🚀 快速开始检查表

- [ ] 玩家对象设置 `"Player"` 标签
- [ ] 创建或配置 Input Action（绑定F键）
- [ ] 在场景中添加 `HoldToInteract` 组件
- [ ] 添加 Collider 组件（自动设置为Trigger）
- [ ] 在 Inspector 中引用 Input Action
- [ ] 调整 `UI Offset` 使UI显示在合适位置
- [ ] （可选）自定义UI颜色和字体大小
- [ ] （可选）订阅 `OnInteractionComplete` 事件处理游戏逻辑
- [ ] 运行测试，查看 Scene 视图中的 Gizmo 和 Game 视图中的 UI

---

## 🎬 效果展示

### 实际游戏场景示例

**终点激活点：**
```
物体配置：
├─ Collider: Box Collider (Is Trigger ✓)
├─ HoldToInteract:
│  ├─ Hold Duration: 3 秒
│  ├─ UI Offset: (0, 2.5, 0)
│  ├─ Interact Prompt: "按住 F 键到达终点"
│  ├─ Progress Bar Fill: 青蓝色
│  └─ Completed Color: 金色
```

**神秘宝箱：**
```
物体配置：
├─ Collider: Box Collider (Is Trigger ✓)
├─ HoldToInteract:
│  ├─ Hold Duration: 1.5 秒
│  ├─ UI Offset: (0, 0.8, 0)
│  ├─ Interact Prompt: "按住 F 键打开宝箱"
│  ├─ Font Size: 14
│  └─ Progress Bar Width: 100
```

---

## 📞 技术支持

如有问题，请检查：
1. Unity 版本是否为 6.2+
2. 是否已安装 **Input System** 包（Package Manager）
3. 玩家对象的碰撞体设置是否正确
4. 场景中是否有标记为 "MainCamera" 的相机

**祝您游戏开发顺利！** 🎮✨

---

## 🔄 版本历史

### v2.0（当前版本）
- ✅ 添加世界空间UI自动显示
- ✅ 实时进度条 + 百分比显示
- ✅ 可配置的UI样式和颜色
- ✅ Gizmo显示UI位置

### v1.0
- ✅ 基础按住交互功能
- ✅ Tag 检测替代 LayerMask
- ✅ C# 9.0 现代化语法
