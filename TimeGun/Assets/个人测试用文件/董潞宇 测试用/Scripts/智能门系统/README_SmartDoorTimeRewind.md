# SmartDoorTimeRewind - 智能门时间回溯系统

## 📋 概述

`SmartDoorTimeRewind` 是专门为 `SmartDoorController` 设计的时间回溯组件，用于解决门在回溯时状态不同步的问题。

## ❌ 问题分析

### 原始问题
使用普通的时间回溯系统时，门的 `IsOpen` 属性和内部状态（如 `_autoCloseTimer`）没有被正确记录，导致回溯后出现以下问题：
- 门的动画回溯了，但逻辑状态没有同步
- 门的自动关门计时器状态丢失
- 回溯后门的检测逻辑可能产生错误的开关门行为

### 解决方案对比

#### 方案1：新增专门的回溯脚本（已实现）✅
- **优点**：
  - 不影响现有的门控制逻辑
  - 更灵活，可以单独控制门的回溯行为
  - 可以记录 `_isDoorOpen` 等自定义状态
  - 符合单一职责原则
  
- **缺点**：
  - 需要新增一个脚本

#### 方案2：从Animator获取状态（未实现）
- **优点**：
  - 不需要额外记录状态
  - 实现相对简单
  
- **缺点**：
  - 如果使用程序化旋转模式（ProceduralRotation），动画机中没有参数
  - 需要修改门的判断逻辑，可能影响现有功能
  - 紧耦合Animator，不够灵活

## 🚀 使用方法

### 1. 添加组件
在带有 `SmartDoorController` 的 GameObject 上添加 `SmartDoorTimeRewind` 组件：

```csharp
// 方法1：在 Inspector 中手动添加
// 1. 选择带有 SmartDoorController 的门对象
// 2. Add Component → Time Rewind → Smart Door Time Rewind

// 方法2：代码添加（自动检测是否缺少 SmartDoorController）
gameObject.AddComponent<SmartDoorTimeRewind>();
```

### 2. 配置参数
在 Inspector 中配置回溯参数（继承自 `AbstractTimeRewindObject`）：

```
【Config 配置】
- Record Seconds Config: 录制时长（秒，0为默认20秒）
- Record FPS Config: 录制帧率（0为默认值）
- Rewind Speed Config: 回溯速度倍率（推荐 2-5）

【Effects 特效（可选）】
- Rewind Effect Prefab: 回溯时播放的特效
- Pause Effect Prefab: 暂停时播放的特效
- Effect Spawn Point: 特效生成点（留空则使用门自身位置）

【Animator 动画】
- Target Animator: 门的 Animator 组件（自动获取）
```

### 3. 调用回溯API
通过 `GlobalTimeRewindManager` 或直接调用组件方法：

```csharp
// 获取组件
var doorRewind = door.GetComponent<SmartDoorTimeRewind>();

// 1. 开始渐进式回溯（持续回溯直到调用 StopRewind）
doorRewind.StartRewind();          // 使用默认速度
doorRewind.StartRewind(3f);        // 使用自定义速度（3倍速）

// 2. 停止回溯
doorRewind.StopRewind();

// 3. 限时回溯（回溯指定时长后自动停止）
doorRewind.StartRewindByTime(2f);  // 回溯2秒后自动停止
doorRewind.StartRewindByTime(2f, 5f);  // 以5倍速回溯2秒

// 4. 瞬间回溯（立即回溯到N秒前的状态）
doorRewind.RewindBySeconds(3f);    // 瞬间回溯到3秒前

// 5. 暂停/恢复（不消耗历史帧）
doorRewind.StartPause();           // 暂停门的所有行为
doorRewind.StopPause();            // 恢复门的正常行为
```

## 🔧 技术细节

### 录制内容
组件会同时录制：
1. **Animator 状态**（继承自 `AnimatorTimeRewind`）
   - 所有层的动画状态哈希
   - 所有层的归一化时间
   - 所有参数值（Float、Int、Bool，不包括Trigger）

2. **门的自定义状态**
   - `IsDoorOpen`：门是否开启
   - `_autoCloseTimer`：自动关门计时器

### 冻结机制
回溯/暂停期间会冻结：
1. **Animator**：设置 `speed = 0`，防止动画继续播放
2. **SmartDoorController**：设置 `enabled = false`，停止门的检测和自动开关逻辑

使用引用计数管理冻结状态，支持暂停和回溯并发运行。

### 反射访问私有字段
由于 `SmartDoorController` 的 `_autoCloseTimer` 是私有字段，组件使用反射访问：

```csharp
// 获取私有字段
var field = typeof(SmartDoorController).GetField(
    "_autoCloseTimer",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
);

float value = (float)field.GetValue(DoorController);
field.SetValue(DoorController, newValue);
```

**性能优化建议**：如果担心反射性能，可以在 `SmartDoorController` 中添加公开属性：

```csharp
// 在 SmartDoorController 中添加
public float AutoCloseTimer
{
    get => _autoCloseTimer;
    set => _autoCloseTimer = value;
}
```

## 📊 性能影响

### 内存占用
假设配置：
- 录制时长：20秒
- 录制帧率：30 FPS
- 总帧数：600帧

每帧存储：
- Animator 状态：约 100-200 字节（取决于动画层数和参数数量）
- 门状态：16 字节（bool + float）
- **总计**：约 116-216 字节/帧

总内存：约 **70KB - 130KB** （非常轻量）

### CPU 开销
- **录制**：每 0.033 秒（30FPS）记录一次，开销极小
- **回放**：每帧应用一次快照，开销与录制相当
- **反射访问**：仅在记录/回放时调用，频率低，影响可忽略

## 🎯 最佳实践

### 1. 全局管理（推荐）
配合 `GlobalTimeRewindManager` 使用，统一管理所有可回溯对象：

```csharp
// 在 GlobalTimeRewindManager 中注册
GlobalTimeRewindManager.Instance.StartRewind(2f);  // 回溯所有注册对象2秒
```

### 2. 单独控制
如果只需要回溯特定的门：

```csharp
// 仅回溯指定的门
door.GetComponent<SmartDoorTimeRewind>().StartRewind();
```

### 3. 调试可视化
启用 Scene 视图的 Gizmos，可以看到：
- 门的回溯状态（回溯中/正常）
- 历史帧数
- 门的当前状态（开启/关闭）

## ⚠️ 注意事项

1. **必须配置 Animator**：门必须使用 Animation 模式，否则无法回溯动画
2. **程序化旋转模式**：如果使用 ProceduralRotation 模式，建议改用 Animation 模式以获得更好的回溯效果
3. **反射性能**：如果门数量很多（>50），建议在 `SmartDoorController` 中添加公开属性以避免反射
4. **回溯速度**：建议设置 2-5 倍速，过高可能导致视觉效果不流畅

## 🐛 常见问题

### Q1: 回溯后门的状态不对？
- **检查**：确保 `SmartDoorTimeRewind` 正确添加到门对象上
- **检查**：确保门使用的是 Animation 模式（而非 ProceduralRotation）
- **解决**：查看 Console 日志，确认录制和回放正常进行

### Q2: 回溯时门还在自动开关？
- **原因**：冻结逻辑未生效
- **解决**：检查 `SmartDoorController` 是否在回溯时被正确禁用（enabled = false）

### Q3: 内存占用过高？
- **原因**：录制时长或帧率设置过高
- **解决**：降低 `recordSecondsConfig` 或 `recordFPSConfig`（例如从60FPS降到30FPS）

## 📝 更新日志

### v1.0 (2024)
- ✅ 初始版本发布
- ✅ 支持 Animator 动画回溯
- ✅ 支持门的自定义状态回溯（IsOpen、AutoCloseTimer）
- ✅ 引用计数冻结机制（支持暂停和回溯并发）
- ✅ Scene 视图调试可视化

## 📚 相关文档

- [AbstractTimeRewindObject 基类文档](./README_AbstractTimeRewindObject.md)
- [AnimatorTimeRewind 文档](./README_AnimatorTimeRewind.md)
- [SmartDoorController 文档](./SmartDoorController.cs)

---

**作者备注**：这个方案优于"从Animator获取状态"的原因是它保持了门控制器的独立性，不需要修改现有逻辑，且支持所有控制模式（Animation 和 ProceduralRotation）。如果未来门增加了更多状态（如开门音效、粒子效果等），只需在 `DoorStateSnapshot` 中添加对应字段即可。
