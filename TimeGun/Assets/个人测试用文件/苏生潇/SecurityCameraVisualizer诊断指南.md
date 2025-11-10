# SecurityCameraVisualizer 诊断和修复指南

## 问题描述
SecurityCameraVisualizer 不能显示半球形景象，在摄像头下面走过时不会变红且没有色块填充。

## 常见问题和解决方案

### 1. ? 可视化组件没有启用

**检查清单：**
- [ ] `Enable Visualization` 是否勾选
- [ ] `Fill Volume` 是否勾选
- [ ] `Show Wireframe` 是否勾选

**解决方案：**
在 Inspector 中确保这些选项都已勾选。

---

### 2. ? 垂直视野参数配置不当

**推荐配置（用于半球形视野）：**
```
Vertical FOV = 90度
Vertical Offset = -45度
```

**说明：**
- `Vertical FOV = 90度`：提供从 6 点钟（正下方）到 9 点钟（水平）的视野覆盖
- `Vertical Offset = -45度`：将视野中心向下偏移，使其覆盖下方区域
- 计算：-45° + (-45°) = -90°（6点钟，正下方）到 -45° + 45° = 0°（9点钟，水平）

---

### 3. ? SecurityCamera 检测逻辑问题

**确认 SecurityCamera.cs 已修复：**
`CanSeePlayer()` 方法应该使用完整的 3D 向量检测：

```csharp
public bool CanSeePlayer()
{
    if (_player == null || cameraHead == null) return false;

    // ? 使用真实的3D方向向量（包含Y轴）
    Vector3 dirToPlayer = (_player.position - cameraHead.position).normalized;
    float distToPlayer = Vector3.Distance(cameraHead.position, _player.position);

    if (distToPlayer > viewRadius) return false;

    // ? 角度检查（使用完整的3D向量，支持垂直角度检测）
    float angle = Vector3.Angle(cameraHead.forward, dirToPlayer);
    if (angle > viewAngle / 2f) return false;

    if (Physics.Raycast(cameraHead.position, dirToPlayer, distToPlayer, obstacleMask))
        return false;

    return true;
}
```

---

### 4. ? 材质/着色器问题

**URP 渲染管线检查：**
1. 项目使用 URP（Universal Render Pipeline）
2. 材质自动使用 `Universal Render Pipeline/Unlit` 着色器
3. 如果找不到该着色器，会回退到 `Transparent/Diffuse`

**Console 检查：**
查看是否有以下警告：
```
[SecurityCameraVisualizer] 未找到URP/Unlit着色器
```

**解决方案：**
- 确保项目正确配置了 URP
- 检查 Project Settings -> Graphics -> Scriptable Render Pipeline Settings

---

### 5. ? Mesh 没有正确生成

**启用调试日志：**
在 Inspector 中勾选 `Enable Debug Log`，查看 Console 输出：

```
[SecurityCameraVisualizer] 初始化完成 - vFOV:90, vOffset:-45, hFOV:60, radius:15
[SecurityCameraVisualizer] LineRenderer 已创建
[SecurityCameraVisualizer] Volume Mesh 已创建 - Material:..., Color:...
[SecurityCameraVisualizer] 检测玩家中 - Progress:0.XX, Color:(...)
```

---

### 6. ? 渲染距离限制

**检查：**
- `Max Render Distance` 是否设置过小
- 建议设置为 `50` 或 `0`（无限制）

---

### 7. ? 更新频率问题

**检查：**
- `Update Rate` 建议设置为 `30`
- 不要设置为 `0`（会导致每帧更新，可能影响性能）

---

### 8. ? 遮挡检测干扰

**临时禁用遮挡检测测试：**
1. 取消勾选 `Enable Occlusion Test`
2. 观察是否能看到视锥体
3. 如果能看到，说明遮挡检测配置有问题

**遮挡 Mask 配置：**
- 确保 `Occlusion Mask` 包含了正确的层级（如 Default、Ground 等）
- 确保玩家所在层不在 `Occlusion Mask` 中

---

### 9. ? 层级遮罩配置问题

**SecurityCamera 组件检查：**
```
Player Mask: 应包含玩家所在层（Player 层）
Obstacle Mask: 应包含墙壁、地面等（Default、Ground 层）
```

**SecurityCameraVisualizer 组件：**
```
Occlusion Mask: 应该和 SecurityCamera 的 Obstacle Mask 一致
```

---

### 10. ? 颜色透明度问题

**颜色配置检查：**
```
Normal Color:     (0, 1, 0, 0.3)  // 绿色，30% 透明度
Detecting Color:  (1, 0.65, 0, 0.5)  // 橙色，50% 透明度
Alarm Color:      (1, 0, 0, 0.7)  // 红色，70% 透明度
```

**如果看不到：**
- 尝试提高 Alpha 值到 `0.8` 或 `1.0`
- 检查场景中是否有其他透明物体遮挡

---

## 完整的排查流程

### 步骤 1：基础检查
1. ? 确保 SecurityCamera 和 SecurityCameraVisualizer 都在同一个 GameObject 上
2. ? 确保 Camera Head 已正确设置
3. ? 在 Scene 视图中选中摄像头，查看 Gizmos 是否显示

### 步骤 2：参数配置
```
SecurityCameraVisualizer:
├─ Enable Visualization: ?
├─ Segments: 30
├─ Show Wireframe: ?
├─ Fill Volume: ?
├─ Enable Occlusion Test: ?
├─ Vertical FOV: 90
├─ Vertical Offset: -45
├─ Occlusion Ray Count: 15
├─ Normal Color: (0, 1, 0, 0.8)  // 提高透明度测试
├─ Update Rate: 30
└─ Enable Debug Log: ?  // 启用调试
```

### 步骤 3：运行时测试
1. 进入 Play 模式
2. 移动玩家到摄像头视野内（包括下方）
3. 观察 Console 输出：
   ```
   [SecurityCamera] SecurityCamera_1 发现玩家！
   [SecurityCameraVisualizer] 检测玩家中 - Progress:0.XX
   ```
4. 观察 Game 视图中是否有颜色变化

### 步骤 4：Scene 视图调试
1. 在 Play 模式下切换到 Scene 视图
2. 选中 SecurityCamera
3. 查看是否有：
   - 绿色/橙色/红色的 3D 视锥体
   - 从摄像头指向玩家的红色射线（如果能看到玩家）

---

## 常见错误消息

### "未找到Player标签的对象"
```
[SecurityCamera] SecurityCamera_1 未找到Player标签的对象！
```
**解决方案：** 确保玩家 GameObject 的 Tag 设置为 "Player"

### "未找到URP/Unlit着色器"
```
[SecurityCameraVisualizer] 未找到URP/Unlit着色器，使用默认透明着色器
```
**解决方案：** 检查 URP 包是否正确安装（Window -> Package Manager -> Universal RP）

---

## 性能优化建议

### 多个摄像头场景
```
Update Rate: 15-20 帧/秒  // 降低更新频率
Max Render Distance: 30-50  // 限制渲染距离
Segments: 20-25  // 减少分段数
Occlusion Ray Count: 10-15  // 减少遮挡检测射线
```

### 单个摄像头场景
```
Update Rate: 30 帧/秒
Max Render Distance: 0（无限制）
Segments: 30-40
Occlusion Ray Count: 15-20
```

---

## Inspector 截图参考

### SecurityCamera 组件
```
[Swing Settings]
  Swing Angle: 45
  Swing Speed: 30

[View Settings]
  View Angle: 60          // 水平视野角度
  View Radius: 15
  Camera Head: ViewPoint
  Player Mask: Player
  Obstacle Mask: Default, Ground

[Detection Settings]
  Detect Duration: 0.5
```

### SecurityCameraVisualizer 组件
```
[Visualization Settings]
  Enable Visualization: ?
  Segments: 30
  Show Wireframe: ?
  Fill Volume: ?
  Enable Occlusion Test: ?

[3D Frustum Config]
  Vertical FOV: 90        // 垂直视野 90 度
  Vertical Offset: -45    // 向下偏移 45 度
  Plane Height Offset: 0
  Near Clip: 0.5

[Occlusion Detection]
  Occlusion Ray Count: 15
  Occlusion Mask: Default, Ground

[Color Config]
  Normal Color: (0, 1, 0, 0.3)
  Detecting Color: (1, 0.65, 0, 0.5)
  Alarm Color: (1, 0, 0, 0.7)

[Performance]
  Update Rate: 30
  Max Render Distance: 50

[Debug]
  Enable Debug Log: ?
```

---

## 联系和支持

如果以上方法都无法解决问题，请提供以下信息：
1. Unity 版本
2. URP 版本
3. Console 完整日志
4. Inspector 截图
5. Scene 视图截图（选中 SecurityCamera）

---

**最后更新：** 2024
**作者：** GitHub Copilot
