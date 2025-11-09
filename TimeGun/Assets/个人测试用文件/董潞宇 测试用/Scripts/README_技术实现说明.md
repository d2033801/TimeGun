# 技术实现说明与架构设计

本文档说明新增功能的技术细节、架构设计思路和扩展性考虑。

---

## 架构设计理念

### 1. 低耦合（Loose Coupling）

所有新增系统都采用**事件驱动架构（Event-Driven Architecture）**，避免直接引用和硬编码依赖：

#### 弹药系统 ↔ UI
```csharp
// ❌ 不好的设计：UI直接引用弹药系统
public class AmmoUI : MonoBehaviour
{
    private AmmoSystem ammoSystem;  // 硬编码依赖
    
    void Update()
    {
        ammoSystem = FindObjectOfType<AmmoSystem>();  // 每帧查找，性能差
        text.text = ammoSystem.CurrentBullets.ToString();
    }
}

// ✅ 好的设计：使用事件解耦
public class AmmoUI : MonoBehaviour
{
    void Start()
    {
        AmmoSystem.OnAmmoChanged += OnAmmoChanged;  // 订阅事件
    }
    
    void OnAmmoChanged(AmmoType type, int current, int max)
    {
        // 只在变化时更新，而非每帧轮询
        if (type == AmmoType.Bullet)
            text.text = $"{current}/{max}";
    }
}
```

**优点**：
- UI和弹药系统完全解耦，可以独立修改
- UI可以在任何时候订阅/取消订阅，甚至在运行时动态切换
- 性能更好（事件驱动而非每帧轮询）

#### 全局回溯 ↔ 音频 ↔ 玩家控制
```
GlobalTimeRewindManager (发布者)
    ↓ OnGlobalRewindStarted 事件
    ├─→ AudioManager (订阅者)
    └─→ PlayerController (订阅者)
```

**优点**：
- AudioManager和PlayerController不需要知道彼此的存在
- 可以轻松添加更多订阅者（例如UI提示、特效控制器等）

---

### 2. 高内聚（High Cohesion）

每个系统的所有相关逻辑都封装在一个组件内：

#### 弹药系统
```
AmmoSystem (单一职责)
├─ 状态：当前弹药数、最大弹药数、CD计时器
├─ 逻辑：消耗弹药、恢复弹药、CD计算
└─ 接口：Try方法、状态查询、事件发布
```

#### 交互系统
```
HoldToInteract (单一职责)
├─ 状态：玩家是否在范围内、按住进度、是否完成
├─ 逻辑：触发器检测、按键检测、进度计算
└─ 接口：进度查询、重置、事件发布
```

**优点**：
- 易于理解和维护
- 可以整体替换或扩展
- 单元测试友好

---

### 3. 可扩展性（Extensibility）

所有系统都预留了扩展点：

#### 弹药系统 - 添加新弹药类型

```csharp
// Step 1: 扩展枚举
public enum AmmoType
{
    Bullet,
    Grenade,
    Rocket,     // ✅ 新增
    Missile     // ✅ 新增
}

// Step 2: 添加字段和属性
[SerializeField] private int maxRockets = 1;
[SerializeField] private float rocketRestoreTime = 60f;
private int currentRockets = 1;
private float rocketRestoreTimer = 0f;

public int CurrentRockets => currentRockets;
// ... 其他属性

// Step 3: 在Update中调用恢复逻辑
void Update()
{
    UpdateAmmoRestore(ref bulletRestoreTimer, bulletRestoreTime, ref currentBullets, maxBullets, AmmoType.Bullet);
    UpdateAmmoRestore(ref grenadeRestoreTimer, grenadeRestoreTime, ref currentGrenades, maxGrenades, AmmoType.Grenade);
    UpdateAmmoRestore(ref rocketRestoreTimer, rocketRestoreTime, ref currentRockets, maxRockets, AmmoType.Rocket);  // ✅ 新增
}
```

#### 音频管理器 - 添加新音乐状态

```csharp
// Step 1: 扩展枚举
private enum MusicState
{
    None,
    MainMenu,
    Gameplay,
    Victory,
    Rewind,
    Boss,      // ✅ 新增
    Credits    // ✅ 新增
}

// Step 2: 添加音乐片段
[SerializeField] private AudioClip bossMusic;
[SerializeField] private AudioClip creditsMusic;

// Step 3: 添加静态方法
public static void PlayBossMusic()
{
    Instance.PlayMusic(MusicState.Boss, Instance.bossMusic);
}
```

---

## 技术细节

### 1. 时间回溯系统的快照机制

#### 问题：如何让计时器也能被回溯？

传统方法：
```csharp
// ❌ 问题：计时器持续累加，回溯时无法恢复
private float timer = 0f;

void Update()
{
    timer += Time.deltaTime;  // 无法回溯
}
```

我们的解决方案：
```csharp
// ✅ 解决方案：将计时器作为快照数据的一部分
private struct DropStateSnapshot
{
    public bool HasFallen;
    public float Timer;       // ✅ 计时器也被记录
    public bool UseGravity;
    public bool IsKinematic;
}

protected override void RecordOneSnap()
{
    var snap = new DropStateSnapshot
    {
        Timer = currentTimer,  // ✅ 录制计时器
        // ...
    };
    _dropStateHistory.Push(snap);
}

protected override void RewindOneSnap()
{
    var snap = _dropStateHistory.PopBack();
    currentTimer = snap.Timer;  // ✅ 回溯计时器
    // ...
}
```

**优点**：
- 计时器可以随时间回溯而倒退
- 符合物理直觉（"时间倒流"）

---

### 2. 全局回溯的自动停止机制

#### 问题：如何检测所有物体都已回溯完毕？

我们的解决方案：
```csharp
// ✅ 使用HashSet追踪正在回溯的物体
private HashSet<AbstractTimeRewindObject> _activeRewindingObjects = new HashSet<AbstractTimeRewindObject>();

// 开始回溯时，将所有物体加入集合
public void StartGlobalRewind(float? speed = null)
{
    _activeRewindingObjects.Clear();
    
    foreach (var obj in _trackedObjects)
    {
        if (obj != null)
        {
            obj.StartRewind(rewindSpeed);
            _activeRewindingObjects.Add(obj);  // ✅ 加入追踪
        }
    }
}

// 在基类中触发停止事件
public virtual void StopRewind()
{
    if (!isRewinding) return;
    isRewinding = false;
    
    OnAnyObjectStoppedRewind?.Invoke(this);  // ✅ 通知管理器
}

// 管理器监听停止事件
private void OnTrackedObjectStoppedRewind(AbstractTimeRewindObject stoppedObject)
{
    _activeRewindingObjects.Remove(stoppedObject);
    
    // ✅ 如果所有物体都停止了，自动停止全局特效
    if (_activeRewindingObjects.Count == 0)
    {
        isGlobalRewinding = false;
    }
}
```

**优点**：
- 自动检测，无需手动判断
- 支持物体在不同时间停止回溯（历史记录长度不同）
- 线程安全（使用事件而非直接回调）

---

### 3. 音频的平滑过渡

#### 问题：音乐切换时如何避免突兀？

我们的解决方案：
```csharp
// ✅ 使用DOTween实现交叉淡入淡出（Crossfade）
private IEnumerator CrossfadeMusic(AudioClip newClip)
{
    if (_mainAudioSource.isPlaying)
    {
        // 新音乐在第二音源播放
        _secondAudioSource.clip = newClip;
        _secondAudioSource.Play();
        _secondAudioSource.DOFade(defaultVolume, crossfadeTime);  // 淡入
        
        // 旧音乐淡出
        _mainAudioSource.DOFade(0f, crossfadeTime);
        
        yield return new WaitForSeconds(crossfadeTime);
        
        // 交换音源（复用两个AudioSource）
        (_mainAudioSource, _secondAudioSource) = (_secondAudioSource, _mainAudioSource);
    }
    else
    {
        // 直接播放
        _mainAudioSource.clip = newClip;
        _mainAudioSource.volume = 0f;
        _mainAudioSource.Play();
        _mainAudioSource.DOFade(defaultVolume, fadeTime);
    }
}
```

**优点**：
- 平滑过渡，无突兀感
- 只需两个AudioSource即可实现无限切换
- 使用DOTween，代码简洁

---

### 4. 弹药系统的通用恢复逻辑

#### 问题：如何避免代码重复？

我们的解决方案：
```csharp
// ✅ 提取通用方法
private void UpdateAmmoRestore(ref float timer, float restoreTime, ref int current, int max, AmmoType ammoType)
{
    if (current >= max || restoreTime <= 0f) 
    {
        timer = 0f;
        return;
    }

    timer += Time.deltaTime;

    if (timer >= restoreTime)
    {
        timer = 0f;
        current++;
        current = Mathf.Min(current, max);
        
        OnAmmoChanged?.Invoke(ammoType, current, max);
        OnAmmoRestored?.Invoke(ammoType);
    }
}

// ✅ 调用通用方法
void Update()
{
    UpdateAmmoRestore(ref bulletRestoreTimer, bulletRestoreTime, ref currentBullets, maxBullets, AmmoType.Bullet);
    UpdateAmmoRestore(ref grenadeRestoreTimer, grenadeRestoreTime, ref currentGrenades, maxGrenades, AmmoType.Grenade);
}
```

**优点**：
- DRY原则（Don't Repeat Yourself）
- 易于扩展（添加新弹药类型只需一行代码）
- 易于维护（修改逻辑只需改一处）

---

## 性能优化

### 1. 事件驱动 vs 每帧轮询

```csharp
// ❌ 糟糕的设计：每帧轮询
void Update()
{
    if (ammoSystem.CurrentBullets != lastBullets)
    {
        UpdateUI();
        lastBullets = ammoSystem.CurrentBullets;
    }
}

// ✅ 优秀的设计：事件驱动
void Start()
{
    AmmoSystem.OnAmmoChanged += UpdateUI;  // 只在变化时调用
}
```

**性能对比**：
- 轮询：每帧检查（60 FPS = 60次/秒）
- 事件：仅在变化时（可能每40秒1次）

---

### 2. 对象池（预留扩展点）

在AmmoAbstract中预留了对象池接口：

```csharp
protected virtual void Despawn()
{
    // ✅ 如果你用对象池，这里应归还池
    gameObject.SetActive(false);
}

protected virtual void OnEnable()
{
    lifeTimer = 0f;
}
```

使用对象池的好处：
- 减少GC（Garbage Collection）压力
- 避免频繁实例化/销毁的性能开销

---

### 3. HashSet vs List

在全局回溯管理器中使用HashSet：

```csharp
private HashSet<AbstractTimeRewindObject> _trackedObjects = new HashSet<AbstractTimeRewindObject>();
private HashSet<AbstractTimeRewindObject> _activeRewindingObjects = new HashSet<AbstractTimeRewindObject>();
```

**优点**：
- Add/Remove操作：O(1) vs List的O(n)
- 自动去重，无需担心重复添加

---

## Unity 6.2 新特性应用

### 1. FindFirstObjectByType (替代FindObjectOfType)

```csharp
// ❌ 旧API（已弃用）
var instance = FindObjectOfType<AudioManager>();

// ✅ 新API（Unity 6.2推荐）
var instance = FindFirstObjectByType<AudioManager>();
```

**性能对比**：
- FindFirstObjectByType：找到第一个即返回（更快）
- FindObjectOfType：遍历所有对象（更慢）

---

### 2. ReadOnly Attribute（Inspector优化）

```csharp
[SerializeField, ReadOnly] private bool hasFallen = false;

// 自定义PropertyDrawer
#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;  // ✅ Inspector中只读显示
        UnityEditor.EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif
```

**优点**：
- 在Inspector中显示运行时状态
- 防止误修改
- 调试友好

---

## 最佳实践

### 1. 事件订阅生命周期管理

```csharp
// ✅ 正确的事件订阅模式
void Start()
{
    GlobalTimeRewindManager.OnGlobalRewindStarted += OnRewindStarted;
}

void OnDestroy()
{
    // ⚠️ 重要：必须取消订阅，防止内存泄漏
    GlobalTimeRewindManager.OnGlobalRewindStarted -= OnRewindStarted;
}
```

**常见错误**：
- 忘记取消订阅 → 内存泄漏
- 在OnDisable中取消订阅 → 如果对象被禁用再启用，事件会失效

---

### 2. 单例模式的线程安全

```csharp
// ✅ Unity单例模式（推荐）
private static AudioManager _instance;
public static AudioManager Instance
{
    get
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<AudioManager>();
            if (_instance == null)
            {
                var go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
            }
        }
        return _instance;
    }
}

void Awake()
{
    if (_instance != null && _instance != this)
    {
        Destroy(gameObject);  // ✅ 防止重复
        return;
    }
    _instance = this;
    DontDestroyOnLoad(gameObject);
}
```

**注意**：
- Unity不是多线程环境，无需加锁
- 使用Awake初始化，确保生命周期正确

---

### 3. 避免Find性能开销

```csharp
// ❌ 糟糕的设计：每帧查找
void Update()
{
    var ammo = FindObjectOfType<AmmoSystem>();  // 性能杀手
}

// ✅ 优秀的设计：缓存引用
private AmmoSystem _ammoSystem;

void Awake()
{
    _ammoSystem = GetComponent<AmmoSystem>();  // 只查找一次
}
```

---

## 测试建议

### 1. 单元测试场景

创建独立测试场景：
- TestScene_AmmoSystem：只测试弹药系统
- TestScene_Rewind：只测试回溯系统
- TestScene_Interact：只测试交互系统

### 2. 压力测试

```csharp
// 压力测试：1000个可回溯物体
for (int i = 0; i < 1000; i++)
{
    var obj = Instantiate(fallingObjectPrefab);
    obj.GetComponent<RewindableFallingObject>().autoStartTimer = true;
}

// 观察：
// - 帧率是否下降
// - 内存是否增长
// - 回溯是否流畅
```

---

## 常见陷阱

### 1. 事件订阅在Awake还是Start？

```csharp
// ⚠️ 问题：如果在Awake订阅，但发布者也在Awake初始化
void Awake()
{
    GlobalTimeRewindManager.OnGlobalRewindStarted += OnRewindStarted;  // 可能为null
}

// ✅ 解决方案：使用null条件运算符
void Awake()
{
    if (TimeRewind.GlobalTimeRewindManager.Instance != null)
    {
        TimeRewind.GlobalTimeRewindManager.OnGlobalRewindStarted += OnRewindStarted;
    }
}

// 或者：在Start中订阅（推荐）
void Start()
{
    TimeRewind.GlobalTimeRewindManager.OnGlobalRewindStarted += OnRewindStarted;
}
```

---

### 2. DOTween的OnComplete回调

```csharp
// ⚠️ 问题：对象销毁后回调仍会执行
_mainAudioSource.DOFade(0f, fadeTime).OnComplete(() => 
{
    _mainAudioSource.Stop();  // 可能已销毁
});

// ✅ 解决方案：检查对象是否存在
_mainAudioSource.DOFade(0f, fadeTime).OnComplete(() => 
{
    if (_mainAudioSource != null)
    {
        _mainAudioSource.Stop();
    }
});
```

---

## 扩展建议

### 1. 数据驱动配置

将硬编码的参数移到ScriptableObject：

```csharp
[CreateAssetMenu(menuName = "TimeGun/Ammo Config")]
public class AmmoConfig : ScriptableObject
{
    public int maxBullets = 2;
    public float bulletRestoreTime = 40f;
    public int maxGrenades = 1;
    public float grenadeRestoreTime = 40f;
}

// 在AmmoSystem中引用
[SerializeField] private AmmoConfig config;
```

**优点**：
- 可以创建多个配置（例如：简单模式、困难模式）
- 策划可以直接修改数据，无需改代码

---

### 2. 保存系统集成

为弹药系统添加保存/加载：

```csharp
[System.Serializable]
public class AmmoSaveData
{
    public int currentBullets;
    public int currentGrenades;
    public float bulletRestoreTimer;
    public float grenadeRestoreTimer;
}

public AmmoSaveData SaveData()
{
    return new AmmoSaveData
    {
        currentBullets = this.currentBullets,
        // ...
    };
}

public void LoadData(AmmoSaveData data)
{
    this.currentBullets = data.currentBullets;
    // ...
}
```

---

## 总结

本次实现遵循了以下原则：

1. **SOLID原则**：
   - **S**ingle Responsibility：每个类只有一个职责
   - **O**pen/Closed：对扩展开放，对修改关闭
   - **L**iskov Substitution：子类可以替换父类
   - **I**nterface Segregation：接口隔离
   - **D**ependency Inversion：依赖倒置（事件驱动）

2. **设计模式**：
   - Singleton：AudioManager、GlobalTimeRewindManager
   - Observer：事件驱动架构
   - Template Method：AbstractTimeRewindObject的回溯框架

3. **Unity最佳实践**：
   - Component-based：每个功能都是独立组件
   - Event-driven：使用C#事件而非直接引用
   - Performance：缓存引用、使用HashSet、避免Find

希望这些技术细节能帮助你理解代码的设计思路，并为未来的扩展提供参考！
