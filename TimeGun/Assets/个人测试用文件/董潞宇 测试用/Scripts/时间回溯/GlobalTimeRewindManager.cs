using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TimeRewind
{
    /// <summary>
    /// 全局时间回溯管理器（Unity 6.2 + Input System + URP 优化版本）
    /// 功能：
    /// - 使用新 Input System 处理输入（支持手柄/键盘/鼠标）
    /// - 自动追踪场景中所有可回溯物体
    /// - 支持按键触发全局回溯 + 自动触发
    /// - 可选：URP Volume 后处理特效（时间扭曲视觉反馈）
    /// </summary>
    [AddComponentMenu("TimeRewind/Global Time Rewind Manager")]
    public class GlobalTimeRewindManager : MonoBehaviour
    {
        #region 全局事件
        /// <summary>
        /// 全局回溯开始事件（任何方式触发全局回溯时触发）
        /// </summary>
        public static event Action OnGlobalRewindStarted;

        /// <summary>
        /// 全局回溯结束事件（全局回溯停止时触发）
        /// </summary>
        public static event Action OnGlobalRewindStopped;
        #endregion

        #region 单例模式
        private static GlobalTimeRewindManager _instance;
        public static GlobalTimeRewindManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GlobalTimeRewindManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("GlobalTimeRewindManager");
                        _instance = go.AddComponent<GlobalTimeRewindManager>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // ✅ 移除：作为关卡工具，随场景生命周期管理，不需要DontDestroyOnLoad

            // 缓存输入动作
            _globalRewindAction = globalRewindAction?.action;

            // ✅ 订阅全局事件
            AbstractTimeRewindObject.OnAnyObjectStoppedRewind += OnTrackedObjectStoppedRewind;
            Debug.Log("[GlobalTimeRewindManager] 已订阅 OnAnyObjectStoppedRewind 事件");
        }

        private void OnDestroy()
        {
            StopGlobalRewind();
            CleanupRewindVolume();

            // ✅ 取消订阅（防止内存泄漏）
            AbstractTimeRewindObject.OnAnyObjectStoppedRewind -= OnTrackedObjectStoppedRewind;
            
            // ✅ 新增：清空单例引用，允许下一个场景重新创建
            if (_instance == this)
            {
                _instance = null;
            }
            
            Debug.Log("[GlobalTimeRewindManager] 已取消订阅事件并清空单例引用");
        }
        #endregion

        #region 配置参数
        [Header("输入配置 (Input System)")]
        [Tooltip("全局回溯输入（推荐使用 Button 类型，支持 Hold）")]
        public InputActionReference globalRewindAction;

        [Header("回溯配置")]
        [Tooltip("全局回溯速度倍率")]
        [Range(0.1f, 10f)]
        public float globalRewindSpeed = 2f;

        [Header("自动触发配置")]
        [Tooltip("是否启用自动触发")]
        public bool enableAutoTrigger = false;

        [Tooltip("游戏开始后多少秒自动触发回溯")]
        [Min(0)]
        public float autoTriggerTime = 10f;

        [Tooltip("自动触发的回溯持续时长（秒）")]
        [Min(0)]
        public float autoTriggerDuration = 3f;

        [Header("URP 特效（可选）")]
        [Tooltip("回溯时的 Volume Profile（用于后处理特效，如色差/扭曲）")]
        public VolumeProfile rewindVolumeProfile;

        [Tooltip("特效强度过渡速度")]
        [Range(0.1f, 10f)]
        public float volumeBlendSpeed = 5f;

        [Header("调试信息")]
        [SerializeField, Tooltip("当前追踪的可回溯物体数量")]
        private int trackedObjectsCount = 0;

        [SerializeField, Tooltip("当前是否正在全局回溯")]
        private bool isGlobalRewinding = false;
        #endregion

        #region 内部状态
        /// <summary>
        /// 追踪的所有可回溯物体
        /// </summary>
        private HashSet<AbstractTimeRewindObject> _trackedObjects = new HashSet<AbstractTimeRewindObject>();

        // ✅ 新增：追踪正在回溯的物体（用于快速判断是否所有物体都已停止）
        private HashSet<AbstractTimeRewindObject> _activeRewindingObjects = new HashSet<AbstractTimeRewindObject>();

        /// <summary>
        /// 缓存的输入动作
        /// </summary>
        private InputAction _globalRewindAction;

        /// <summary>
        /// 自动触发计时器
        /// </summary>
        private float _autoTriggerTimer = 0f;

        /// <summary>
        /// 是否已自动触发过
        /// </summary>
        private bool _hasAutoTriggered = false;

        /// <summary>
        /// URP Volume 实例（用于后处理特效）
        /// </summary>
        private Volume _rewindVolume;

        /// <summary>
        /// 当前 Volume 权重（用于平滑过渡）
        /// </summary>
        private float _currentVolumeWeight = 0f;
        #endregion

        #region 初始化与更新
        private void Start()
        {
            RefreshTrackedObjects();
            InitializeRewindVolume();
        }

        private void OnEnable()
        {
            // 启用输入动作
            if (_globalRewindAction != null && !_globalRewindAction.enabled)
            {
                _globalRewindAction.Enable();
            }
        }

        private void OnDisable()
        {
            // 禁用输入动作
            if (_globalRewindAction != null && _globalRewindAction.enabled)
            {
                _globalRewindAction.Disable();
            }
        }

        private void Update()
        {
            HandleManualTrigger();
            HandleAutoTrigger();
            UpdateVolumeEffect();
        }

        /// <summary>
        /// 当任意物体停止回溯时触发（立即停止全局回溯）
        /// </summary>
        private void OnTrackedObjectStoppedRewind(AbstractTimeRewindObject stoppedObject)
        {
            Debug.Log($"[GlobalTimeRewindManager] 收到物体停止回溯事件: {stoppedObject.name}");

            // 如果不在全局回溯模式，忽略
            if (!isGlobalRewinding) 
            {
                Debug.Log($"[GlobalTimeRewindManager] 不在全局回溯模式，忽略此事件");
                return;
            }

            // ✅ 修复：只要有一个物体停止，立即停止全局回溯
            Debug.Log($"[GlobalTimeRewindManager] ✅ 物体 {stoppedObject.name} 历史耗尽，立即停止全局回溯");
            
            isGlobalRewinding = false;
            
            // 停止所有其他正在回溯的物体
            var objectsToStop = new List<AbstractTimeRewindObject>(_activeRewindingObjects);
            foreach (var obj in objectsToStop)
            {
                if (obj != null && obj != stoppedObject)
                {
                    obj.StopRewind();
                }
            }
            
            _activeRewindingObjects.Clear();
            
            // 触发全局停止事件
            OnGlobalRewindStopped?.Invoke();
        }
        #endregion

        #region 对象追踪管理
        /// <summary>
        /// 刷新追踪列表（扫描场景中所有 AbstractTimeRewindObject）
        /// </summary>
        public void RefreshTrackedObjects()
        {
            _trackedObjects.Clear();
            var allRewindObjects = FindObjectsByType<AbstractTimeRewindObject>(FindObjectsSortMode.None);
            foreach (var obj in allRewindObjects)
            {
                if (obj != null)
                {
                    _trackedObjects.Add(obj);
                }
            }
            trackedObjectsCount = _trackedObjects.Count;
            Debug.Log($"[GlobalTimeRewindManager] 刷新追踪列表，当前追踪 {trackedObjectsCount} 个可回溯物体");
        }

        /// <summary>
        /// 手动注册可回溯物体
        /// </summary>
        public void RegisterObject(AbstractTimeRewindObject obj)
        {
            if (obj != null && _trackedObjects.Add(obj))
            {
                trackedObjectsCount = _trackedObjects.Count;
                Debug.Log($"[GlobalTimeRewindManager] 注册物体: {obj.name}");
            }
        }

        /// <summary>
        /// 手动注销可回溯物体
        /// </summary>
        public void UnregisterObject(AbstractTimeRewindObject obj)
        {
            if (obj != null && _trackedObjects.Remove(obj))
            {
                trackedObjectsCount = _trackedObjects.Count;
                Debug.Log($"[GlobalTimeRewindManager] 注销物体: {obj.name}");
            }
        }
        #endregion

        #region 手动触发逻辑
        /// <summary>
        /// 处理手动按键触发全局回溯（Input System）
        /// </summary>
        private void HandleManualTrigger()
        {
            if (_globalRewindAction == null) return;

            // 按下 → 启动全局回溯
            if (_globalRewindAction.WasPressedThisFrame())
            {
                StartGlobalRewind(globalRewindSpeed);
            }

            // 松开 → 停止全局回溯
            if (_globalRewindAction.WasReleasedThisFrame())
            {
                StopGlobalRewind();
            }
        }
        #endregion

        #region 自动触发逻辑
        /// <summary>
        /// 处理自动触发全局回溯
        /// </summary>
        private void HandleAutoTrigger()
        {
            if (!enableAutoTrigger || _hasAutoTriggered) return;

            _autoTriggerTimer += Time.deltaTime;

            if (_autoTriggerTimer >= autoTriggerTime)
            {
                _hasAutoTriggered = true;
                Debug.Log($"[GlobalTimeRewindManager] 自动触发全局回溯，持续 {autoTriggerDuration} 秒");
                StartGlobalRewindByTime(autoTriggerDuration, globalRewindSpeed);
            }
        }

        /// <summary>
        /// 重置自动触发状态
        /// </summary>
        public void ResetAutoTrigger()
        {
            _autoTriggerTimer = 0f;
            _hasAutoTriggered = false;
            Debug.Log("[GlobalTimeRewindManager] 已重置自动触发计时器");
        }
        #endregion

        #region 全局回溯控制 API
        /// <summary>
        /// 启动全局回溯（持续按住模式）
        /// </summary>
        public void StartGlobalRewind(float? speed = null)
        {
            if (isGlobalRewinding)
            {
                Debug.LogWarning("[GlobalTimeRewindManager] 全局回溯已在进行中");
                return;
            }

            isGlobalRewinding = true;
            float rewindSpeed = speed ?? globalRewindSpeed;

            // 清理已销毁的物体
            _trackedObjects.RemoveWhere(obj => obj == null);
            trackedObjectsCount = _trackedObjects.Count;

            // ✅ 新增：清空旧记录
            _activeRewindingObjects.Clear();

            Debug.Log($"[GlobalTimeRewindManager] 启动全局回溯，影响 {trackedObjectsCount} 个物体，速度 x{rewindSpeed}");

            foreach (var obj in _trackedObjects)
            {
                if (obj != null)
                {
                    obj.StartRewind(rewindSpeed);
                    // ✅ 新增：记录正在回溯的物体
                    _activeRewindingObjects.Add(obj);
                }
            }

            // ✅ 新增：触发全局事件
            OnGlobalRewindStarted?.Invoke();
        }

        /// <summary>
        /// 停止全局回溯
        /// </summary>
        public void StopGlobalRewind()
        {
            if (!isGlobalRewinding) return;

            isGlobalRewinding = false;

            Debug.Log($"[GlobalTimeRewindManager] 停止全局回溯");

            // ✅ 修改：复制集合避免迭代时修改
            var objectsToStop = new List<AbstractTimeRewindObject>(_activeRewindingObjects);

            foreach (var obj in objectsToStop)
            {
                if (obj != null)
                {
                    obj.StopRewind(); // 会触发事件，自动从 _activeRewindingObjects 移除
                }
            }

            _activeRewindingObjects.Clear();

            // ✅ 新增：触发全局事件
            OnGlobalRewindStopped?.Invoke();
        }

        /// <summary>
        /// 全局回溯指定秒数（瞬间回溯）
        /// </summary>
        public void GlobalRewindBySeconds(float seconds)
        {
            if (seconds <= 0f)
            {
                Debug.LogWarning("[GlobalTimeRewindManager] 回溯秒数必须大于0");
                return;
            }

            _trackedObjects.RemoveWhere(obj => obj == null);
            trackedObjectsCount = _trackedObjects.Count;

            Debug.Log($"[GlobalTimeRewindManager] 全局瞬间回溯 {seconds} 秒");

            foreach (var obj in _trackedObjects)
            {
                if (obj != null)
                {
                    obj.RewindBySeconds(seconds);
                }
            }
        }

        /// <summary>
        /// 全局限时回溯（自动停止）
        /// </summary>
        public void StartGlobalRewindByTime(float duration, float? speed = null)
        {
            if (duration <= 0f)
            {
                Debug.LogWarning("[GlobalTimeRewindManager] 回溯时长必须大于0");
                return;
            }

            // ✅ 修复：如果已经在全局回溯中，先停止
            if (isGlobalRewinding)
            {
                Debug.LogWarning("[GlobalTimeRewindManager] 全局回溯已在进行中，先停止旧回溯");
                StopGlobalRewind();
            }

            // ✅ 修复：设置全局回溯状态
            isGlobalRewinding = true;
            float rewindSpeed = speed ?? globalRewindSpeed;

            _trackedObjects.RemoveWhere(obj => obj == null);
            trackedObjectsCount = _trackedObjects.Count;

            // ✅ 修复：清空旧记录
            _activeRewindingObjects.Clear();

            Debug.Log($"[GlobalTimeRewindManager] 启动全局限时回溯 {duration} 秒，速度 x{rewindSpeed}，影响 {trackedObjectsCount} 个物体");

            foreach (var obj in _trackedObjects)
            {
                if (obj != null)
                {
                    obj.StartRewindByTime(duration, rewindSpeed);
                    // ✅ 修复：记录正在回溯的物体
                    _activeRewindingObjects.Add(obj);
                }
            }

            // ✅ 修复：触发全局事件
            OnGlobalRewindStarted?.Invoke();
        }
        #endregion

        #region URP Volume 特效
        /// <summary>
        /// 初始化 URP Volume（用于回溯后处理特效）
        /// </summary>
        private void InitializeRewindVolume()
        {
            if (rewindVolumeProfile == null) return;

            // 创建 Volume GameObject
            var volumeGO = new GameObject("GlobalRewindVolume");
            volumeGO.transform.SetParent(transform);
            _rewindVolume = volumeGO.AddComponent<Volume>();

            _rewindVolume.isGlobal = true;
            _rewindVolume.priority = 100; // 高优先级，覆盖默认后处理
            _rewindVolume.profile = rewindVolumeProfile;
            _rewindVolume.weight = 0f; // 初始为0（无效果）

            Debug.Log("[GlobalTimeRewindManager] 已初始化 URP Volume 特效");
        }

        /// <summary>
        /// 更新 Volume 特效权重（平滑过渡）
        /// </summary>
        private void UpdateVolumeEffect()
        {
            if (_rewindVolume == null) return;

            float targetWeight = isGlobalRewinding ? 1f : 0f;
            _currentVolumeWeight = Mathf.Lerp(_currentVolumeWeight, targetWeight, Time.deltaTime * volumeBlendSpeed);
            _rewindVolume.weight = _currentVolumeWeight;
        }

        /// <summary>
        /// 清理 Volume 实例
        /// </summary>
        private void CleanupRewindVolume()
        {
            if (_rewindVolume != null)
            {
                Destroy(_rewindVolume.gameObject);
                _rewindVolume = null;
            }
        }
        #endregion

        #region 状态查询
        /// <summary>
        /// 获取当前是否正在全局回溯
        /// </summary>
        public bool IsGlobalRewinding => isGlobalRewinding;

        /// <summary>
        /// 获取当前追踪的物体数量
        /// </summary>
        public int TrackedObjectsCount => trackedObjectsCount;
        #endregion
    }
}