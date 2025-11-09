using UnityEngine;
using TimeRewind;
using Utility;

namespace TimeGun
{
    /// <summary>
    /// 可回溯的掉落物体（Unity 6.2 + 时间回溯系统集成）
    /// 
    /// 功能：
    /// - 在指定倒计时后从天花板掉落（通过启用重力实现）
    /// - 倒计时可被时间回溯（内置计时器也会随历史快照回溯）
    /// - 支持手动触发掉落
    /// - 提供掉落状态查询接口
    /// 
    /// 使用场景：
    /// - 悬挂在天花板的物体（初始禁用重力，到达指定时间后自由落体）
    /// - 时间谜题（玩家需要通过回溯时间来重置掉落物体）
    /// - 环境互动（例如定时炸弹、悬挂陷阱等）
    /// 
    /// 配置建议：
    /// 1. 初始状态：禁用 useGravity，可选 isKinematic=true（防止物理抖动）
    /// 2. 掉落时：启用 useGravity，禁用 isKinematic（使物体受重力影响）
    /// 3. 回溯时：恢复到初始状态（通过快照系统自动处理）
    /// </summary>
    [AddComponentMenu("TimeRewind/Rewindable Falling Object")]
    [RequireComponent(typeof(Rigidbody))]
    public class RewindableFallingObject : AbstractTimeRewindRigidBody
    {
        #region 检视器参数
        [Header("掉落配置")]
        [Tooltip("掉落倒计时（秒），0表示不自动掉落")]
        [SerializeField, Min(0)] private float dropDelay = 5f;

        [Tooltip("是否在游戏开始时自动启动倒计时")]
        [SerializeField] private bool autoStartTimer = true;

        [Tooltip("掉落碰撞时播放的音效")]
        [SerializeField] private AudioClip dropSound;

        [Tooltip("掉落音效音量"), Range(0f, 1f)]
        [SerializeField] private float dropSoundVolume = 0.5f;

        [Header("物理状态（运行时）")]
        [Tooltip("初始重力状态（自动从Rigidbody读取）")]
        [SerializeField, ReadOnly] private bool initialUseGravity;

        [Tooltip("初始Kinematic状态（自动从Rigidbody读取）")]
        [SerializeField, ReadOnly] private bool initialIsKinematic;

        [Header("调试信息")]
        [SerializeField, ReadOnly] private bool hasFallen = false;
        [SerializeField, ReadOnly] private float currentTimer = 0f;
        [SerializeField, ReadOnly] private bool hasPlayedDropSound = false; // ✅ 新增：是否已播放掉落音效
        #endregion

        #region 公共接口
        /// <summary>
        /// 是否已经掉落（只读）
        /// </summary>
        public bool HasFallen => hasFallen;

        /// <summary>
        /// 当前倒计时剩余时间（只读）
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, dropDelay - currentTimer);

        /// <summary>
        /// 手动触发掉落（立即掉落，忽略倒计时）
        /// </summary>
        public void TriggerDrop()
        {
            if (hasFallen) return;

            hasFallen = true;
            
            // 使用父类的protected属性rb（需要在父类中将rb改为protected）
            var rigidbody = GetComponent<Rigidbody>();
            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;

            // ✅ 移除：不再在掉落时播放音效
            // PlayDropSound();
        }

        /// <summary>
        /// 重置物体到初始状态（取消掉落，重置计时器）
        /// </summary>
        public void ResetToInitialState()
        {
            hasFallen = false;
            currentTimer = 0f;
            hasPlayedDropSound = false; // ✅ 新增：重置音效状态
            
            var rigidbody = GetComponent<Rigidbody>();
            rigidbody.useGravity = initialUseGravity;
            rigidbody.isKinematic = initialIsKinematic;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
        #endregion

        #region 内部状态
        /// <summary>
        /// 掉落状态快照历史（用于回溯）
        /// </summary>
        private RingBuffer<DropStateSnapshot> _dropStateHistory;

        /// <summary>
        /// 掉落状态快照结构
        /// </summary>
        private struct DropStateSnapshot
        {
            public bool HasFallen;
            public float Timer;
            public bool UseGravity;
            public bool IsKinematic;
            public bool HasPlayedDropSound; // ✅ 新增：记录音效状态
        }
        #endregion

        #region 初始化
        protected override void MainInit()
        {
            base.MainInit();

            // 保存初始物理状态
            var rigidbody = GetComponent<Rigidbody>();
            initialUseGravity = rigidbody.useGravity;
            initialIsKinematic = rigidbody.isKinematic;

            // 初始化掉落状态历史
            _dropStateHistory = RewindInit<DropStateSnapshot>(out _);

            // 如果启用自动计时，则确保初始状态为"未掉落"
            if (autoStartTimer)
            {
                hasFallen = false;
                currentTimer = 0f;
                hasPlayedDropSound = false; // ✅ 新增：初始化音效状态
            }
        }
        #endregion

        #region 更新逻辑
        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            // 仅在正常录制模式下更新计时器
            if (!isRewinding && !IsPaused && autoStartTimer && !hasFallen)
            {
                currentTimer += Time.fixedDeltaTime;

                // 达到掉落时间
                if (currentTimer >= dropDelay && dropDelay > 0f)
                {
                    TriggerDrop();
                }
            }
        }
        #endregion

        #region 快照记录与回溯（重写父类方法）
        protected override void RecordOneSnap()
        {
            base.RecordOneSnap();

            var rigidbody = GetComponent<Rigidbody>();
            var snap = new DropStateSnapshot
            {
                HasFallen = hasFallen,
                Timer = currentTimer,
                UseGravity = rigidbody.useGravity,
                IsKinematic = rigidbody.isKinematic,
                HasPlayedDropSound = hasPlayedDropSound // ✅ 新增：记录音效状态
            };
            _dropStateHistory.Push(snap);
        }

        protected override void RewindOneSnap()
        {
            base.RewindOneSnap();

            var snap = _dropStateHistory.PopBack();
            hasFallen = snap.HasFallen;
            currentTimer = snap.Timer;
            hasPlayedDropSound = snap.HasPlayedDropSound; // ✅ 新增：恢复音效状态
            
            var rigidbody = GetComponent<Rigidbody>();
            rigidbody.useGravity = snap.UseGravity;
            rigidbody.isKinematic = snap.IsKinematic;
        }
        #endregion

        #region ✅ 新增：碰撞检测（第一次碰撞时播放音效）
        /// <summary>
        /// 碰撞时触发（第一次碰到物体时播放掉落音效）
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            // 仅在已掉落且未播放过音效的情况下播放
            if (hasFallen && !hasPlayedDropSound)
            {
                hasPlayedDropSound = true;
                PlayDropSound(collision.GetContact(0).point);
            }
        }
        #endregion

        #region 音效
        /// <summary>
        /// 在指定位置播放掉落音效
        /// </summary>
        private void PlayDropSound(Vector3 position)
        {
            if (dropSound != null)
            {
                AudioSource.PlayClipAtPoint(dropSound, position, dropSoundVolume);
            }
        }
        #endregion
    }

    #region 只读属性特性（用于Inspector显示）
    /// <summary>
    /// Inspector中只读显示的属性特性（仅用于调试，不影响实际逻辑）
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
    #endregion
}
