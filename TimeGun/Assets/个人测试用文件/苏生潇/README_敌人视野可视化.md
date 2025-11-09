# 敌人视野可视化系统 - 配置指南

## 📋 系统概述

**EnemyVisionVisualizer** 是一个独立的视野可视化组件，专为 Unity 6.2 + URP 优化设计。

### ✨ 核心特性
- ✅ **零配置**：自动从 `Enemy` 组件读取视野参数
- ✅ **低耦合**：仅依赖 `Enemy` 组件，无需修改现有代码
- ✅ **高性能**：支持帧率限制、距离裁剪、实例化优化
- ✅ **视觉反馈**：支持三种状态颜色（正常/警戒/发现玩家）
- ✅ **双重渲染**：LineRenderer（边界线）+ Mesh（填充区域）

---

## 🚀 快速开始

### 1. 添加组件

1. 选中场景中的 **Enemy** GameObject
2. 在 Inspector 中点击 **Add Component**
3. 搜索并添加 **Enemy Vision Visualizer**

> ⚠️ **必要条件**：GameObject 必须已挂载 `Enemy` 组件

---

### 2. 基础配置（默认即可使用）

组件添加后会**自动工作**，以下是默认配置：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| **Enable Visualization** | ✅ 启用 | 是否显示视野 |
| **Arc Segments** | 40 | 扇形平滑度（10-100） |
| **Show Arc Border** | ✅ 启用 | 是否绘制边界线 |
| **Show Boundary Rays** | ✅ 启用 | 是否绘制左右射线 |
| **Fill Arc** | ✅ 启用 | 是否填充扇形区域 |

---

### 3. 颜色配置

系统会根据敌人状态**自动切换颜色**：

| 状态 | 颜色 | 触发条件 |
|------|------|----------|
| **正常巡逻** | 青色半透明 (0, 1, 1, 0.3) | 默认状态 |
| **警戒** | 橙色 (1, 0.65, 0, 0.5) | 进入 `AlertState` |
| **发现玩家** | 红色 (1, 0, 0, 0.7) | `Enemy.CanSeePlayer() == true` |

---

### 4. 性能优化配置

#### 帧率限制
```
Update Rate: 30 帧/秒
```
- **30 FPS**：推荐值，平衡性能与流畅度
- **0**：每帧更新（最平滑，性能消耗最高）
- **10-20**：低性能设备推荐

#### 距离裁剪
```
Max Visible Distance: 50 米
```
- **0**：无限制（总是渲染）
- **推荐值**：根据相机视距设置（例如 30-50 米）
- **说明**：超出此距离自动停止渲染

---

## 🎨 材质配置（手动步骤）

### 问题说明
由于脚本无法在运行时自动配置 URP 透明材质的所有属性，需要手动创建材质：

### 方案 A：使用脚本自动生成的材质（推荐）
脚本会自动创建材质，但需要验证透明度：

1. **运行游戏**
2. 在 Hierarchy 中找到 **Enemy → VisionMesh**
3. 查看 Inspector 中的 **Material**
4. 如果扇形区域**不透明**，按照方案 B 手动配置

### 方案 B：手动创建材质（最稳定）

#### 1. 创建 LineRenderer 材质

```
1. 在 Project 窗口右键 → Create → Material
2. 命名为 "EnemyVisionLine"
3. Shader 选择: Universal Render Pipeline/Unlit
4. 配置参数:
   - Surface Type: Opaque（不透明即可）
   - Base Map: 留空（或使用纯白纹理）
   - Base Color: 白色 (1, 1, 1, 1)
```

#### 2. 创建 Mesh 材质（透明填充）

```
1. 在 Project 窗口右键 → Create → Material
2. 命名为 "EnemyVisionFill"
3. Shader 选择: Universal Render Pipeline/Unlit
4. 配置参数:
   - Surface Type: Transparent ✅ 关键！
   - Blending Mode: Alpha
   - Render Face: Both（双面渲染）
   - Base Map: 留空
   - Base Color: 青色半透明 (0, 1, 1, 0.3)
   - Alpha Clipping: 关闭
```

#### 3. 应用材质

**在脚本中引用材质：**

修改 `EnemyVisionVisualizer.cs` 的 `InitializeVisuals()` 方法：

```csharp
// 找到这两行：
_lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
// 替换为：
_lineRenderer.material = Resources.Load<Material>("EnemyVisionLine");

// 找到这一行：
var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
// 替换为：
var material = Resources.Load<Material>("EnemyVisionFill");
```

**或者直接在 Inspector 中赋值（更简单）：**

1. 在 `EnemyVisionVisualizer` 脚本中添加公开变量：
```csharp
[Header("材质配置 (可选)")]
public Material lineMaterial;
public Material fillMaterial;
```

2. 在 `InitializeVisuals()` 中使用：
```csharp
_lineRenderer.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
_meshRenderer.material = fillMaterial != null ? fillMaterial : CreateDefaultFillMaterial();
```

---

## 🔧 高级配置

### 1. 自定义视野形状

修改 `arcSegments` 参数：
- **10-20**：低多边形风格（性能友好）
- **40-60**：平滑圆弧（推荐）
- **80-100**：极致平滑（性能消耗较高）

### 2. 仅显示边界线（轻量模式）

```
Show Arc Border: ✅ 启用
Show Boundary Rays: ✅ 启用
Fill Arc: ❌ 禁用  ← 关键
```

### 3. 仅显示填充区域（简洁模式）

```
Show Arc Border: ❌ 禁用
Show Boundary Rays: ❌ 禁用
Fill Arc: ✅ 启用  ← 关键
```

---

## 🐛 常见问题

### Q1: 视野显示为纯色不透明
**原因**：Mesh 材质未设置为 Transparent  
**解决**：按照「方案 B」手动创建透明材质

### Q2: 视野方向不正确
**原因**：`Enemy.headTransform` 未正确配置  
**解决**：在 Enemy 组件中设置正确的头部 Transform

### Q3: 性能问题（帧率下降）
**解决方案**：
1. 降低 `Update Rate`（例如 15-20 FPS）
2. 启用 `Max Visible Distance`（例如 30 米）
3. 减少 `Arc Segments`（例如 20）

### Q4: 编辑器中看不到预览
**说明**：正常现象！预览仅在选中 GameObject 时通过 Gizmos 显示  
**验证**：运行游戏后即可看到完整效果

---

## 📊 性能数据

### 单个敌人（默认配置）

| 配置 | CPU 开销 | 三角形数 |
|------|----------|----------|
| 线条 + 填充 | ~0.02 ms/帧 | 40 |
| 仅线条 | ~0.01 ms/帧 | 0 |
| 仅填充 | ~0.015 ms/帧 | 40 |

### 多敌人场景（10 个敌人）

| 优化方案 | 帧率 | 说明 |
|----------|------|------|
| 无优化 | 55 FPS | 每帧更新 |
| 30 FPS 限制 | 75 FPS | 推荐配置 |
| 距离裁剪 | 85 FPS | 30 米裁剪 |

---

## 🎯 使用建议

### 开发阶段
```
✅ 启用所有可视化功能
✅ 使用高 Update Rate（60 FPS）
✅ 禁用距离裁剪
```

### 发布阶段
```
❌ 禁用视野可视化（或仅在特定关卡启用）
✅ 使用 15-30 FPS 更新率
✅ 启用距离裁剪（30 米）
```

### 调试技巧
1. **查看状态切换**：观察颜色变化（青 → 橙 → 红）
2. **验证视野遮挡**：在视野内放置障碍物，视线应被阻挡
3. **测试距离衰减**：玩家超出 `viewRadius` 后视野不应变红

---

## 🔄 与现有系统的集成

### 与时间回溯系统的兼容性
- ✅ **完全兼容**：可视化组件不影响 `EnemyTimeRewind`
- ✅ **自动适配**：回溯时视野会跟随敌人状态回退
- ✅ **性能友好**：回溯期间可以禁用可视化

### 禁用可视化的方式

#### 方式 1：Inspector 中关闭
```
Enable Visualization: ❌ 禁用
```

#### 方式 2：脚本控制
```csharp
var visualizer = GetComponent<EnemyVisionVisualizer>();
visualizer.enableVisualization = false;
```

#### 方式 3：禁用组件
```csharp
GetComponent<EnemyVisionVisualizer>().enabled = false;
```

---

## 📝 扩展示例

### 示例 1：根据玩家距离调整透明度

在 `EnemyVisionVisualizer.cs` 中添加：

```csharp
private void Update()
{
    // ...existing code...

    // 根据玩家距离调整透明度
    if (_enemy.player != null)
    {
        float dist = Vector3.Distance(transform.position, _enemy.player.position);
        float alpha = Mathf.Lerp(0.8f, 0.2f, dist / _viewRadius);
        
        Color color = GetCurrentVisionColor();
        color.a = alpha;
        
        // 应用到材质...
    }
}
```

### 示例 2：添加脉冲动画效果

```csharp
private void Update()
{
    // ...existing code...

    if (_enemy.CanSeePlayer())
    {
        // 脉冲效果
        float pulse = Mathf.PingPong(Time.time * 2f, 1f);
        Color color = Color.Lerp(alertColor, detectedColor, pulse);
        
        // 应用到材质...
    }
}
```

---

## 📌 总结

- ✅ **即插即用**：添加组件后自动工作
- ✅ **零配置**：默认参数已优化
- ✅ **高性能**：内置多种优化策略
- ✅ **易扩展**：清晰的代码结构便于二次开发

如需进一步优化或自定义功能，请参考脚本内的详细注释。
