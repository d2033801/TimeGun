# 安全摄像头（CCTV）系统使用指南

## 概述
这是一个模仿Enemy系统的闭路电视安全摄像头，具有以下功能：
- ? 90度水平来回摆动
- ? 扇形视野检测系统
- ? 发现玩家后0.5秒触发死亡
- ? **运行时3D视锥体可视化（全新升级）**
- ? Gizmos场景编辑器可视化

## ?? 新特性：3D 视锥体可视化

`SecurityCameraVisualizer` 已升级为完整的 3D 视锥体可视化系统，完全模仿 `EnemyVisionVisualizer3D` 的功能：

### 3D 可视化功能
- ? **3D 视锥体体积显示**（而非平面扇形）
- ? **垂直视野角度支持**（可调节上下视角范围）
- ? **遮挡检测**（墙壁后的视野自动缩短）
- ? **实时颜色渐变**（绿色→橙色→红色）
- ? **线框 + 体积填充**双模式
- ? **性能优化**（LOD、更新频率控制）

### 与 2D 版本的对比

| 特性 | 2D 版本（旧） | 3D 版本（新） |
|------|-------------|-------------|
| 视野形状 | 平面扇形 | 3D 视锥体 |
| 垂直视角 | ? | ? |
| 遮挡检测 | ? | ? |
| 真实感 | 低 | 高 |
| 性能开销 | 低 | 中等 |

## 快速开始

### 1. 创建安全摄像头物体

在Unity中创建一个新的GameObject：

```
1. 右键 Hierarchy -> Create Empty
2. 重命名为 "SecurityCamera"
3. 添加 SecurityCamera 组件
4. （可选）添加 SecurityCameraVisualizer 组件用于运行时3D可视化
```

### 2. 基础配置

#### SecurityCamera 组件参数：

**摆动设置：**
- `Swing Angle`: 摆动角度范围（默认45度，总共90度）
- `Swing Speed`: 摆动速度（默认30度/秒）

**视野设置：**
- `View Angle`: 视野角度（默认60度）
- `View Radius`: 视野半径（默认15米）
- `Camera Head`: 摄像头头部Transform（视线检测起点）
- `Player Mask`: 玩家图层（通常是Default或Player层）
- `Obstacle Mask`: 障碍物图层（用于遮挡检测）

**检测设置：**
- `Detect Duration`: 触发警报所需时间（默认0.5秒）

**视觉效果（可选）：**
- `Camera Lens`: 摄像头镜头物体（用于旋转动画）
- `Alarm Light`: 警报灯光（检测到玩家时变红）
- `Normal Light Color`: 正常状态灯光颜色（默认绿色）
- `Alarm Light Color`: 警报状态灯光颜色（默认红色）

#### SecurityCameraVisualizer 组件参数（3D版本）：

**可视化设置：**
- `Enable Visualization`: 是否启用视野可视化
- `Segments`: 视锥体分段数（20-40，越高越平滑）
- `Show Wireframe`: 是否绘制视锥体线框
- `Fill Volume`: 是否填充视锥体体积
- `Enable Occlusion Test`: 是否启用遮挡检测

**3D 视锥体配置：**
- `Vertical FOV`: 垂直视野角度（0-90度，默认30度）
- `Vertical Offset`: 垂直偏移（-45到45度，调整视野俯仰）
- `Plane Height Offset`: 平面高度偏移（-2到2米，调整视锥体离地高度）
- `Near Clip`: 近裁剪距离（默认0.5米）

**遮挡检测配置：**
- `Occlusion Ray Count`: 遮挡检测射线数量（5-30，越多越精确但性能消耗越大）
- `Occlusion Mask`: 遮挡检测使用的LayerMask（通常与Obstacle Mask相同）

**颜色配置：**
- `Normal Color`: 正常状态颜色（绿色半透明）
- `Detecting Color`: 检测中颜色（橙色）
- `Alarm Color`: 警报状态颜色（红色）

**性能优化：**
- `Update Rate`: 更新频率（0-60帧/秒，0表示每帧更新）
- `Max Render Distance`: 最大渲染距离（超出距离不渲染，0表示无限制）

### 3. 推荐的物体层级结构

```
SecurityCamera (Root)
├── SecurityCamera (Component)
├── SecurityCameraVisualizer (Component, 可选)
├── CameraBody (模型)
├── CameraLens (镜头，设置到Camera Lens字段)
│   └── ViewPoint (空物体，设置到Camera Head字段)
└── AlarmLight (Light组件，设置到Alarm Light字段)
```

### 4. 设置步骤详解

#### Step 1: 创建摄像头模型
```
1. 在SecurityCamera下创建子物体作为摄像头本体
2. 添加3D模型或简单的Cube/Cylinder作为外观
3. 创建一个CameraLens子物体表示镜头
```

#### Step 2: 设置检测起点
```
1. 在CameraLens下创建一个空物体"ViewPoint"
2. 将ViewPoint设置到SecurityCamera的Camera Head字段
3. ViewPoint的位置应该是摄像头镜头的位置
```

#### Step 3: 添加灯光效果（可选）
```
1. 创建一个子物体"AlarmLight"
2. 添加Light组件（建议使用Spot Light）
3. 将AlarmLight设置到SecurityCamera的Alarm Light字段
4. 调整灯光的Intensity、Range等参数
```

#### Step 4: 配置图层遮罩
```
1. Player Mask: 勾选玩家所在的图层
2. Obstacle Mask: 勾选墙壁、障碍物等图层
```

#### Step 5: 添加3D可视化（强烈推荐）
```
1. 在SecurityCamera物体上添加SecurityCameraVisualizer组件
2. 调整Segments（视锥体分段数）以控制平滑度（建议30）
3. 启用Fill Volume（填充视锥体体积）
4. 启用Enable Occlusion Test（遮挡检测，让视野更真实）
5. 调整Vertical FOV（垂直视野角度，建议30度）
6. 自定义颜色：Normal Color（正常）、Detecting Color（检测中）、Alarm Color（警报）
```

## 工作原理

### 摆动系统
- 摄像头会在初始朝向的左右各摆动45度（可配置）
- 到达边界时自动反向
- 使用平滑的角度插值

### 检测系统
1. **距离检测**: 玩家必须在视野半径内
2. **角度检测**: 玩家必须在视野角度范围内
3. **遮挡检测**: 使用Raycast检测视线是否被障碍物遮挡

### 警报触发
1. 摄像头检测到玩家
2. 开始计时（灯光从绿色渐变到红色）
3. 达到检测时间（默认0.5秒）后触发警报
4. 调用玩家的Die()方法
5. 玩家死亡，显示死亡面板

### 3D 可视化系统（新）
- **绿色3D视锥体**: 正常巡逻状态
- **橙色到红色渐变**: 正在检测玩家（颜色随进度变化）
- **红色3D视锥体**: 警报触发状态
- **遮挡效果**: 遇到墙壁时视锥体自动缩短，更真实
- **垂直视野**: 可以看到摄像头的上下视角范围

## 使用示例

### 示例1: 基础摄像头
```csharp
// 无需代码，直接在Inspector配置即可
```

### 示例2: 自定义检测回调（扩展）
如果需要自定义警报行为，可以修改SecurityCamera.cs的`TriggerAlarm()`方法：

```csharp
private void TriggerAlarm()
{
    _alarmTriggered = true;
    Debug.Log($"[SecurityCamera] {gameObject.name} 触发警报！");

    // 自定义行为：播放警报音效
    AudioSource audioSource = GetComponent<AudioSource>();
    if (audioSource != null)
    {
        audioSource.Play();
    }

    // 自定义行为：触发其他系统
    // 例如：通知所有敌人
    // 例如：锁定门
    
    // 杀死玩家
    if (_player != null)
    {
        var playerController = _player.GetComponent<TimeGun.PlayerController>();
        if (playerController != null && !playerController.IsDead)
        {
            playerController.Die(transform);
        }
    }
}
```

### 示例3: 多个摄像头联动
```csharp
// 可以在场景中放置多个SecurityCamera
// 每个摄像头独立检测和触发
// 建议调整摆动速度和角度，避免视野盲区
```

### 示例4: 调整3D视野效果
```csharp
// 天花板摄像头（向下俯视）
verticalOffset = -30f; // 视野向下偏移30度
verticalFOV = 45f;     // 增大垂直视角

// 走廊摄像头（水平扫描）
verticalOffset = 0f;   // 水平视野
verticalFOV = 20f;     // 窄垂直视角

// 广场摄像头（宽视野）
verticalFOV = 60f;     // 大垂直视角
occlusionRayCount = 25; // 更多射线，更精确的遮挡检测
```

## 调试技巧

### Scene视图可视化
- 选中SecurityCamera物体
- Scene视图会显示：
  - 黄色球体：视野半径
  - 青色线条：视野锥形边界
  - 蓝色线条：摆动范围（运行时）
  - 红色线条：指向玩家的检测射线（运行时）
  - **黄色射线网格**：遮挡检测射线预览（编辑器模式）

### 运行时可视化（3D版本）
- 添加SecurityCameraVisualizer组件
- 运行时会在Game视图显示**3D视锥体**
- 颜色渐变表示检测状态
- **遮挡效果**：视锥体会在墙壁处自动缩短
- **线框模式**：可以看到视锥体的边界
- **体积填充**：半透明的3D体积填充

### 常见问题排查
1. **摄像头不摆动？**
   - 检查Swing Speed是否大于0
   - 检查Swing Angle是否大于0

2. **检测不到玩家？**
   - 检查Player Mask是否正确设置
   - 确认玩家在视野半径内
   - 使用Gizmos查看视野范围
   - 检查Obstacle Mask是否误将玩家包含进去

3. **玩家在视野内但不触发？**
   - 检查是否有障碍物遮挡（Obstacle Mask）
   - 确认Camera Head位置设置正确
   - 检查玩家是否已经死亡

4. **灯光不变色？**
   - 确认Alarm Light字段已设置
   - 检查Light组件是否启用

5. **3D视锥体不显示？**
   - 确认Enable Visualization已勾选
   - 检查Max Render Distance设置（主相机距离是否在范围内）
   - 确认URP渲染管线已正确配置
   - 查看Console是否有材质/着色器警告

6. **遮挡检测不工作？**
   - 确认Enable Occlusion Test已勾选
   - 检查Occlusion Mask是否正确设置
   - 增加Occlusion Ray Count以提高精度
   - 确认障碍物有Collider组件

7. **性能问题？**
   - 降低Segments（分段数）至20-25
   - 降低Occlusion Ray Count至10-15
   - 增大Update Rate（降低更新频率至15-30）
   - 设置Max Render Distance限制渲染距离

## 性能优化建议

### 1. 视野可视化优化
- **多摄像头场景**：如果有大量摄像头，考虑：
  - 禁用部分SecurityCameraVisualizer
  - 降低Update Rate（更新频率至15-30帧/秒）
  - 设置Max Render Distance（超出50米不渲染）
  - 降低Segments（分段数至20-25）

### 2. 遮挡检测优化
- **Occlusion Ray Count**：
  - 简单场景：10-15条射线
  - 复杂场景：20-25条射线
  - 避免超过30条（性能开销大）

### 3. Mesh 优化
- **Segments**：
  - 远距离摄像头：20-25
  - 近距离摄像头：30-35
  - 避免超过40（顶点数过多）

### 4. 检测频率优化
- SecurityCamera每帧都会检测玩家
- 如需优化，可以修改Update()改为按间隔检测：

```csharp
private float _detectionInterval = 0.1f; // 每0.1秒检测一次
private float _detectionTimer = 0f;

void Update()
{
    // ...摆动逻辑...

    _detectionTimer += Time.deltaTime;
    if (_detectionTimer >= _detectionInterval)
    {
        _detectionTimer = 0f;
        UpdatePlayerDetection(); // 仅在间隔时检测
    }
}
```

### 5. LOD（细节层次）优化
- 根据距离动态调整Segments：

```csharp
float distToCamera = Vector3.Distance(transform.position, Camera.main.transform.position);
if (distToCamera < 20f)
    segments = 30; // 近距离：高精度
else if (distToCamera < 40f)
    segments = 20; // 中等距离：中等精度
else
    segments = 15; // 远距离：低精度
```

### 6. Gizmos
- Gizmos仅在Scene视图显示，不影响游戏性能

## 扩展功能建议

### 1. 添加时间回溯支持
参考EnemyTimeRewind，可以创建SecurityCameraTimeRewind组件：
- 录制摆动角度
- 录制检测状态
- 回溯时恢复状态

### 2. 添加声音效果
```csharp
public AudioClip detectSound;  // 检测到玩家的声音
public AudioClip alarmSound;   // 警报声音

// 在UpdatePlayerDetection()中添加：
if (canSee && !_playerDetected)
{
    AudioSource.PlayClipAtPoint(detectSound, transform.position);
}
```

### 3. 网络同步
如果是多人游戏，需要同步摆动状态和检测状态

### 4. 关闭/破坏系统
```csharp
public bool isDestroyed = false;

void Update()
{
    if (isDestroyed) return;
    // ...原有逻辑
}

public void Destroy()
{
    isDestroyed = true;
    // 播放破坏特效
    // 禁用灯光
    // 禁用可视化
    
    // 禁用3D可视化
    var visualizer = GetComponent<SecurityCameraVisualizer>();
    if (visualizer != null)
        visualizer.enableVisualization = false;
}
```

### 5. 多层检测（增强版）
```csharp
// 在SecurityCamera中添加：
[Header("高级检测")]
public float immediateDetectionRadius = 3f; // 近距离立即触发

bool CanSeePlayer()
{
    // 原有逻辑...
    
    // 如果玩家非常近，立即触发
    if (distToPlayer < immediateDetectionRadius)
    {
        _detectTimer = detectDuration; // 立即满计时
        return true;
    }
    
    // 否则正常检测
    return originalCanSeePlayer();
}
```

## 与其他系统的集成

### Enemy系统
- SecurityCamera和Enemy使用相同的视野检测逻辑
- 可以配合使用，形成立体防御网
- **3D可视化风格一致**，视觉统一

### 智能门系统
- 摄像头触发警报时可以锁定智能门
- 需要在TriggerAlarm()中添加门控制逻辑

### 时间回溯系统
- 玩家可以用时间倒流躲避摄像头
- 摄像头会回溯到之前的摆动角度
- **3D视锥体也会跟随回溯**

### EnemyVisionVisualizer3D
- SecurityCameraVisualizer 完全模仿了 EnemyVisionVisualizer3D
- 两者使用相同的3D视锥体生成算法
- 可以混用，视觉风格完全一致

## 技术细节

### 3D 视锥体生成算法
1. **顶点布局**：中心点 + 射线网格（水平×垂直）
2. **三角形拓扑**：
   - 从中心点到第一圈的扇形三角形
   - 射线之间的四边形（分割成2个三角形）
   - 左右两侧的封闭三角形
3. **遮挡检测**：每个顶点独立进行Raycast
4. **性能优化**：使用List<int>动态构建三角形索引

### 材质系统
- **URP优先**：自动检测并使用URP/Unlit着色器
- **降级方案**：如果URP不可用，回退到标准透明着色器
- **渲染队列**：透明物体队列（RenderQueue.Transparent）
- **混合模式**：标准Alpha混合（SrcAlpha, OneMinusSrcAlpha）

### 更新策略
- **频率控制**：可配置的更新频率（默认30fps）
- **距离剔除**：超出Max Render Distance自动隐藏
- **组件解耦**：可独立禁用线框或体积填充

### 死亡摄像机集成算法
1. **检测击杀者类型**：
   ```csharp
   var cameraComp = killerEnemy.GetComponent<SecurityCamera>();
   var enemyComp = killerEnemy.GetComponent<Enemy>();
   ```
2. **选择摄像机位置和目标**：
   - **SecurityCamera**（第一人称摄像头视角）：
     - Follow（位置） = `lookAtTarget` 或 `cameraHead`
     - LookAt（目标） = **玩家Transform**
     - **效果**：从摄像头位置看向玩家（模拟摄像头看到玩家的画面）
   - **Enemy**（跟随敌人视角）：
     - Follow（位置） = 敌人根节点
     - LookAt（目标） = `headTransform` 或子节点
     - **效果**：跟随敌人，看向敌人头部
3. **设置Cinemachine**：
   ```csharp
   deathCam.Follow = cameraFollow;  // SecurityCamera: lookAtTarget, Enemy: 敌人根节点
   deathCam.LookAt = cameraLookAt;  // SecurityCamera: 玩家, Enemy: headTransform
   ```
4. **摄像机过渡**：混合1.5秒 + 停留2秒 → 显示死亡面板

## 许可和归属
- 基于Enemy系统设计
- 3D可视化完全模仿EnemyVisionVisualizer3D
- 作者：苏生潇
- 日期：2024

---

**祝使用愉快！如有问题请查看代码注释或联系开发者。**
