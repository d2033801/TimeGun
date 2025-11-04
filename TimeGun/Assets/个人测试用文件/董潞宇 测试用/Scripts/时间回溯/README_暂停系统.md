# TimeRewind 暂停系统使用指南

## 📋 概述

本系统为所有 `AbstractTimeRewindObject` 派生类提供了**独立的暂停功能**，与回溯系统完全解耦。

## 🎯 核心特性

### ✅ 完全解耦
- 暂停和回溯是两个独立的系统
- 暂停不会消耗历史帧数据
- 暂停时既不录制也不回放

### ✅ 自动适配
- **刚体物体** (`AbstractTimeRewindRigidBody` 及其子类) - 自动冻结物理行为
- **敌人AI** (`EnemyTimeRewind`) - 自动冻结导航、动画和逻辑
- **自定义物体** - 可重写 `OnStartPause`/`OnStopPause` 实现自定义冻结

### ✅ 易于使用
```csharp
// 启动暂停
rewindObj.StartPause();

// 停止暂停
rewindObj.StopPause();

// 检查暂停状态
if (rewindObj.IsPaused) { ... }
```

---

## 📦 系统架构

### 1. 基类 - `AbstractTimeRewindObject`

**新增字段**:
```csharp
private bool isPaused = false;  // 暂停状态标记
```

**新增API**:
```csharp
public virtual void StartPause()     // 启动暂停
public virtual void StopPause()      // 停止暂停
public bool IsPaused { get; }        // 获取暂停状态
```

**新增虚方法** (供子类重写):
```csharp
protected virtual void OnStartPause()  // 暂停开始时触发
protected virtual void OnStopPause()   // 暂停结束时触发
```

**核心逻辑修改**:
```csharp
protected virtual void FixedUpdate()
{
    if (isPaused) return;  // ✅ 暂停时既不录制也不回溯
    
    if (isRewinding)
        RewindFixedStep();
    else if(isRecording)
        RecordFixedStep();
}
```

---

### 2. 刚体类 - `AbstractTimeRewindRigidBody`

**暂停时的行为**:
- 保存原始 `isKinematic` 状态
- 设置 `rb.isKinematic = true` 冻结物理

**恢复时的行为**:
- 恢复原始 `isKinematic` 状态

**实现代码**:
```csharp
protected override void OnStartPause()
{
    base.OnStartPause();
    oriIsKinematic = rb.isKinematic;
    rb.isKinematic = true;  // 冻结物理
}

protected override void OnStopPause()
{
    base.OnStopPause();
    rb.isKinematic = oriIsKinematic;  // 恢复物理
}
```

---

### 3. 敌人AI类 - `EnemyTimeRewind`

**暂停时的行为** (复用回溯的冻结逻辑):
1. **NavMeshAgent** - 停止寻路、禁用自动更新
2. **Animator** - 设置 `speed = 0` 冻结动画
3. **Enemy脚本** - 设置 `enabled = false` 停止Update

**恢复时的行为**:
- 恢复到暂停前的原始状态 (与回溯结束时恢复到快照状态不同)

**核心实现**:
```csharp
protected override void OnStartPause()
{
    base.OnStartPause();
    FreezeAllComponents();  // 复用冻结逻辑
}

protected override void OnStopPause()
{
    base.OnStopPause();
    UnfreezeAllComponents(false);  // false = 恢复到原始状态
}

// 回溯结束时调用
protected override void OnStopRewind()
{
    base.OnStopRewind();
    UnfreezeAllComponents(true);  // true = 恢复到快照状态
}
```

---

## 🚀 使用示例

### 示例1: 榴弹触发暂停

```csharp
// RewindRifleGrenade.cs
private void TryTriggerPause(Collider targetCollider, float duration)
{
    var rewindObj = targetCollider.GetComponentInParent<AbstractTimeRewindObject>();
    if (rewindObj == null) return;

    // ✅ 使用独立的暂停API
    rewindObj.StartPause();
    
    // 在目标对象上启动协程，避免榴弹销毁导致协程中断
    rewindObj.StartCoroutine(StopPauseAfterDelay(rewindObj, duration));
}

private IEnumerator StopPauseAfterDelay(AbstractTimeRewindObject rewindObj, float delay)
{
    yield return new WaitForSeconds(delay);
    
    if (rewindObj != null && rewindObj.IsPaused)
    {
        rewindObj.StopPause();
    }
}
```

### 示例2: 自定义暂停行为

```csharp
public class MyCustomRewindObject : AbstractTimeRewindObject
{
    private ParticleSystem particles;
    private AudioSource audioSource;
    
    protected override void OnStartPause()
    {
        base.OnStartPause();
        
        // 自定义暂停逻辑
        if (particles != null)
            particles.Pause();
        
        if (audioSource != null)
            audioSource.Pause();
    }
    
    protected override void OnStopPause()
    {
        base.OnStopPause();
        
        // 自定义恢复逻辑
        if (particles != null)
            particles.Play();
        
        if (audioSource != null)
            audioSource.UnPause();
    }
}
```

### 示例3: 区域暂停技能

```csharp
public class TimeStopSkill : MonoBehaviour
{
    public float radius = 10f;
    public float duration = 5f;
    
    public void ActivateTimeStop()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        
        foreach (var c in hits)
        {
            var rewindObj = c.GetComponentInParent<AbstractTimeRewindObject>();
            if (rewindObj != null)
            {
                rewindObj.StartPause();
                StartCoroutine(AutoResume(rewindObj, duration));
            }
        }
    }
    
    private IEnumerator AutoResume(AbstractTimeRewindObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) obj.StopPause();
    }
}
```

---

## 🔄 回溯 vs 暂停 对比

| 特性 | 回溯 (Rewind) | 暂停 (Pause) |
|------|--------------|-------------|
| **用途** | 让物体回到历史状态 | 冻结物体的当前状态 |
| **历史帧** | 消耗历史帧数据 | 不消耗历史帧 |
| **录制** | 停止录制 | 停止录制 |
| **物理** | 禁用 (回放历史位置) | 禁用 (保持当前位置) |
| **恢复** | 恢复到快照状态 | 恢复到暂停前状态 |
| **API** | `StartRewind()` / `StopRewind()` | `StartPause()` / `StopPause()` |

---

## ⚙️ 技术细节

### 暂停期间的行为

1. **FixedUpdate** - 完全跳过，既不录制也不回放
2. **物理系统** - 通过 `isKinematic = true` 冻结
3. **AI导航** - 通过 `isStopped = true` 和禁用更新冻结
4. **动画** - 通过 `animator.speed = 0` 冻结
5. **脚本逻辑** - 通过 `enabled = false` 冻结

### 状态优先级

```
isPaused > isRewinding > isRecording
```

如果同时处于多个状态：
- `isPaused = true` → 完全冻结，其他状态被忽略
- `isRewinding = true` → 回放历史，停止录制
- `isRecording = true` → 正常录制

### 协程生命周期

**❌ 错误做法**:
```csharp
// 榴弹上启动协程，榴弹销毁后协程中断
StartCoroutine(StopPauseAfterDelay(rewindObj, duration));
Destroy(gameObject);  // ❌ 协程宿主被销毁
```

**✅ 正确做法**:
```csharp
// 在目标对象上启动协程，目标存活则协程继续
rewindObj.StartCoroutine(StopPauseAfterDelay(rewindObj, duration));
Destroy(gameObject);  // ✅ 榴弹销毁不影响目标的协程
```

---

## 📝 升级指南

### 从旧版本迁移

**旧代码** (使用回溯速度0的hack):
```csharp
rewindObj.StartRewind(0f);  // ❌ 耦合度高，语义不清
yield return new WaitForSeconds(duration);
rewindObj.StopRewind();
```

**新代码** (使用独立的暂停API):
```csharp
rewindObj.StartPause();  // ✅ 解耦，语义清晰
yield return new WaitForSeconds(duration);
rewindObj.StopPause();
```

---

## 🐛 常见问题

### Q: 暂停的物体会消耗历史帧吗？
**A**: 不会。暂停期间 `FixedUpdate` 直接返回，不录制也不回放。

### Q: 可以同时暂停和回溯吗？
**A**: 不可以。暂停优先级更高，如果 `isPaused = true`，回溯逻辑不会执行。

### Q: 暂停会影响其他物体吗？
**A**: 不会。每个 `AbstractTimeRewindObject` 实例独立管理自己的暂停状态。

### Q: 如何实现"暂停所有敌人"？
**A**: 
```csharp
var allEnemies = FindObjectsOfType<EnemyTimeRewind>();
foreach (var enemy in allEnemies)
{
    enemy.StartPause();
}
```

---

## 📌 最佳实践

1. **协程宿主选择** - 在目标对象上启动自动恢复的协程，而不是触发源
2. **状态检查** - 调用 `StopPause()` 前检查 `IsPaused`，避免重复调用
3. **自定义暂停** - 重写 `OnStartPause`/`OnStopPause` 时记得调用 `base` 方法
4. **死亡处理** - `EnemyTimeRewind` 已自动处理：死亡敌人暂停后不会恢复 `enabled`

---

## 🎓 总结

本暂停系统通过以下方式实现了完全解耦：

1. ✅ **独立的状态标记** - `isPaused` 与 `isRewinding` 分离
2. ✅ **独立的API** - `StartPause()` / `StopPause()` vs `StartRewind()` / `StopRewind()`
3. ✅ **独立的回调** - `OnStartPause()` / `OnStopPause()` vs `OnStartRewind()` / `OnStopRewind()`
4. ✅ **复用冻结逻辑** - `EnemyTimeRewind` 通过参数区分恢复行为，避免代码重复

现在所有继承自 `AbstractTimeRewindObject` 的物体都支持暂停功能！🎉
