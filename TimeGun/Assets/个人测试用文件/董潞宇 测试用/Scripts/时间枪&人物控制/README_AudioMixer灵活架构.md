# AudioMixer 灵活架构配置指南

## 🎯 核心理念

**分离关注点**：
- **BackgroundMusic 组**：纯净的音乐播放（无效果器）
- **RewindEffects 组**：回溯效果处理（Lowpass + Pitch + Echo）

**优势**：
- ✅ BGM 音质纯净，不受效果器影响
- ✅ 回溯效果可随时调整
- ✅ 支持多种架构模式

---

## 📐 三种架构模式

### 方案A：单组架构（简单 - 当前你的结构）

```
Master
└─ Music
   └─ BackgroundMusic
      ├─ Lowpass Simple
      ├─ Pitch Shifter
      └─ Echo
```

**配置步骤**：
1. 保持现有结构
2. 设置效果器为"透明"状态：
   ```
   Lowpass: Cutoff = 22000 Hz
   Pitch: 1.0
   Echo: Wet Mix = 0
   ```
3. 在 AudioManager Inspector 中：
   ```
   BGM Group Name: "BackgroundMusic"
   Effects Group Name: "BackgroundMusic"  ← 与 BGM 相同
   ```

**适用场景**：快速开始，效果器数量少

---

### 方案B：双组架构（推荐 - 最灵活）

```
Master
└─ Music
   ├─ BackgroundMusic  ← BGM 播放（干净）
   └─ RewindEffects    ← 回溯效果（Lowpass + Pitch + Echo）
```

#### 配置步骤

##### 1. 重新组织 Mixer 结构

1. 打开 `GameAudioMixer`
2. 选中 `BackgroundMusic` 组
3. **移除所有效果器**（Lowpass、Pitch、Echo）
4. 在 `Music` 组下创建新组 `RewindEffects`
5. 为 `RewindEffects` 添加效果器：
   - Lowpass Simple
   - Pitch Shifter
   - Echo

##### 2. 设置信号路由

关键：让 BackgroundMusic 的信号**同时**发送到 Master 和 RewindEffects

```
方式1：使用 Send（推荐）
BackgroundMusic → Send → RewindEffects
                ↘ Master

方式2：使用子组
BackgroundMusic → RewindEffects → Master
```

**操作（使用 Send）**：
1. 选中 `BackgroundMusic` 组
2. 在 Inspector 中找到 **Send** 区域
3. 点击 **+** 添加 Send
4. 选择 `RewindEffects` 作为目标
5. 设置 **Send Level** = 0 dB（全强度）

##### 3. 配置 RewindEffects 效果器

```
Lowpass Simple:
├─ Cutoff freq: 22000 Hz  ← 初始值（透明）
└─ Resonance: 1.0

Pitch Shifter:
├─ Pitch: 1.0  ← 初始值（透明）
├─ FFT Size: 2048
└─ Overlap: 4

Echo:
├─ Delay: 300 ms
├─ Decay: 0.5
├─ Dry Mix: 1.0
└─ Wet Mix: 0.0  ← 初始值（透明）
```

##### 4. 暴露参数

所有参数都在 **RewindEffects** 组上暴露：

```
右键点击 → Expose to script → 重命名：
├─ Lowpass Cutoff freq → "BGMLowpassCutoff"
├─ Pitch Shifter Pitch → "BGMPitch"
└─ Echo Wet Mix → "BGMEchoWetMix"
```

##### 5. AudioManager 配置

在 Inspector 中设置：
```
AudioMixer（可选 - 用于回溯效果）
├─ Audio Mixer: [GameAudioMixer]
├─ BGM Group Name: "BackgroundMusic"      ← BGM 播放组
├─ Effects Group Name: "RewindEffects"    ← 效果处理组
├─ BGM Lowpass Parameter: "BGMLowpassCutoff"
├─ BGM Pitch Parameter: "BGMPitch"
└─ BGM Echo Parameter: "BGMEchoWetMix"

回溯效果参数
├─ Rewind Lowpass Cutoff: 800 Hz
├─ Rewind Pitch: 0.85
├─ Rewind Echo Wet Mix: 0.3  ← Echo 强度（0-1）
├─ Normal Lowpass Cutoff: 22000 Hz
├─ Normal Pitch: 1.0
└─ Normal Echo Wet Mix: 0.0  ← 正常时无 Echo
```

---

### 方案C：三组架构（专业 - 最细粒度）

```
Master
└─ Music
   ├─ BackgroundMusic  ← 纯净 BGM
   ├─ LowpassEffect    ← 低通滤波
   ├─ PitchEffect      ← 音调变换
   └─ EchoEffect       ← 回声效果
```

**优势**：每个效果器独立控制
**劣势**：配置复杂，路由复杂

**不推荐**：除非有特殊需求

---

## 🎵 Echo 参数调优

### Echo 是什么？

Echo（回声）会让声音"重复"，营造空间感：

```
原始：♪♫♬
Echo：♪♫♬...♪♫♬...♪♫♬（越来越小）
```

### 推荐 Echo 参数

| 场景 | Delay | Decay | Wet Mix | 效果描述 |
|------|-------|-------|---------|---------|
| **轻微空间感** | 200ms | 0.3 | 0.2 | 像在小房间 |
| **明显回声** | 300ms | 0.5 | 0.3 | 像在走廊 |
| **强烈回声** | 500ms | 0.7 | 0.5 | 像在山谷 |

### 回溯效果推荐

```
正常播放：
└─ Wet Mix: 0.0（无回声）

回溯时：
└─ Wet Mix: 0.3（轻微回声，增强"时空扭曲"感）
```

**为什么 0.3？**
- 太低（< 0.2）：听不出效果
- 适中（0.2-0.4）：营造空间感但不混乱 ✅
- 太高（> 0.5）：音乐变得散乱、难以辨识

---

## 🎨 效果器组合建议

### 组合1：沉闷低频（推荐）

```
✅ Lowpass: 800 Hz
✅ Pitch: 0.85
❌ Echo: 0.0（禁用）

效果：沉闷、低沉，清晰的"时间扭曲"感
适合：快节奏游戏，频繁使用回溯
```

### 组合2：时空裂缝（戏剧化）

```
✅ Lowpass: 600 Hz
✅ Pitch: 0.8
✅ Echo: 0.3

效果：沉闷 + 低沉 + 空间感，强烈的"异次元"感觉
适合：重要剧情时刻，长时间回溯
```

### 组合3：梦幻回溯（实验性）

```
✅ Lowpass: 1000 Hz
✅ Pitch: 0.9
✅ Echo: 0.4
➕ Reverb: Room -500 dB（如果有）

效果：朦胧、空灵，像回忆场景
适合：故事驱动游戏
```

---

## 🔧 配置对比表

| 架构模式 | 配置难度 | 灵活性 | 音质 | 推荐度 |
|---------|---------|--------|------|--------|
| **单组架构** | ⭐ 简单 | ⭐⭐ 中等 | ⭐⭐⭐⭐ 好 | ⭐⭐⭐ |
| **双组架构** | ⭐⭐ 中等 | ⭐⭐⭐⭐⭐ 极高 | ⭐⭐⭐⭐⭐ 极好 | ⭐⭐⭐⭐⭐ |
| **三组架构** | ⭐⭐⭐⭐ 复杂 | ⭐⭐⭐⭐⭐ 极高 | ⭐⭐⭐⭐⭐ 极好 | ⭐⭐ |

---

## 📋 快速迁移步骤（单组 → 双组）

### 1. 备份当前配置（1分钟）
```
1. 右键 GameAudioMixer → Duplicate
2. 重命名为 GameAudioMixer_Backup
```

### 2. 创建新组结构（2分钟）
```
1. 在 Music 下创建 RewindEffects 组
2. 为 RewindEffects 添加：
   - Lowpass Simple
   - Pitch Shifter
   - Echo
3. 设置所有效果器为"透明"状态
```

### 3. 设置信号路由（3分钟）
```
1. 选中 BackgroundMusic
2. 添加 Send → RewindEffects
3. Send Level = 0 dB
```

### 4. 从 BackgroundMusic 移除效果器（1分钟）
```
1. 选中 BackgroundMusic
2. 右键每个效果器 → Remove Effect
```

### 5. 暴露参数（2分钟）
```
在 RewindEffects 组上暴露：
- Lowpass Cutoff → BGMLowpassCutoff
- Pitch → BGMPitch
- Echo Wet Mix → BGMEchoWetMix
```

### 6. 更新 AudioManager（30秒）
```
Effects Group Name: "RewindEffects"
```

### 7. 测试（1分钟）
```
运行游戏 → 测试回溯效果
```

**总计时间：~10分钟**

---

## 🧪 测试检查清单

### 正常播放测试
```
1. 运行游戏
2. 播放 Gameplay 音乐
3. ✅ 音质清晰？无沉闷、无回声？
4. ✅ 音调正常？
```

### 回溯效果测试
```
1. 按住回溯键
2. ✅ 音乐变沉闷（Lowpass）？
3. ✅ 音调降低（Pitch）？
4. ✅ 有轻微回声（Echo）？
5. ✅ 过渡平滑（1秒内完成）？
```

### 恢复测试
```
1. 松开回溯键
2. ✅ 音乐恢复清晰？
3. ✅ 音调恢复正常？
4. ✅ 回声消失？
5. ✅ 过渡平滑？
```

---

## 💡 专业建议

### 什么时候使用单组架构？

```
适合：
✅ 快速原型开发
✅ 效果器数量少（< 3个）
✅ 不需要频繁调整效果

不适合：
❌ 需要精细音质控制
❌ 效果器数量多
❌ 需要独立控制每个效果
```

### 什么时候使用双组架构？

```
适合：
✅ 追求高音质
✅ 需要灵活调整效果
✅ BGM 和效果分离管理
✅ 专业音频制作

推荐：
⭐⭐⭐⭐⭐ 强烈推荐用于正式项目
```

---

## 🎯 我的建议

### 对于你的项目

**推荐：双组架构 + 保留 Echo**

```
理由：
1. Echo 效果确实不错（你自己也觉得好）
2. 双组架构音质最优
3. 未来调整灵活

配置：
├─ BackgroundMusic（纯净播放）
└─ RewindEffects（Lowpass + Pitch + Echo）
   └─ Echo Wet Mix: 0.3（回溯时）
```

### Echo 参数建议

```
正常播放：Wet Mix = 0
回溯时：Wet Mix = 0.3

Delay: 300 ms
Decay: 0.5
Dry Mix: 1.0
```

**效果**：沉闷 + 低沉 + 空间感，时空扭曲感很强 ✨

---

## 🚀 下一步行动

### 选择1：保持单组（简单）
```
1. 设置 Echo Wet Mix = 0（正常）
2. 暴露 Echo Wet Mix 参数
3. 在 AudioManager 中配置
✅ 10分钟完成
```

### 选择2：升级双组（推荐）
```
1. 按照迁移步骤操作
2. 重新组织 Mixer 结构
3. 测试验证
✅ 15分钟完成，音质最佳
```

---

**我的建议：花15分钟升级到双组架构，未来会感谢自己！** 🎵✨
