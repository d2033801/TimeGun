#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TimeRewind.Editor
{
    /// <summary>
    /// 时间回溯配置编辑器工具
    /// 功能:
    /// - 批量管理场景中所有可回溯物体的配置
    /// - 预设方案(性能模式/平衡模式/质量模式)
    /// - 实时预览内存占用
    /// - 支持撤销/重做
    /// - ✅ 新增:通过反射自动检测所有子类快照结构,精确计算内存
    /// </summary>
    public class TimeRewindConfigEditor : EditorWindow
    {
        #region 窗口入口
        [MenuItem("Tools/时间回溯/配置管理器 &R")]
        public static void ShowWindow()
        {
            var window = GetWindow<TimeRewindConfigEditor>("回溯配置管理器");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        #endregion

        #region 配置预设
        private enum PresetMode
        {
            Custom,     // 自定义
            Performance, // 性能模式 (10秒 30FPS)
            Balanced,   // 平衡模式 (20秒 60FPS)
            Quality     // 质量模式 (30秒 120FPS)
        }

        private static readonly Dictionary<PresetMode, (int seconds, int fps, float speed)> Presets = new()
        {
            [PresetMode.Performance] = (10, 30, 2f),
            [PresetMode.Balanced] = (20, 60, 2f),
            [PresetMode.Quality] = (30, 120, 3f)
        };
        #endregion

        #region 编辑器状态
        private List<AbstractTimeRewindObject> _allRewindObjects = new();
        private Vector2 _scrollPos;
        private PresetMode _selectedPreset = PresetMode.Custom;

        // 批量修改参数
        private int _batchRecordSeconds = 20;
        private int _batchRecordFPS = 60;
        private float _batchRewindSpeed = 2f;

        // 过滤器
        private string _searchFilter = "";
        private bool _showOnlyCustom = false;

        // 内存预估
        private long _totalMemoryBytes = 0;

        // ✅ 新增:快照大小缓存(避免重复反射计算)
        private Dictionary<System.Type, int> _snapshotSizeCache = new Dictionary<System.Type, int>();
        #endregion

        #region Unity 回调
        private void OnEnable()
        {
            RefreshObjectList();
            EditorApplication.hierarchyChanged += RefreshObjectList;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= RefreshObjectList;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("场景回溯配置管理器", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawToolbar();
            DrawPresetSelector();
            DrawBatchControls();
            DrawMemoryEstimate();
            DrawObjectList();
        }
        #endregion

        #region 刷新对象列表
        private void RefreshObjectList()
        {
            _allRewindObjects = FindObjectsByType<AbstractTimeRewindObject>(FindObjectsSortMode.None)
                .OrderBy(obj => obj.name)
                .ToList();

            CalculateMemoryUsage();
        }
        #endregion

        #region 工具栏
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshObjectList();
            }

            GUILayout.FlexibleSpace();

            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            _showOnlyCustom = GUILayout.Toggle(_showOnlyCustom, "仅显示自定义", EditorStyles.toolbarButton, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }
        #endregion

        #region 预设选择器
        private void DrawPresetSelector()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("快速预设", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _selectedPreset = (PresetMode)EditorGUILayout.EnumPopup("预设模式", _selectedPreset);

            if (EditorGUI.EndChangeCheck() && _selectedPreset != PresetMode.Custom)
            {
                var preset = Presets[_selectedPreset];
                _batchRecordSeconds = preset.seconds;
                _batchRecordFPS = preset.fps;
                _batchRewindSpeed = preset.speed;
            }

            // 预设说明
            EditorGUILayout.HelpBox(GetPresetDescription(_selectedPreset), MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private string GetPresetDescription(PresetMode mode)
        {
            return mode switch
            {
                PresetMode.Performance => "性能模式: 10秒 30FPS - 适合移动端/低配置",
                PresetMode.Balanced => "平衡模式: 20秒 60FPS - 推荐配置",
                PresetMode.Quality => "质量模式: 30秒 120FPS - 高端配置/展示用",
                _ => "自定义模式: 手动调整参数"
            };
        }
        #endregion

        #region 批量控制
        private void DrawBatchControls()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("批量修改参数", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _batchRecordSeconds = EditorGUILayout.IntSlider("录制时长(秒)", _batchRecordSeconds, 0, 420);
            _batchRecordFPS = EditorGUILayout.IntSlider("录制帧率(FPS)", _batchRecordFPS, 10, 240);
            _batchRewindSpeed = EditorGUILayout.Slider("回溯速度倍率", _batchRewindSpeed, 0.5f, 10f);

            if (EditorGUI.EndChangeCheck())
            {
                _selectedPreset = PresetMode.Custom; // 手动修改后切换为自定义模式
                CalculateMemoryUsage(); // 实时更新内存预估
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _allRewindObjects.Count > 0;

            if (GUILayout.Button("应用到所有物体", GUILayout.Height(30)))
            {
                ApplyToAllObjects();
            }

            if (GUILayout.Button("应用到选中物体", GUILayout.Height(30)))
            {
                ApplyToSelectedObjects();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        #endregion

        #region 内存预估

        private void DrawMemoryEstimate()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("内存占用预估", EditorStyles.boldLabel);

            string memoryText = FormatBytes(_totalMemoryBytes);
            Color originalColor = GUI.color;

            // 根据内存大小改变颜色
            if (_totalMemoryBytes > 100 * 1024 * 1024) // >100MB
                GUI.color = Color.red;
            else if (_totalMemoryBytes > 50 * 1024 * 1024) // >50MB
                GUI.color = Color.yellow;
            else
                GUI.color = Color.green;

            EditorGUILayout.LabelField($"总内存: {memoryText}", EditorStyles.largeLabel);
            GUI.color = originalColor;

            EditorGUILayout.LabelField($"物体数量: {_allRewindObjects.Count}");
            EditorGUILayout.LabelField($"平均每物体: {FormatBytes(_allRewindObjects.Count > 0 ? _totalMemoryBytes / _allRewindObjects.Count : 0)}");

            // ✅ 新增:显示内存组成分析
            EditorGUILayout.Space(5);
            if (GUILayout.Button("显示内存详情", EditorStyles.miniButton))
            {
                ShowMemoryBreakdown();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// ✅ 新增:显示内存组成详情窗口
        /// </summary>
        private void ShowMemoryBreakdown()
        {
            var breakdown = new Dictionary<string, long>();

            foreach (var obj in _allRewindObjects)
            {
                if (obj == null) continue;

                string typeName = obj.GetType().Name;
                long memory = CalculateObjectMemory(obj);

                if (breakdown.ContainsKey(typeName))
                    breakdown[typeName] += memory;
                else
                    breakdown[typeName] = memory;
            }

            string message = "=== 内存占用详情 ===\n\n";
            foreach (var kvp in breakdown.OrderByDescending(x => x.Value))
            {
                message += $"{kvp.Key}: {FormatBytes(kvp.Value)}\n";
            }

            EditorUtility.DisplayDialog("内存详情", message, "确定");
        }

        /// <summary>
        /// ✅ 重构:动态计算总内存占用(自动检测所有快照结构)
        /// </summary>
        private void CalculateMemoryUsage()
        {
            _totalMemoryBytes = 0;

            foreach (var obj in _allRewindObjects)
            {
                if (obj == null) continue;

                _totalMemoryBytes += CalculateObjectMemory(obj);
            }
        }

        /// <summary>
        /// ✅ 新增:计算单个物体的内存占用(通过反射检测快照结构)
        /// </summary>
        private long CalculateObjectMemory(AbstractTimeRewindObject obj)
        {
            if (obj == null) return 0;

            int seconds = GetRecordSeconds(obj);
            int fps = GetRecordFPS(obj);
            int frames = seconds * fps;

            // 计算该物体类型的单帧快照大小
            int snapshotSize = GetSnapshotSizeForType(obj.GetType());

            return (long)(frames * snapshotSize);
        }

        /// <summary>
        /// ✅ 核心方法:通过反射获取物体类型的快照总大小
        /// </summary>
        private int GetSnapshotSizeForType(System.Type objectType)
        {
            // 缓存检查
            if (_snapshotSizeCache.TryGetValue(objectType, out int cachedSize))
                return cachedSize;

            int totalSize = 0;

            // 1. 基础 Transform 快照 (所有类都有)
            totalSize += CalculateStructSize(typeof(AbstractTimeRewindObject.TransformValuesSnapshot));

            // 2. 检测是否继承自 AbstractTimeRewindRigidBody
            if (IsSubclassOf(objectType, "AbstractTimeRewindRigidBody"))
            {
                totalSize += CalculateVelocitySnapshotSize();
            }

            // 3. 检测特定子类的快照结构
            if (objectType.Name == "EnemyTimeRewind")
            {
                totalSize += CalculateEnemySnapshotSize();
            }
            else if (objectType.Name == "AnimatorTimeRewind")
            {
                totalSize += CalculateAnimatorSnapshotSize();
            }

            // ✅ 4. 添加 RingBuffer 开销（更准确的估算）
            // - RingBuffer 对象本身：约 40 字节（对象头 + 字段）
            // - T[] 数组对象头：24 字节（对象头 + Length 字段）
            // - GC 堆对齐损耗：约 5%
            int ringBufferOverhead = 40 + 24; // 每个 RingBuffer 固定开销
            totalSize = (int)(totalSize * 1.05f) + ringBufferOverhead; // 加上对齐损耗

            // ✅ 5. 如果包含引用类型数组（如 AnimatorRecorder.Snapshot），额外计算
            if (objectType.Name == "EnemyTimeRewind" || objectType.Name == "AnimatorTimeRewind")
            {
                // 每个 Snapshot 中的数组都有对象开销
                // AnimatorSnapshot: 3 个数组对象 × 24 字节 = 72 字节/帧
                // EnemyTimeRewind 有 3 个 RingBuffer，每个都有开销
                totalSize += 72; // 数组对象开销
            }

            // 缓存结果
            _snapshotSizeCache[objectType] = totalSize;

            return totalSize;
        }

        /// <summary>
        /// 计算 TransformValuesSnapshot 大小
        /// Vector3(Position) + Quaternion(Rotation) + Vector3(Scale) = 12 + 16 + 12 = 40 字节
        /// </summary>
        private int CalculateStructSize(System.Type structType)
        {
            if (structType == typeof(AbstractTimeRewindObject.TransformValuesSnapshot))
                return 40; // Vector3*2 + Quaternion = 12 + 12 + 16 = 40

            return 0;
        }

        /// <summary>
        /// 计算 VelocityValuesSnapshot 大小
        /// Vector3(Velocity) + Vector3(AngularVelocity) = 12 + 12 = 24 字节
        /// </summary>
        private int CalculateVelocitySnapshotSize()
        {
            return 24; // Vector3 * 2
        }

        /// <summary>
        /// 计算 EnemyTimeRewind 的所有快照大小（修正版）
        /// AgentSnapshot + AnimatorSnapshot + EnemySnapshot + 引用开销
        /// </summary>
        private int CalculateEnemySnapshotSize()
        {
            int size = 0;

            // AgentSnapshot (NavMeshAgent)
            // bool*3 + float*3 + Vector3*2 + bool*2 ≈ 3 + 12 + 24 + 2 = 41 字节
            size += 44; // 对齐到4字节边界

            // AnimatorSnapshot (包含3个数组的引用，实际数据在堆上)
            // ✅ 修正：快照本身只存储数组引用（8字节×3）+ 数据
            // 假设: 3层*4字节(StateHash) + 3层*4字节(NormalizedTime) + 10个参数*4字节(ParamValue) 
            // = 12 + 12 + 40 = 64 字节（数组元素数据）
            // + 24字节×3（每个数组的对象头） = 72 字节
            // + 24字节（Snapshot 结构体中的3个引用） = 24 字节
            // 总计: 64 + 72 + 24 = 160 字节
            size += 160;

            // EnemySnapshot
            // bool + float*4 + int*2 ≈ 1 + 16 + 8 = 25
            size += 28;

            // ✅ 新增：EnemyTimeRewind 有 3 个独立的 RingBuffer
            // 每个都有额外的对象开销（上面已在 GetSnapshotSizeForType 中统一处理）

            return size; // 单个完整快照: 232 字节/帧
        }

        /// <summary>
        /// 计算 AnimatorTimeRewind 的快照大小（修正版）
        /// </summary>
        private int CalculateAnimatorSnapshotSize()
        {
            // AnimatorRecorder.Snapshot
            // ✅ 修正：包含数组对象开销
            // LayerStateHashes(int[3]): 12 字节数据 + 24 字节对象头 = 36
            // LayerNormalizedTimes(float[3]): 12 字节数据 + 24 字节对象头 = 36
            // ParameterValues(ParamValue[10]): 40 字节数据 + 24 字节对象头 = 64
            // Snapshot 本身: 24 字节（3个引用）
            // 总计: 36 + 36 + 64 + 24 = 160
            return 160; // 对齐后
        }

        #endregion

        #region 对象列表

        private void DrawObjectList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"场景物体列表 ({_allRewindObjects.Count})", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var filteredObjects = FilterObjects();

            foreach (var obj in filteredObjects)
            {
                if (obj == null) continue;
                DrawObjectRow(obj);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawObjectRow(AbstractTimeRewindObject obj)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            // 物体名称（可点击聚焦）+ 类型标签
            string displayName = $"{obj.name} [{obj.GetType().Name}]";
            if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Width(250)))
            {
                Selection.activeGameObject = obj.gameObject;
                EditorGUIUtility.PingObject(obj.gameObject);
            }

            GUILayout.FlexibleSpace();

            SerializedObject so = new SerializedObject(obj);
            var propSeconds = so.FindProperty("recordSecondsConfig");
            var propFPS = so.FindProperty("recordFPSConfig");
            var propSpeed = so.FindProperty("rewindSpeedConfig");

            int configSeconds = propSeconds != null ? propSeconds.intValue : 0;
            int configFPS = propFPS != null ? propFPS.intValue : 0;

            int displaySeconds = GetRecordSeconds(obj);
            int displayFPS = GetRecordFPS(obj);
            float displaySpeed = GetRewindSpeed(obj);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal(GUILayout.Width(350));
            
            // 录制时长
            EditorGUILayout.LabelField("回溯长度(秒)", GUILayout.Width(80));
            int newSeconds = EditorGUILayout.IntField(displaySeconds, GUILayout.Width(50));
            
            GUILayout.Space(5);
            EditorGUILayout.LabelField("|", GUILayout.Width(5));
            GUILayout.Space(5);

            // 录制帧率
            EditorGUILayout.LabelField("回溯帧率(fps)", GUILayout.Width(80));
            int newFPS = EditorGUILayout.IntField(displayFPS, GUILayout.Width(50));
            
            GUILayout.Space(5);
            EditorGUILayout.LabelField("|", GUILayout.Width(5));
            GUILayout.Space(5);

            // 回溯速度
            EditorGUILayout.LabelField("速度(倍)", GUILayout.Width(60));
            float newSpeed = EditorGUILayout.FloatField(displaySpeed, GUILayout.Width(50));
            
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(obj, "修改回溯配置");
                SetRecordSeconds(obj, newSeconds);
                SetRecordFPS(obj, newFPS);
                SetRewindSpeed(obj, newSpeed);
                CalculateMemoryUsage();
            }

            // 重置按钮
            if (GUILayout.Button("重置", GUILayout.Width(50)))
            {
                Undo.RecordObject(obj, "重置回溯配置");
                SetRecordSeconds(obj, 0);
                SetRecordFPS(obj, 0);
                SetRewindSpeed(obj, 2f);
                CalculateMemoryUsage();
            }

            EditorGUILayout.EndHorizontal();

            // 默认值提示
            if (configSeconds == 0 || configFPS == 0)
            {
                string defaultInfo = "使用默认配置: ";
                if (configSeconds == 0) defaultInfo += $"录制时长 {displaySeconds}秒 ";
                if (configFPS == 0) defaultInfo += $"帧率 {displayFPS}FPS";
                EditorGUILayout.LabelField(defaultInfo, EditorStyles.miniLabel);
            }

            // ✅ 优化:显示详细内存组成
            long objMemory = CalculateObjectMemory(obj);
            int snapshotSize = GetSnapshotSizeForType(obj.GetType());
            EditorGUILayout.LabelField(
                $"预估内存: {FormatBytes(objMemory)} (单帧:{snapshotSize}字节)", 
                EditorStyles.miniLabel
            );

            EditorGUILayout.EndVertical();
        }

        private List<AbstractTimeRewindObject> FilterObjects()
        {
            var filtered = _allRewindObjects.AsEnumerable();

            // 搜索过滤
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                filtered = filtered.Where(obj => obj.name.ToLower().Contains(_searchFilter.ToLower()));
            }

            // ✅ 修复: 仅显示自定义配置 (检查配置值而非实际值)
            if (_showOnlyCustom)
            {
                filtered = filtered.Where(obj =>
                {
                    SerializedObject so = new SerializedObject(obj);
                    var propSeconds = so.FindProperty("recordSecondsConfig");
                    var propFPS = so.FindProperty("recordFPSConfig");
                    
                    int configSeconds = propSeconds != null ? propSeconds.intValue : 0;
                    int configFPS = propFPS != null ? propFPS.intValue : 0;
                    
                    return configSeconds != 0 || configFPS != 0;
                });
            }

            return filtered.ToList();
        }

        #endregion

        #region 批量应用
        private void ApplyToAllObjects()
        {
            if (!EditorUtility.DisplayDialog("确认操作",
                $"将对 {_allRewindObjects.Count} 个物体应用以下配置:\n" +
                $"录制时长: {_batchRecordSeconds}秒\n" +
                $"录制帧率: {_batchRecordFPS} FPS\n" +
                $"回溯速度: {_batchRewindSpeed}x\n\n" +
                "此操作可撤销。",
                "确认", "取消"))
            {
                return;
            }

            Undo.RecordObjects(_allRewindObjects.ToArray(), "批量修改回溯配置");

            foreach (var obj in _allRewindObjects)
            {
                if (obj == null) continue;
                SetRecordSeconds(obj, _batchRecordSeconds);
                SetRecordFPS(obj, _batchRecordFPS);
                SetRewindSpeed(obj, _batchRewindSpeed);
                EditorUtility.SetDirty(obj);
            }

            CalculateMemoryUsage();
            Debug.Log($"[TimeRewindConfigEditor] 已对 {_allRewindObjects.Count} 个物体应用配置");
        }

        private void ApplyToSelectedObjects()
        {
            var selectedObjects = _allRewindObjects
                .Where(obj => Selection.gameObjects.Contains(obj.gameObject))
                .ToList();

            if (selectedObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选中需要修改的物体", "确定");
                return;
            }

            Undo.RecordObjects(selectedObjects.ToArray(), "批量修改选中物体回溯配置");

            foreach (var obj in selectedObjects)
            {
                SetRecordSeconds(obj, _batchRecordSeconds);
                SetRecordFPS(obj, _batchRecordFPS);
                SetRewindSpeed(obj, _batchRewindSpeed);
                EditorUtility.SetDirty(obj);
            }

            CalculateMemoryUsage();
            Debug.Log($"[TimeRewindConfigEditor] 已对 {selectedObjects.Count} 个选中物体应用配置");
        }
        #endregion

        #region 序列化属性访问 (修复版)

// ✅ 硬编码默认值,与运行时逻辑保持一致
private const int DEFAULT_RECORD_SECONDS = 20;
private const int DEFAULT_RECORD_FPS = 50; // 假设 Time.fixedDeltaTime = 0.02

private int GetRecordSeconds(AbstractTimeRewindObject obj)
{
    SerializedObject so = new SerializedObject(obj);
    var prop = so.FindProperty("recordSecondsConfig");
    int configValue = prop != null ? prop.intValue : 0;
    
    return configValue == 0 ? DEFAULT_RECORD_SECONDS : configValue;
}

private void SetRecordSeconds(AbstractTimeRewindObject obj, int value)
{
    SerializedObject so = new SerializedObject(obj);
    var prop = so.FindProperty("recordSecondsConfig");
    if (prop != null)
    {
        prop.intValue = value;
        so.ApplyModifiedProperties();
    }
}

private int GetRecordFPS(AbstractTimeRewindObject obj)
{
    SerializedObject so = new SerializedObject(obj);
    var prop = so.FindProperty("recordFPSConfig");
    int configValue = prop != null ? prop.intValue : 0;
    
    return configValue == 0 ? DEFAULT_RECORD_FPS : configValue;
}

private void SetRecordFPS(AbstractTimeRewindObject obj, int value)
{
    SerializedObject so = new SerializedObject(obj);
    var prop = so.FindProperty("recordFPSConfig");
    if (prop != null)
    {
        prop.intValue = value;
        so.ApplyModifiedProperties();
    }
}

private float GetRewindSpeed(AbstractTimeRewindObject obj)
{
    SerializedObject so = new SerializedObject(obj);
    var prop = so.FindProperty("rewindSpeedConfig");
    return prop != null ? prop.floatValue : 2f;
}

private void SetRewindSpeed(AbstractTimeRewindObject obj, float value)
{
    SerializedObject so = new SerializedObject(obj);
    var prop = so.FindProperty("rewindSpeedConfig");
    if (prop != null)
    {
        prop.floatValue = value;
        so.ApplyModifiedProperties();
    }
}

        /// <summary>
        /// 格式化字节数为可读字符串
        /// </summary>
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F2} KB";
            return $"{bytes / (1024f * 1024f):F2} MB";
        }

        /// <summary>
        /// 检查类型是否继承自指定基类名
        /// </summary>
        private bool IsSubclassOf(System.Type type, string baseClassName)
        {
            while (type != null && type != typeof(MonoBehaviour))
            {
                if (type.Name == baseClassName)
                    return true;
                type = type.BaseType;
            }
            return false;
        }

#endregion
    }
}
#endif