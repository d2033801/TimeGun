# URP 透明材质创建指南（Unity 6.2）

## 📌 问题说明

在 URP 中创建透明材质需要正确配置 Surface Type 和 Blend Mode，否则扇形填充区域会显示为不透明的纯色。

---

## 🎨 方法 1：手动创建材质（推荐）

### 步骤 1：创建 LineRenderer 材质

1. **在 Project 窗口中**：
   - 右键 → **Create → Material**
   - 命名为 `EnemyVisionLine`

2. **配置 Shader**：
   - 在 Inspector 中点击 **Shader** 下拉菜单
   - 选择：`Universal Render Pipeline → Unlit`

3. **配置参数**：
   ```
   Surface Type: Opaque（不透明）
   Base Map: 留空
   Base Color: 白色 (1, 1, 1, 1)
   ```

4. **保存材质**

---

### 步骤 2：创建 Mesh 透明材质（⚠️ 关键）

1. **创建新材质**：
   - 右键 → **Create → Material**
   - 命名为 `EnemyVisionFill`

2. **配置 Shader**：
   - Shader：`Universal Render Pipeline → Unlit`

3. **✅ 关键：配置透明模式**：

   在 Inspector 中找到以下选项：

   | 参数 | 设置 | 说明 |
   |------|------|------|
   | **Surface Type** | **Transparent** ✅ | **最关键**！必须设置为透明 |
   | **Blending Mode** | **Alpha** | 标准 Alpha 混合 |
   | **Render Face** | **Both** | 双面渲染（可选） |
   | **Base Map** | 留空 | 不需要纹理 |
   | **Base Color** | (0, 1, 1, 0.3) | 青色 + 30% 透明度 |
   | **Alpha Clipping** | 关闭 | 不需要裁剪 |

4. **验证透明度**：
   - 在 Scene 视图中旋转相机
   - 材质球预览应该是**半透明**的

---

### 步骤 3：应用材质到组件

#### 方法 A：在 Inspector 中拖拽（最简单）

1. **选中 Enemy GameObject**
2. **找到 EnemyVisionVisualizer 组件**
3. **在「材质配置」区域**：
   - 将 `EnemyVisionLine` 拖到 **Line Material** 槽
   - 将 `EnemyVisionFill` 拖到 **Fill Material** 槽

#### 方法 B：通过脚本赋值

```csharp
var visualizer = GetComponent<EnemyVisionVisualizer>();
visualizer.lineMaterial = Resources.Load<Material>("EnemyVisionLine");
visualizer.fillMaterial = Resources.Load<Material>("EnemyVisionFill");
```

---

## 🔧 方法 2：使用脚本自动生成（已内置）

**如果不手动创建材质**，脚本会自动生成临时材质。但由于 Unity 的限制，某些透明属性可能需要手动调整。

### 验证自动生成的材质

1. **运行游戏**
2. **在 Hierarchy 中找到**：
   ```
   Enemy
   └── VisionLine（LineRenderer）
   └── VisionMesh（MeshRenderer）
   ```

3. **选中 VisionMesh**
4. **查看 Inspector → Materials**：
   - 如果材质显示为 `<Auto Generated>`
   - 点击材质右侧的 **圆形图标**
   - 在弹出的材质编辑器中**手动设置 Surface Type 为 Transparent**

---

## 🐛 常见问题排查

### Q1: 扇形区域是纯色不透明

**原因**：材质的 Surface Type 未设置为 Transparent

**解决方案**：

#### 如果使用自定义材质：
1. 在 Project 窗口选中 `EnemyVisionFill` 材质
2. 在 Inspector 中确认 **Surface Type** 为 **Transparent**
3. 确认 **Base Color** 的 Alpha 值 < 1.0

#### 如果使用自动生成的材质：
1. 运行游戏
2. 选中 Hierarchy 中的 `Enemy → VisionMesh`
3. 在 Inspector 中找到 **Material**
4. 点击材质右侧的圆形图标打开材质编辑器
5. 将 **Surface Type** 改为 **Transparent**
6. 停止游戏并重新运行（Unity 会记住运行时的更改）

---

### Q2: 扇形显示但颜色不对

**原因**：材质的 Base Color 覆盖了脚本的颜色设置

**解决方案**：

1. 在材质中将 **Base Color** 设置为**白色** `(1, 1, 1, 1)`
2. 或者直接使用脚本中的颜色配置（推荐）

**工作原理**：
- 脚本通过 `material.color` 动态设置颜色
- 材质的 Base Color 会与脚本颜色**相乘**
- 如果材质是白色，最终颜色 = 脚本颜色 × 白色 = 脚本颜色

---

### Q3: 透明度不对（太透明或太不透明）

**调整方法**：

#### 在脚本中调整（推荐）：
```csharp
// 在 EnemyVisionVisualizer 组件的 Inspector 中
Normal Color: (0, 1, 1, 0.3)  // 最后一位是 Alpha（0-1）
Alert Color: (1, 0.65, 0, 0.5)
Detected Color: (1, 0, 0, 0.7)
```

#### 在材质中调整（需要先设置 Base Color 为白色）：
```
Base Color: (1, 1, 1, 0.5)  // Alpha 控制透明度
```

---

### Q4: 视野在某些角度消失

**原因**：背面剔除（Backface Culling）

**解决方案**：

1. 选中 `EnemyVisionFill` 材质
2. 找到 **Render Face** 选项
3. 设置为 **Both**（双面渲染）

---

### Q5: 材质在运行时不更新颜色

**原因**：材质是共享的（Shared Material）

**解决方案**：

脚本已自动处理（使用 `material` 而非 `sharedMaterial`），如果仍有问题：

1. 确认没有多个 Enemy 共享同一个材质实例
2. 在 `EnemyVisionVisualizer` 的 Inspector 中**不要**勾选材质的 **Static** 选项

---

## 📊 性能对比

| 方案 | CPU 开销 | 内存占用 | 说明 |
|------|----------|----------|------|
| **手动创建材质** | 最低 | 低 | 所有敌人共享材质 |
| **自动生成材质** | 略高 | 中 | 每个敌人独立材质实例 |

**推荐**：生产环境使用手动创建的材质（方法 1）

---

## 🎯 最佳配置模板

### 用于开发/调试

```
Enable Visualization: ✅
Arc Segments: 40
Show Arc Border: ✅
Show Boundary Rays: ✅
Fill Arc: ✅
Normal Color: (0, 1, 1, 0.5)   ← 稍高透明度便于观察
Update Rate: 60 FPS
Max Visible Distance: 0（无限制）
```

### 用于发布/性能优化

```
Enable Visualization: ❌ 或根据需要启用
Arc Segments: 30
Show Arc Border: ❌
Show Boundary Rays: ❌
Fill Arc: ✅（仅填充）
Normal Color: (0, 1, 1, 0.3)
Update Rate: 15-30 FPS
Max Visible Distance: 30 米
```

---

## 🧪 测试清单

运行游戏后，依次检查：

- [ ] **视野显示为扇形**（不是圆形或其他形状）
- [ ] **扇形区域是半透明的**（能看到后面的物体）
- [ ] **边界线清晰可见**（如果启用）
- [ ] **颜色根据敌人状态变化**：
  - [ ] 巡逻时：青色
  - [ ] 警戒时：橙色
  - [ ] 发现玩家时：红色
- [ ] **视野跟随敌人旋转**
- [ ] **敌人死亡后视野消失**

---

## 📝 进阶：自定义着色器

如果需要更高级的视觉效果（例如边缘发光、渐变、纹理等），可以创建自定义 Shader：

### 示例：边缘发光效果

1. 创建新的 Shader Graph（URP）
2. 添加 **Fresnel Node**（菲涅尔效果）
3. 将 Fresnel 输出连接到 **Emission** 和 **Alpha**
4. 在 Shader Graph 中设置：
   ```
   Surface: Transparent
   Blend: Alpha
   ```

### 应用自定义 Shader

1. 将 Shader Graph 保存为材质
2. 在 `EnemyVisionVisualizer` 组件中：
   - 将自定义材质拖到 **Fill Material** 槽

---

## 🔗 相关资源

- **Unity URP 文档**：https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest
- **Shader Graph 教程**：https://learn.unity.com/tutorial/introduction-to-shader-graph
- **透明材质性能优化**：https://docs.unity3d.com/Manual/shader-TransparentPerformance.html

---

## 📌 总结

✅ **最简单的方法**：
1. 创建两个材质（Line + Fill）
2. Fill 材质设置 **Surface Type = Transparent**
3. 拖到组件槽中

✅ **零配置方法**：
- 不创建材质，脚本自动生成
- 运行时手动调整 VisionMesh 的材质透明度

✅ **推荐配置**：
- 开发阶段：使用自动生成（快速迭代）
- 发布阶段：使用手动创建（性能最优）

如有其他问题，请参考脚本内的详细注释或查阅上述文档。
