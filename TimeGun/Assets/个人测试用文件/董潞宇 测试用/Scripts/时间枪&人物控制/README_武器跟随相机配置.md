# 武器跟随相机俯仰配置指南

## 🎯 问题描述

当武器挂在手上并使用 IK 时，会遇到以下问题：
- ✅ IK 让手自然握住枪
- ❌ 但枪无法独立跟随相机俯仰（因为枪绑在手上）
- ❌ 结果：无法瞄准不同高度的目标

---

## 💡 解决方案：WeaponAimController

**WeaponAimController** 组件让武器在保持 IK 握持姿势的同时，独立跟随相机旋转。

### 工作原理

```
相机俯仰
    ↓
WeaponAimController 旋转武器
    ↓
IK 目标跟随武器旋转（因为是武器子物体）
    ↓
手部 IK 跟随目标移动
    ↓
✅ 结果：武器跟随相机，手部自然握持
```

---

## 🔧 配置步骤

### 第 1 步：添加组件

在**武器根节点**上添加 `WeaponAimController` 组件：

```
RightHand (手部骨骼)
└── Weapon (武器根节点) ← 在这里添加 WeaponAimController
    ├── Model (枪械模型)
    ├── MuzzlePoint
    ├── RightHandIK
    └── LeftHandIK
```

**Unity 操作**：
1. 选择武器根节点
2. Add Component → TimeGun → WeaponAimController

---

### 第 2 步：配置参数

在 WeaponAimController Inspector 中设置：

#### References
| 参数 | 说明 | 推荐值 |
|------|------|--------|
| **Camera Root** | 相机根节点引用 | 拖入 PlayerController 的 cameraRoot（或留空自动查找）|

#### Aim Settings
| 参数 | 说明 | 推荐值 |
|------|------|--------|
| **Enable Aim** | 是否启用跟随 | ✅ 勾选 |
| **Aim Mode** | 跟随模式 | `Pitch Only`（仅跟随俯仰）|
| **Rotation Smoothing** | 平滑速度 | `10`（0 = 即时跟随）|
| **Pitch Offset** | 俯仰角偏移 | `0`（根据需要调整）|
| **Yaw Offset** | 偏航角偏移 | `0` |

#### Constraints（可选）
| 参数 | 说明 | 推荐值 |
|------|------|--------|
| **Constrain Pitch** | 是否限制俯仰角 | ❌ 取消勾选（除非需要限制）|
| **Pitch Clamp** | 俯仰角限制范围 | `(-45, 45)` |

#### Debug
| 参数 | 说明 | 推荐值 |
|------|------|--------|
| **Show Debug** | 显示调试信息 | ❌ 取消勾选（正式版）|

---

### 第 3 步：选择跟随模式

**Aim Mode** 有 3 种模式：

#### 1. Pitch Only（推荐）
- 仅跟随相机的俯仰角（上下）
- 偏航（左右）跟随角色身体
- **适用场景**：大多数 TPS/FPS 游戏

#### 2. Full Rotation
- 完全跟随相机方向（俯仰 + 偏航）
- 武器始终指向相机朝向
- **适用场景**：需要精确瞄准的狙击枪

#### 3. Custom
- 自定义模式（可在代码中扩展）
- **适用场景**：特殊需求

---

## 🎨 层级结构示例

### 完整层级（推荐）

```
Player (PlayerController + WeaponManager)
├── CharacterController
└── Model (Animator + WeaponIKHandler)
    └── Armature
        └── RightHand (手部骨骼)
            └── Weapon ← WeaponAimController 在这里
                ├── Model (枪械模型)
                ├── MuzzlePoint
                ├── RightHandIK ← IK 目标
                └── LeftHandIK ← IK 目标
```

**关键点**：
- ✅ WeaponAimController 在武器根节点上
- ✅ IK 目标是武器的子物体（会跟随武器旋转）
- ✅ 武器挂在手部骨骼上

---

## 🔄 工作流程

### 运行时流程

```
1. 玩家移动鼠标
    ↓
2. PlayerController 更新 cameraRoot 旋转
    ↓
3. WeaponAimController 读取 cameraRoot 角度
    ↓
4. 武器旋转到目标角度（平滑插值）
    ↓
5. IK 目标作为武器子物体跟随旋转
    ↓
6. WeaponIKHandler 更新手部 IK
    ↓
7. 手部移动到 IK 目标位置
    ↓
✅ 结果：武器跟随相机，手部自然握持
```

---

## 🎯 测试清单

### 基础测试
- [ ] 进入 Play Mode
- [ ] 上下移动鼠标
- [ ] 武器枪口跟随相机俯仰
- [ ] 手部仍然自然握枪（IK 生效）
- [ ] 左右移动鼠标
- [ ] 武器偏航跟随角色身体（Pitch Only 模式）

### 高级测试
- [ ] 瞄准高处目标（如窗户）
- [ ] 瞄准低处目标（如地面）
- [ ] 快速移动鼠标，武器平滑跟随
- [ ] 射击时子弹从枪口正确发出

---

## 🐛 常见问题

### 问题 1：武器不跟随相机
**可能原因**：
1. Camera Root 未设置
2. Enable Aim 未勾选
3. WeaponAimController 不在武器根节点上

**解决方法**：
- 检查 Inspector 中的 Camera Root 引用
- 确认 Enable Aim 已勾选
- 确认组件层级关系

---

### 问题 2：武器旋转太快/太慢
**可能原因**：
Rotation Smoothing 设置不合适

**解决方法**：
- 调整 Rotation Smoothing 值：
  - `0` = 即时跟随（无平滑）
  - `5-10` = 较快跟随
  - `15-20` = 平滑跟随
  - `30+` = 缓慢跟随

---

### 问题 3：武器角度有偏移
**可能原因**：
初始旋转或偏移设置不正确

**解决方法**：
1. 调整 `Pitch Offset`：
   - 正值：枪口向上偏移
   - 负值：枪口向下偏移
2. 调整 `Yaw Offset`：
   - 正值：枪口向右偏移
   - 负值：枪口向左偏移

---

### 问题 4：手部 IK 与武器旋转冲突
**可能原因**：
IK 目标不是武器的子物体

**解决方法**：
确保 IK 目标（RightHandIK / LeftHandIK）是武器的**子物体**，而不是独立物体。

---

### 问题 5：武器旋转范围受限
**可能原因**：
Constrain Pitch 已勾选

**解决方法**：
- 取消勾选 Constrain Pitch
- 或调整 Pitch Clamp 范围

---

## 🎨 调试技巧

### 1. 使用 Gizmos
勾选 `Show Debug`，在 Scene 视图中会显示：
- 🔴 红色线 = 武器朝向
- 🟡 黄色线 = 相机朝向

### 2. 查看运行时信息
Inspector 底部会显示：
- ✅ CameraRoot 状态
- 当前武器旋转角度（运行时）

### 3. 控制台日志
勾选 `Show Debug` 后，每帧输出：
```
[WeaponAimController] Weapon Rotation: Pitch=-15.0°, Yaw=45.0°
```

---

## 🔧 高级配置

### 1. 动态启用/禁用跟随

```csharp
// 获取组件
var aimController = weapon.GetComponent<WeaponAimController>();

// 启用跟随
aimController.EnableAim();

// 禁用跟随
aimController.DisableAim();
```

### 2. 切换跟随模式

```csharp
// 切换到完全跟随模式（狙击枪）
aimController.SetAimMode(WeaponAimController.AimMode.FullRotation);

// 切换回仅俯仰模式（普通枪械）
aimController.SetAimMode(WeaponAimController.AimMode.PitchOnly);
```

### 3. 重置到初始旋转

```csharp
// 重置武器到初始状态
aimController.ResetToInitialRotation();
```

---

## 📊 模式对比

| 特性 | Pitch Only | Full Rotation |
|------|-----------|---------------|
| 跟随俯仰（上下）| ✅ | ✅ |
| 跟随偏航（左右）| ❌ 跟随角色身体 | ✅ |
| 适用武器 | 步枪、手枪、冲锋枪 | 狙击枪、精确射击 |
| 视觉效果 | 自然（武器随身体）| 精确（武器随相机）|
| 推荐 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |

---

## 🎯 最佳实践

### 1. 优先使用 Pitch Only
大多数情况下，仅跟随俯仰角看起来更自然。

### 2. 调整平滑值
- TPS 游戏：`10-15`（平滑跟随）
- FPS 游戏：`5-8`（快速响应）
- 狙击枪：`15-20`（缓慢精确）

### 3. 配合 IK 使用
- WeaponAimController：控制武器旋转
- WeaponIKHandler：控制手部握持
- 两者配合，实现完美效果

### 4. 不同武器使用不同设置
为每把武器预制体设置不同的：
- Rotation Smoothing（狙击枪更慢）
- Aim Mode（狙击枪使用 Full Rotation）
- Pitch Offset（不同枪械高度）

---

## ✅ 配置完成检查

完成以下所有项后，系统正常工作：

### 组件配置
- [ ] WeaponAimController 已添加到武器根节点
- [ ] Camera Root 引用已设置
- [ ] Enable Aim 已勾选
- [ ] Aim Mode 设置为 Pitch Only

### 层级结构
- [ ] 武器挂在手部骨骼上
- [ ] IK 目标是武器的子物体
- [ ] WeaponIKHandler 在 Animator GameObject 上

### 功能测试
- [ ] 武器跟随相机俯仰
- [ ] 手部自然握枪（IK 生效）
- [ ] 射击时子弹方向正确
- [ ] 移动平滑自然

---

## 🎉 完成！

现在您的武器系统支持：
- ✅ IK 自然握持
- ✅ 跟随相机俯仰
- ✅ 平滑过渡
- ✅ 灵活配置

**享受您的完美武器系统吧！** 🎮
