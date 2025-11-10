# AudioManager Time.timeScale 修复说明

## 🐛 问题根源

### 现象
- ✅ 正常播放时音量变化正常
- ❌ 暂停菜单时音量不降低（Time.timeScale = 0）
- ❌ Victory 画面时音乐不切换或音量不变（Time.timeScale = 0）

### 根本原因

**DOTween 的默认行为会受到 Time.timeScale 影响！**

```csharp
// 暂停或胜利时
Time.timeScale = 0;  // 游戏时间冻结

// 此时所有默认的 DOTween Tweens 都会停止执行！
_audioSource.DOFade(targetVolume, duration);  // ❌ 不会执行
DOVirtual.Float(from, to, duration, ...);     // ❌ 不会执行
```

---

## ✅ 解决方案

### 使用 Unscaled Time

DOTween 提供了 `.SetUpdate(true)` 方法，让 Tween 使用 **真实时间**而不是游戏时间：

```csharp
// ❌ 旧代码（受 timeScale 影响）
_volumeTween = _audioSource.DOFade(target, duration)
    .SetEase(Ease.InOutQuad);

// ✅ 新代码（不受 timeScale 影响）
_volumeTween = _audioSource.DOFade(target, duration)
    .SetEase(Ease.InOutQuad)
    .SetUpdate(true);  // 🔑 关键！使用 unscaled time
```

---

## 🔧 修复详情

### 1. 音量 Tween 修复

**位置**：`SetVolume()` 方法

```csharp
private void SetVolume(float target, float duration = -1f)
{
    if (duration < 0f) duration = volumeFadeTime;

    _volumeTween?.Kill();
    
    // 修复前
    // _volumeTween = _audioSource.DOFade(target, duration)
    //     .SetEase(Ease.InOutQuad);
    
    // 修复后
    _volumeTween = _audioSource.DOFade(target, duration)
        .SetEase(Ease.InOutQuad)
        .SetUpdate(true);  // 🎯 即使 timeScale = 0 也会执行
}
```

**效果**：
- 暂停时音量正常降低
- 恢复时音量正常升高
- Victory 时音乐正常切换到最大音量

---

### 2. Mixer 效果 Tween 修复

**位置**：`ApplyRewindEffect()` 方法

```csharp
private void ApplyRewindEffect(bool enable)
{
    if (!_hasMixer) return;

    float targetCutoff = enable ? rewindLowpassCutoff : normalLowpassCutoff;
    float targetPitch = enable ? rewindPitch : normalPitch;

    // 低通滤波器
    if (audioMixer.GetFloat(bgmLowpassParameter, out float currentCutoff))
    {
        DOVirtual.Float(currentCutoff, targetCutoff, volumeFadeTime, value =>
            audioMixer.SetFloat(bgmLowpassParameter, value)
        ).SetEase(Ease.InOutQuad)
         .SetUpdate(true);  // 🎯 回溯效果不受 timeScale 影响
    }

    // 音调变换
    if (audioMixer.GetFloat(bgmPitchParameter, out float currentPitch))
    {
        DOVirtual.Float(currentPitch, targetPitch, volumeFadeTime, value =>
            audioMixer.SetFloat(bgmPitchParameter, value)
        ).SetEase(Ease.InOutQuad)
         .SetUpdate(true);  // 🎯 回溯效果不受 timeScale 影响
    }
}
```

---

### 3. 协程等待修复

**位置**：`CrossfadeToClip()` 方法

```csharp
private IEnumerator CrossfadeToClip(AudioClip newClip)
{
    float targetVolume = GetTargetVolume();

    if (_audioSource.isPlaying)
    {
        SetVolume(0f, musicCrossfadeTime);
        
        // 修复前
        // yield return new WaitForSeconds(musicCrossfadeTime);  // ❌ timeScale = 0 时永不继续
        
        // 修复后
        yield return new WaitForSecondsRealtime(musicCrossfadeTime);  // ✅ 使用真实时间
        
        _audioSource.Stop();
    }

    // 播放新音乐并淡入
    _audioSource.clip = newClip;
    _audioSource.volume = 0f;
    _audioSource.Play();
    SetVolume(targetVolume, volumeFadeTime);
}
```

---

### 4. 延迟调用修复

**位置**：`StopMusic()` 方法

```csharp
private void StopMusic()
{
    _currentMusic = MusicState.None;
    SetVolume(0f, volumeFadeTime);
    
    // 修复前
    // DOVirtual.DelayedCall(volumeFadeTime, () => _audioSource.Stop());  // ❌ 受 timeScale 影响
    
    // 修复后
    DOVirtual.DelayedCall(volumeFadeTime, () => _audioSource.Stop())
        .SetUpdate(true);  // ✅ 使用 unscaled time
}
```

---

## 📊 修复前后对比

### 场景 1：暂停菜单

| 阶段 | 修复前 | 修复后 |
|------|--------|--------|
| **打开暂停菜单** | Time.timeScale = 0 | Time.timeScale = 0 |
| **调用 EnterPauseState()** | DOFade 不执行 ❌ | DOFade 执行 ✅ |
| **结果** | 音量保持 0.7 | 音量降至 0.3 |

### 场景 2：胜利画面

| 阶段 | 修复前 | 修复后 |
|------|--------|--------|
| **触发 Victory** | Time.timeScale = 0 | Time.timeScale = 0 |
| **调用 PlayVictoryMusic()** | 协程卡在 WaitForSeconds ❌ | 协程正常执行 ✅ |
| **音乐切换** | 不切换或卡住 | 正常切换 |
| **音量变化** | DOFade 不执行 ❌ | 音量升至 1.0 ✅ |

### 场景 3：暂停中回溯（边缘情况）

| 阶段 | 修复前 | 修复后 |
|------|--------|--------|
| **暂停状态** | 音量 0.7（未降低） | 音量 0.3（正确） |
| **触发回溯** | 音量 → 0.5（但不执行） | 音量 0.3 → 0.5 ✅ |
| **回溯结束** | 音量保持不变 | 音量 0.5 → 0.3 ✅ |

---

## 🎯 核心要点

### DOTween 的两种时间模式

```csharp
// 1. Scaled Time（默认）- 受 Time.timeScale 影响
Tween tween1 = transform.DOMove(target, 1f);
// Time.timeScale = 0 时，Tween 停止

// 2. Unscaled Time - 不受 Time.timeScale 影响
Tween tween2 = transform.DOMove(target, 1f).SetUpdate(true);
// Time.timeScale = 0 时，Tween 仍然执行
```

### 何时使用 SetUpdate(true)

| 场景 | 是否需要 SetUpdate(true) | 原因 |
|------|------------------------|------|
| **游戏内动画** | ❌ 否 | 暂停时应该停止 |
| **UI 动画** | ✅ 是 | 暂停菜单仍需动画 |
| **音频淡入淡出** | ✅ 是 | 暂停时音量仍需变化 |
| **计时器倒计时** | ❌ 否 | 暂停时应该停止 |

---

## 🧪 测试验证

### 测试步骤

1. **暂停测试**
   ```
   1. 运行游戏，播放 Gameplay 音乐
   2. 按 ESC 打开暂停菜单（Time.timeScale = 0）
   3. ✅ 观察音量是否在 1 秒内降至 0.3
   4. 关闭暂停菜单
   5. ✅ 观察音量是否在 1 秒内升至 0.7
   ```

2. **Victory 测试**
   ```
   1. 触发胜利条件（Time.timeScale = 0）
   2. ✅ 观察音乐是否切换到 Victory
   3. ✅ 观察音量是否升至 1.0
   ```

3. **回溯测试**
   ```
   1. 按住回溯键（Time.timeScale 正常）
   2. ✅ 音量降至 0.5，应用滤波效果
   3. 松开回溯键
   4. ✅ 音量恢复至 0.7，效果恢复
   ```

4. **暂停中回溯测试**（边缘情况）
   ```
   1. 打开暂停菜单（音量 0.3）
   2. 按住回溯键
   3. ✅ 音量应该变为 0.5（回溯优先级更高）
   4. 松开回溯键
   5. ✅ 音量应该恢复到 0.3（暂停状态）
   ```

---

## 📝 代码检查清单

如果你要在其他地方使用 DOTween，记得检查：

### ✅ 需要 SetUpdate(true) 的情况

```csharp
// UI 动画
uiElement.DOFade(1f, 0.5f).SetUpdate(true);
uiElement.DOScale(1.2f, 0.3f).SetUpdate(true);

// 音频淡入淡出
audioSource.DOFade(0.5f, 1f).SetUpdate(true);

// 暂停菜单动画
pausePanel.DOLocalMoveY(0f, 0.5f).SetUpdate(true);

// 延迟调用（需要在暂停时执行）
DOVirtual.DelayedCall(1f, () => { ... }).SetUpdate(true);
```

### ❌ 不需要 SetUpdate(true) 的情况

```csharp
// 游戏内物体移动
enemy.DOMove(target, 2f);  // 暂停时应该停止

// 游戏内旋转
propeller.DORotate(new Vector3(0, 360, 0), 1f);

// 游戏内计时器
DOVirtual.DelayedCall(5f, () => SpawnEnemy());  // 暂停时应该停止
```

---

## 🚀 性能说明

### SetUpdate(true) 的开销

**几乎没有额外开销**：
- DOTween 内部只是切换时间获取方式
- `Time.time` → `Time.unscaledTime`
- 不会创建额外的 Update 循环

### 最佳实践

```csharp
// ✅ 推荐：链式调用
transform.DOMove(target, 1f)
    .SetEase(Ease.InOutQuad)
    .SetUpdate(true);

// ✅ 也可以：分步设置
Tween tween = transform.DOMove(target, 1f);
tween.SetEase(Ease.InOutQuad);
tween.SetUpdate(true);

// ❌ 不推荐：创建后不保存引用（难以 Kill）
transform.DOMove(target, 1f).SetUpdate(true);
// 无法在外部 Kill 这个 Tween
```

---

## 🎓 扩展知识

### Unity 中的时间系统

```csharp
// 1. 游戏时间（受 timeScale 影响）
float gameTime = Time.time;              // 总游戏时间
float deltaTime = Time.deltaTime;        // 上一帧到这一帧的时间

// 2. 真实时间（不受 timeScale 影响）
float realTime = Time.unscaledTime;      // 总真实时间
float realDelta = Time.unscaledDeltaTime; // 上一帧到这一帧的真实时间

// 3. 固定时间步
float fixedTime = Time.fixedTime;        // 物理更新时间
float fixedDelta = Time.fixedDeltaTime;  // 固定更新间隔（默认 0.02）
```

### 协程中的时间

```csharp
IEnumerator Example()
{
    // ❌ 受 timeScale 影响
    yield return new WaitForSeconds(1f);
    // Time.timeScale = 0 时，永远不会继续

    // ✅ 不受 timeScale 影响
    yield return new WaitForSecondsRealtime(1f);
    // Time.timeScale = 0 时，1秒后继续

    // ✅ 不受 timeScale 影响
    yield return new WaitForEndOfFrame();
    // 每帧都会继续

    // ✅ 不受 timeScale 影响
    yield return null;
    // 下一帧继续
}
```

---

## 🎉 总结

### 问题
- Time.timeScale = 0 导致 DOTween 停止工作
- 暂停和 Victory 时音量变化无效

### 解决
- 所有音频相关的 Tweens 添加 `.SetUpdate(true)`
- 协程中使用 `WaitForSecondsRealtime` 代替 `WaitForSeconds`

### 结果
- ✅ 暂停时音量正常降低和恢复
- ✅ Victory 时音乐正常切换和升高音量
- ✅ 所有状态转换平滑无误

---

**修复完成！现在 AudioManager 在所有情况下都能正常工作！** 🎵✨
