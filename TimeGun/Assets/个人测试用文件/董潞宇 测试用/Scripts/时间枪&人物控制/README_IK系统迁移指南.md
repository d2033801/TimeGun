# IK 系统迁移指南

## 🎯 迁移目的

将 IK 功能从 `PlayerController` 迁移到独立的 `WeaponIKHandler` 组件，解决以下问题：
1. ✅ Animator 和 PlayerController 不在同一个 GameObject 上
2. ✅ `OnAnimatorIK()` 无法被正确调用
3. ✅ 职责分离，代码更清晰

---

## 📂 层级结构示例

### 迁移前（❌ 错误）
```
Player (PlayerController + WeaponManager)
├── CharacterController
└── Model (Animator 在这里)
    └── Armature
        └── ... (骨骼)
```
**问题**：PlayerController 的 `OnAnimatorIK()` 不会被调用，因为 Animator 不在同一个 GameObject 上。

### 迁移后（✅ 正确）
```
Player (PlayerController + WeaponManager)
├── CharacterController
└── Model (Animator + WeaponIKHandler 都在这里)
    └── Armature
        └── RightHand
            └── Weapon
                ├── RightHandIK
                ├── LeftHandIK
                └── MuzzlePoint
```
**优点**：`WeaponIKHandler` 和 Animator 在同一个 GameObject 上，`OnAnimatorIK()` 可以正常调用。

---

## 🔧 迁移步骤

### 第 1 步：备份场景
在开始前保存当前场景！

### 第 2 步：添加 WeaponIKHandler 组件
1. 选择带有 **Animator** 的 GameObject（通常是 Player 的子物体 Model）
2. Add Component → TimeGun → WeaponIKHandler
3. 确认 Animator 组件在同一个 GameObject 上

### 第 3 步：配置 WeaponIKHandler
在 Inspector 中设置：

| 参数 | 说明 | 推荐值 |
|------|------|--------|
| **Weapon Manager** | 拖入父物体的 WeaponManager 组件（如果为空会自动查找） | 留空自动查找 |
| **Enable IK** | 是否启用 IK | ✅ 勾选 |
| **IK Layer** | IK 层索引 | 0（默认 Base Layer）|
| **Right Hand IK Weight** | 右手 IK 权重 | 1.0 |
| **Left Hand IK Weight** | 左手 IK 权重 | 1.0 |
| **Elbow Hint Weight** | 肘部辅助权重 | 1.0 |
| **Show Gizmos** | 显示调试 Gizmos | ✅ 勾选（调试时）|
| **Show Debug Logs** | 显示调试日志 | ❌ 取消勾选（正式版）|

### 第 4 步：验证 Animator 设置
1. 打开 Animator 窗口（Window → Animation → Animator）
2. 选择 Base Layer（或包含动画的层）
3. 点击层右侧的齿轮图标 ⚙️
4. ✅ 确保 **IK Pass** 已勾选

### 第 5 步：验证模型设置
1. 选择角色模型的 `.fbx` 文件
2. Inspector → Rig → Animation Type: **Humanoid**
3. 点击 **Configure**，确认骨骼映射正确
4. 点击 **Apply**

### 第 6 步：测试 IK 系统
1. 进入 Play Mode
2. 查看 Scene 视图，应该看到以下 Gizmos：
   - 🔵 蓝色球体 = 右手 IK 目标
   - 🟢 绿色球体 = 左手 IK 目标
   - 🔷 青色球体 = 右肘辅助点
   - 🟣 紫色球体 = 左肘辅助点
   - 🔴 红色线框球 = Animator 根节点
   - 🟡 黄色连线 = IK 目标到实际手部的距离
3. 观察角色手部是否自然握枪
4. 上下移动视角，确认武器跟随

---

## ✅ 验证清单

### 运行前检查
- [ ] WeaponIKHandler 和 Animator 在同一个 GameObject 上
- [ ] Animator 是 Humanoid 类型
- [ ] Animator Controller 的 IK Pass 已勾选
- [ ] 武器预制体的 IK 目标已正确设置

### 运行时检查
- [ ] 控制台无错误日志
- [ ] Scene 视图显示 Gizmos（蓝色/绿色球体）
- [ ] 角色双手自然握枪
- [ ] 移动视角时武器跟随
- [ ] 射击时子弹从枪口发出

### Inspector 检查
打开 WeaponIKHandler 的 Inspector，底部会显示：
- ✅ `Animator 配置正确（Humanoid）`
- ✅ `找到 WeaponManager: Player`
- ℹ️ `提醒：请确保 IK Pass 已勾选`

---

## 🐛 常见问题排查

### 问题 1：IK 不工作，手部不动
**可能原因**：
1. Animator Controller 的 IK Pass 未勾选
2. WeaponIKHandler 和 Animator 不在同一个 GameObject 上
3. 模型不是 Humanoid 类型

**解决方法**：
- 检查 Animator 窗口中的 IK Pass
- 确认组件层级关系
- 检查模型 Rig 设置

### 问题 2：手部位置错误（偏移很大）
**可能原因**：
1. IK 目标位置不正确
2. Model 子物体的 Transform 不是 (0,0,0)
3. 模型 Scale 不是 (1,1,1)

**解决方法**：
- 在 Play Mode 下调整 IK 目标位置
- 检查 Model 的 Transform 值
- 检查模型导入设置的 Scale Factor

### 问题 3：控制台报错 "未找到 WeaponManager"
**可能原因**：
WeaponManager 不在父物体上

**解决方法**：
- 手动在 Inspector 中拖入 WeaponManager 引用
- 或将 WeaponManager 移到父物体上

### 问题 4：Gizmos 不显示
**可能原因**：
1. Show Gizmos 未勾选
2. 未进入 Play Mode
3. Scene 视图的 Gizmos 按钮未开启

**解决方法**：
- 勾选 WeaponIKHandler 的 Show Gizmos
- 进入 Play Mode
- 点击 Scene 视图右上角的 Gizmos 按钮

---

## 🎨 微调 IK 目标位置

### 实时调整技巧
1. **进入 Play Mode**
2. 在 Scene 视图中选择 IK 目标（RightHandIK / LeftHandIK）
3. 使用移动/旋转工具调整位置
4. 观察角色手部姿势变化
5. **记录满意的 Transform 值**
6. 退出 Play Mode
7. **手动输入 Transform 值到预制体**

### 调整建议
- **右手太低**：增加 Y 值
- **手臂弯曲不自然**：调整 Z 轴位置（前后移动）
- **手掌方向错误**：调整旋转角度
- **肘部不自然**：添加 ElbowHint 并调整位置

---

## 🚀 高级功能

### 动态调整 IK 权重
```csharp
// 在其他脚本中获取 WeaponIKHandler
var ikHandler = GetComponentInChildren<WeaponIKHandler>();

// 动态调整权重
ikHandler.SetIKWeights(
    rightHand: 1.0f,  // 右手权重
    leftHand: 0.5f,   // 左手权重（部分启用）
    elbowHint: 0.8f   // 肘部权重
);

// 启用/禁用 IK
ikHandler.EnableIK();
ikHandler.DisableIK();
```

### 不同武器使用不同 IK 设置
为每把武器创建独立的预制体，设置不同的 IK 目标位置：
- 手枪：右手靠近扳机，左手权重设为 0
- 步枪：双手握持
- 狙击枪：左手更靠前

### 根据状态调整 IK
```csharp
// 在 WeaponIKHandler 中添加
private void OnAnimatorIK(int layerIndex)
{
    // ...existing code...

    // 根据瞄准状态调整权重
    if (_playerController != null && _playerController.IsAiming)
    {
        rightHandIkWeight = 1.0f; // 瞄准时完全启用
    }
    else
    {
        rightHandIkWeight = 0.7f; // 非瞄准时减少影响
    }
}
```

---

## 📊 性能优化

已实施的优化：
- ✅ 调试日志限制频率（每 100 帧 1 次）
- ✅ Gizmos 可选开关
- ✅ 仅在需要时执行 IK 计算
- ✅ 缓存组件引用

---

## 🔄 回滚方案

如果迁移后出现问题，可以临时回滚：

1. **禁用 WeaponIKHandler**
   - 取消勾选组件上的 Enable IK

2. **恢复手动旋转**（不推荐）
   - 在 PlayerController.LateUpdate() 中取消注释：
   ```csharp
   weaponManager.UpdateWeaponPitch(_cameraPitch);
   ```

但请注意：**手动旋转和 IK 不能同时使用！**

---

## 📚 参考资料

- [Unity IK 官方文档](https://docs.unity3d.com/Manual/InverseKinematics.html)
- [Humanoid Avatars 配置](https://docs.unity3d.com/Manual/ConfiguringtheAvatar.html)
- 项目内文档：
  - `README_IK系统使用指南.md` - 完整配置指南
  - `README_IK快速开始.md` - 5 分钟快速配置

---

## ✅ 迁移完成检查

完成以下所有项后，迁移成功：

- [ ] WeaponIKHandler 已添加到带 Animator 的 GameObject
- [ ] WeaponManager 引用已设置（或自动找到）
- [ ] Animator 是 Humanoid 类型
- [ ] IK Pass 已勾选
- [ ] 运行游戏，角色双手自然握枪
- [ ] 射击系统正常工作
- [ ] 控制台无错误日志

---

**完成迁移后，IK 系统会自动工作，无需额外代码！** 🎉

如有问题，请查看控制台的 `[WeaponIKHandler]` 日志。
