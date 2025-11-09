# 快速开始：5分钟配置敌人视野显示

## 🎯 目标

在现有的 Enemy 对象上添加视野可视化，无需修改任何现有代码。

---

## ⚡ 最快方法（60秒）

### 1. 添加组件

选中 Enemy GameObject → **Add Component** → 搜索 `Enemy Vision Visualizer` → 添加

✅ **完成！** 运行游戏即可看到效果。

---

## 🎨 推荐配置（5分钟，更好的视觉效果）

### 第一步：创建透明材质（2分钟）

#### 1.1 创建扇形填充材质

```
Project 窗口右键
→ Create → Material
→ 命名为 "EnemyVisionFill"
```

**在 Inspector 中设置**：
```
Shader: Universal Render Pipeline/Unlit
Surface Type: Transparent  ✅ 必须！
Blending Mode: Alpha
Base Color: 白色 (1, 1, 1, 1)
```

#### 1.2 创建边界线材质（可选）

```
Project 窗口右键
→ Create → Material
→ 命名为 "EnemyVisionLine"
```

**在 Inspector 中设置**：
```
Shader: Universal Render Pipeline/Unlit
Surface Type: Opaque
Base Color: 白色 (1, 1, 1, 1)
```

---

### 第二步：配置组件（2分钟）

#### 2.1 添加组件

选中 Enemy GameObject → **Add Component** → `Enemy Vision Visualizer`

#### 2.2 赋值材质

在 `EnemyVisionVisualizer` 组件的 Inspector 中：

**材质配置区域**：
- **Line Material**：拖入 `EnemyVisionLine`（可选）
- **Fill Material**：拖入 `EnemyVisionFill`（必须）

---

### 第三步：调整颜色（1分钟）

在 `EnemyVisionVisualizer` 组件中调整：

```
Normal Color: (0, 1, 1, 0.3)      // 青色半透明
Alert Color: (1, 0.65, 0, 0.5)    // 橙色
Detected Color: (1, 0, 0, 0.7)    // 红色
```

**颜色说明**：
- RGB：前三位控制颜色
- A（第四位）：控制透明度（0=完全透明，1=不透明）

---

## 🎮 运行测试

1. **运行游戏**
2. **观察效果**：
   - ✅ 敌人周围显示青色扇形视野
   - ✅ 视野随敌人旋转
   - ✅ 敌人警戒时变为橙色
   - ✅ 发现玩家时变为红色

---

## 🐛 问题排查（如果视野不透明）

### 方法 1：检查材质配置

1. 选中 `EnemyVisionFill` 材质
2. 确认 **Surface Type** 是否为 **Transparent**
3. 如果不是，改为 **Transparent** 并保存

### 方法 2：运行时调整

1. **运行游戏**
2. **在 Hierarchy 中找到**：`Enemy → VisionMesh`
3. **选中 VisionMesh**，查看 Inspector
4. **找到 Material** → 点击右侧的圆形图标
5. **在弹出的窗口中**：
   - 将 **Surface Type** 改为 **Transparent**
6. **停止游戏并重新运行**（Unity 会记住更改）

---

## ⚙️ 可选：性能优化配置

如果场景中有多个敌人，建议调整：

```
Update Rate: 30 帧/秒（默认）
Max Visible Distance: 50 米
Arc Segments: 30（降低平滑度提升性能）
```

---

## 📋 配置模板对比

### 模板 1：完整视野（调试用）

```yaml
Enable Visualization: ✅
Arc Segments: 40
Show Arc Border: ✅
Show Boundary Rays: ✅
Fill Arc: ✅
Update Rate: 60 FPS
```

**适用场景**：开发调试、AI 行为测试

---

### 模板 2：简洁视野（发布用）

```yaml
Enable Visualization: ✅
Arc Segments: 30
Show Arc Border: ❌
Show Boundary Rays: ❌
Fill Arc: ✅  # 仅显示填充区域
Update Rate: 20 FPS
Max Visible Distance: 30
```

**适用场景**：正式游戏、性能敏感场景

---

### 模板 3：仅边界线（极简风格）

```yaml
Enable Visualization: ✅
Arc Segments: 20
Show Arc Border: ✅
Show Boundary Rays: ✅
Fill Arc: ❌  # 不填充，仅显示线条
Update Rate: 30 FPS
```

**适用场景**：简约美术风格、低性能设备

---

## 🔥 高级技巧

### 技巧 1：仅在警戒时显示视野

```csharp
// 在 EnemyVisionVisualizer.cs 的 Update() 方法中添加：
if (_enemy.stateMachine?.CurrentState != _enemy.alertState)
{
    HideVisualization();
    return;
}
```

### 技巧 2：根据玩家距离调整透明度

```csharp
// 在 GetCurrentVisionColor() 方法后添加：
if (_enemy.player != null)
{
    float dist = Vector3.Distance(transform.position, _enemy.player.position);
    float alpha = Mathf.Lerp(0.8f, 0.2f, dist / _viewRadius);
    currentColor.a *= alpha; // 距离越远越透明
}
```

### 技巧 3：脉冲动画效果

```csharp
// 在 GetCurrentVisionColor() 后添加：
if (_enemy.CanSeePlayer())
{
    float pulse = Mathf.PingPong(Time.time * 2f, 1f);
    return Color.Lerp(alertColor, detectedColor, pulse);
}
```

---

## 📸 预期效果截图说明

### 正常巡逻状态
- **颜色**：青色半透明
- **扇形**：平滑圆弧
- **边界线**：细线框
- **视野**：跟随敌人头部旋转

### 警戒状态
- **颜色**：橙色（比正常稍微不透明）
- **行为**：敌人在 Alert State 时自动切换

### 发现玩家状态
- **颜色**：红色（更加不透明，强烈警告）
- **触发**：`Enemy.CanSeePlayer()` 返回 true

---

## ✅ 配置完成检查清单

在运行游戏前，确认：

- [ ] `EnemyVisionVisualizer` 组件已添加到 Enemy
- [ ] `Fill Material` 已赋值（或留空使用自动生成）
- [ ] 材质的 **Surface Type** 为 **Transparent**
- [ ] `Enemy` 组件的 `headTransform` 已正确设置
- [ ] `Enemy` 组件的 `viewAngle` 和 `viewRadius` 不为 0

---

## 🆘 还是不工作？

### 检查 Enemy 组件配置

确保 `Enemy` 组件中已配置：
```
Head Transform: 敌人头部的 Transform（不能为空）
View Angle: 90 度（或其他非零值）
View Radius: 10 米（或其他非零值）
```

### 查看控制台日志

运行游戏后检查 Console，查找：
```
[EnemyVisionVisualizer] ...
```

### 检查摄像机距离

如果设置了 `Max Visible Distance`，确保：
```
相机距离 < Max Visible Distance
```

---

## 📝 总结

**最简单配置**：
1. 添加 `EnemyVisionVisualizer` 组件 → 完成

**推荐配置**：
1. 创建 `EnemyVisionFill` 材质（Transparent）
2. 添加组件并赋值材质
3. 调整颜色和性能参数

**预期时间**：5 分钟以内

如有问题，请参考 `README_敌人视野可视化.md` 或 `README_URP透明材质配置.md`。
