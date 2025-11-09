# AudioManager 快速配置指南（BGM 回溯版本）

## 🚀 5分钟快速开始

### 核心设计理念

**之前的方案**：切换到专用的回溯音轨（RewindMusic）
**现在的方案**：直接对当前播放的BGM应用音效处理

**优势：**
- ✅ 音乐保持连贯性（不中断当前播放）
- ✅ 无需准备额外的回溯音频文件
- ✅ 实时动态处理，效果更自然
- ✅ 可以平滑地调整回溯强度

---

## 步骤1：创建 AudioMixer（2分钟）

1. **Project 窗口** → 右键 → **Create** → **Audio Mixer**
2. 命名为 `GameAudioMixer`
3. 双击打开 **Audio Mixer 窗口**

---

## 步骤2：配置混音器结构（1分钟）

**简化的结构（只需要BGM）：**

```
Master
└─ Music
   └─ BackgroundMusic  ← 所有BGM都在这里，回溯时直接处理
```

**操作：**
- 选中 `Master` → 右键 → **Add child group** → 命名 `Music`
- 选中 `Music` → 右键 → **Add child group** → 命名 `BackgroundMusic`

---

## 步骤3：为 BackgroundMusic 添加音效器（2分钟）

**关键：** 效果器添加到 **BackgroundMusic** 组（不是 RewindMusic）

选中 **BackgroundMusic** 组，点击 **Add Effect**：

### 效果器1：Lowpass Simple（必需）
```
Cutoff freq: 22000 Hz  ← 初始值（全频段）
Resonance: 1.5
```

### 效果器2：Pitch Shifter（推荐）
```
Pitch: 1.0  ← 初始值（正常音调）
FFT Size: 2048
Overlap: 4
```

### 效果器3：Echo（可选，增强效果）
```
Delay: 300 ms
Decay: 0.4
Dry Mix: 0.8
Wet Mix: 0.3
```

---

## 步骤4：暴露参数到脚本（1分钟）

**必需参数（全部在 BackgroundMusic 组）：**

1. 选中 `Music` 组 → 右键点击 **Volume** → **Expose to script**
   - 重命名为 `MusicVolume`

2. 选中 `BackgroundMusic` 组 → 暴露 **Volume**
   - 重命名为 `BGMVolume`

3. **关键效果：** 选中 `BackgroundMusic` → 展开 **Lowpass Simple**
   - 右键 **Cutoff freq** → **Expose to script**
   - 重命名为 `BGMLowpassCutoff`  ← 注意名称变化！

4. **可选：** 展开 **Pitch Shifter** → 暴露 **Pitch**
   - 重命名为 `BGMPitch`  ← 注意名称变化！

**⚠️ 重要：参数名称已更改**
```
旧名称 → 新名称
RewindLowpassCutoff → BGMLowpassCutoff
RewindPitch → BGMPitch
RewindVolume → （已删除，不再需要）
```

---

## 步骤5：配置 AudioManager（1分钟）

选中场景中的 `AudioManager` 对象，在 Inspector 中：

```
AudioMixer配置
├─ Audio Mixer: [拖入 GameAudioMixer]
├─ Master Volume Parameter: "MasterVolume"
├─ Music Volume Parameter: "MusicVolume"
├─ BGM Volume Parameter: "BGMVolume"        ← 控制BGM音量
├─ BGM Lowpass Parameter: "BGMLowpassCutoff"  ← 关键效果！
└─ BGM Pitch Parameter: "BGMPitch"          ← 音调变换

回溯音效参数
├─ Rewind Lowpass Cutoff: 800 Hz   ← 回溯时的滤波器频率
├─ Rewind Pitch: 0.85               ← 回溯时的音调
├─ Normal Lowpass Cutoff: 22000     ← 正常时的频率（全频段）
├─ Normal Pitch: 1.0                ← 正常时的音调
└─ Rewind BGM Volume: 0.5           ← 回溯时的音量
```

---

## 步骤6：测试（30秒）

1. **运行游戏**
2. **触发回溯**（按住时间枪右键）
3. **观察效果**：
   - ✅ **当前播放的BGM**变沉闷？
   - ✅ 音调降低？
   - ✅ 音乐**没有中断**？
   - ✅ 过渡平滑？

---

## 🎨 效果对比

### 旧方案（切换音轨）
```
正常播放：♪♫♬ (BGM - 主旋律)
  ↓ 切换
回溯状态：♩♪♫ (RewindMusic - 沉闷版本)
  ↓ 切换
正常播放：♪♫♬ (BGM - 主旋律)

问题：
❌ 音乐中断（切换时有断点）
❌ 需要准备两个版本的音频
❌ 切换时可能不同步
```

### 新方案（实时处理BGM）
```
正常播放：♪♫♬ (BGM - 清晰)
  ↓ 动态处理（不中断）
回溯状态：♪♫♬ (同一BGM - 沉闷+低沉)
  ↓ 恢复处理（不中断）
正常播放：♪♫♬ (BGM - 清晰)

优势：
✅ 音乐连贯（无缝过渡）
✅ 只需一个音频文件
✅ 完美同步
✅ 可以动态调整强度
```

---

## 🎯 参数调优

### 推荐配置方案

#### 方案A：轻微回溯（频繁使用）
```
Rewind Lowpass Cutoff: 1500 Hz
Rewind Pitch: 0.9
Rewind BGM Volume: 0.6
```
**效果：** 轻微变沉闷，适合短时间频繁回溯

#### 方案B：明显回溯（推荐）
```
Rewind Lowpass Cutoff: 800 Hz
Rewind Pitch: 0.85
Rewind BGM Volume: 0.5
```
**效果：** 明显的回溯感，音乐变低沉且沉闷

#### 方案C：强烈回溯（戏剧性）
```
Rewind Lowpass Cutoff: 500 Hz
Rewind Pitch: 0.7
Rewind BGM Volume: 0.3
```
**效果：** 极强的"时空扭曲"感，适合重要剧情

---

## 🔧 实时调试

### 启用调试模式

在 AudioManager Inspector 中：

```
调试工具
├─ Enable Debug Mode: ✓  ← 勾选
└─ Debug Rewind Intensity: 0.0 - 1.0  ← 实时调整强度
```

**使用方法：**
1. 运行游戏
2. 触发回溯
3. 拖动 `Debug Rewind Intensity` 滑块
4. 听音效变化：
   - `0.0` = 完全正常
   - `0.5` = 中等回溯效果
   - `1.0` = 完全回溯效果

---

## 🎵 动态强度控制（高级）

### 根据回溯速度调整效果

在您的回溯代码中添加：

```csharp
// 示例：根据回溯速度动态调整音效强度
float rewindSpeed = GlobalTimeRewindManager.Instance.CurrentRewindSpeed;
float intensity = Mathf.Clamp01(rewindSpeed / 2f);  // 假设最大速度2x
AudioManager.SetRewindIntensity(intensity);
```

**效果：**
```
慢速回溯 (0.5x) → Intensity 0.25 → 轻微效果
正常回溯 (1.0x) → Intensity 0.5  → 中等效果
快速回溯 (2.0x) → Intensity 1.0  → 完全效果
```

---

## 🔍 常见问题排查

### ❌ 问题：回溯时BGM没有变化

**检查清单：**
1. [ ] AudioMixer Asset 已分配？
2. [ ] 参数名称正确？（`BGMLowpassCutoff` 不是 `RewindLowpassCutoff`）
3. [ ] AudioSource 的 Output 设置为 `BackgroundMusic` Group？
4. [ ] Inspector 中 **Audio Mixer Valid** 是 `True`？

**快速验证：**
```
运行游戏 → 查看 Console：
✅ "[AudioManager] ✅ AudioMixer 配置验证成功！"
✅ "[AudioManager] BGM Mixer Group 已分配: BackgroundMusic"
✅ "[AudioManager] 🔄 进入回溯状态 - 对BGM应用音效处理"
```

### ❌ 问题：效果不明显

**原因：** 参数设置太保守

**解决：**
```
降低 Rewind Lowpass Cutoff: 500 Hz - 800 Hz
降低 Rewind Pitch: 0.7 - 0.85
增加过渡时间: 1.0s - 2.0s
```

### ❌ 问题：音效切换有爆音

**原因：** 过渡时间太短

**解决：**
```
增加 Fade Time: 至少 1.0 秒
使用 DOTween 的平滑缓动（代码中已实现）
```

---

## 📊 参数对照表

| 参数名称（Inspector） | AudioMixer 参数名 | 作用 | 推荐值 |
|---------------------|-----------------|------|--------|
| **BGM Volume Parameter** | `BGMVolume` | BGM音量 | - |
| **BGM Lowpass Parameter** | `BGMLowpassCutoff` | 低通滤波器频率 | - |
| **BGM Pitch Parameter** | `BGMPitch` | 音调 | - |
| **Rewind Lowpass Cutoff** | （脚本参数） | 回溯时的滤波器频率 | 800 Hz |
| **Rewind Pitch** | （脚本参数） | 回溯时的音调 | 0.85 |
| **Rewind BGM Volume** | （脚本参数） | 回溯时的音量 | 0.5 |

---

## 🎯 配置检查清单

### AudioMixer 配置
- [ ] 创建了 `GameAudioMixer` Asset
- [ ] 分组结构：`Master → Music → BackgroundMusic`
- [ ] 为 **BackgroundMusic** 添加了效果器：
  - [ ] Lowpass Simple
  - [ ] Pitch Shifter（可选）
  - [ ] Echo（可选）
- [ ] 暴露了参数：
  - [ ] `MusicVolume`
  - [ ] `BGMVolume`
  - [ ] `BGMLowpassCutoff`
  - [ ] `BGMPitch`

### AudioManager 配置
- [ ] 拖入了 `GameAudioMixer`
- [ ] 填写了所有参数名称（注意拼写！）
- [ ] 设置了回溯音效参数（Cutoff、Pitch、Volume）
- [ ] AudioSource 的 Output 设置为 `BackgroundMusic` Group

### 测试验证
- [ ] 运行游戏，Console 无错误
- [ ] Inspector 中 `Audio Mixer Valid` 为 `True`
- [ ] 触发回溯，BGM 变沉闷
- [ ] 退出回溯，BGM 恢复正常

---

## 🎬 效果演示（文字版）

```
正常播放状态：
♪♫♬ 高音清晰 | 中音饱满 | 低音浑厚
[==================] 20Hz - 20kHz 全频段
音调: 1.0 (正常)
音量: 100%

↓ 按下回溯键（1秒过渡）

回溯状态：
♩♪♫ 高音消失 | 中音沉闷 | 低音突出
[=======░░░░░░░░░░░] 20Hz - 800Hz (低通滤波)
音调: 0.85 (降低)
音量: 50%

效果描述：
- 仿佛音乐在水下播放
- 低沉、遥远、模糊
- 时间倒流的"扭曲感"

↓ 松开回溯键（1秒过渡）

正常播放状态：
♪♫♬ 高音清晰 | 中音饱满 | 低音浑厚
[==================] 全频段恢复
音调: 1.0
音量: 100%
```

---

## 🚀 总结

### 与之前方案的对比

| 特性 | 旧方案（切换音轨） | 新方案（处理BGM） |
|------|------------------|-----------------|
| **音乐连贯性** | ❌ 切换时中断 | ✅ 无缝过渡 |
| **音频文件数量** | ❌ 需要2个版本 | ✅ 只需1个 |
| **同步问题** | ❌ 可能不同步 | ✅ 完美同步 |
| **动态调整** | ❌ 无法实时调整 | ✅ 支持强度控制 |
| **配置复杂度** | 中等 | 低 |
| **效果质量** | 取决于音频质量 | 实时处理，稳定 |

### 核心优势

1. **音乐不中断**：回溯时 BGM 继续播放，只是被实时处理
2. **效果自然**：平滑的1秒过渡，无突兀感
3. **节省资源**：不需要额外的音频文件
4. **灵活控制**：可以动态调整回溯强度

---

**配置完成后，您的BGM会在回溯时变得沉闷、低沉，仿佛时间在倒流！** 🎵⏪✨

**预计总配置时间：5分钟**
