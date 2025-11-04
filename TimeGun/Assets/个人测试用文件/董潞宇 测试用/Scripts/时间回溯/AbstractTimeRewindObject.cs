using System;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEditor;
using UnityEngine;
using Utility;


namespace TimeRewind
{
    /// <summary>
    /// 作为所有可时停物体的基抽象类, 提供时停功能的基本框架。
    /// </summary>
    /// TODO: 但其实我也没想明白这个类该不该抽象化，到底把哪些抽象化
    public abstract class AbstractTimeRewindObject : MonoBehaviour
    {
        #region 显示在检视器上的数据
        [Header("Config")]
        [Min(0), SerializeField, Tooltip("录制时长, 0为默认值20秒")]private int recordSecondsConfig = 0;
        [Min(0), SerializeField, Tooltip("每秒录制帧率(0为默认值)")] private int recordFPSConfig = 0;
        [Range(0,10), SerializeField, Tooltip("回溯速度倍率")] private float rewindSpeedConfig = 2;

        [Header("特效 Effects (可选)")]
        [Tooltip("回溯时播放的特效预制件"), SerializeField] 
        private GameObject rewindEffectPrefab;
        
        [Tooltip("暂停时播放的特效预制件"), SerializeField] 
        private GameObject pauseEffectPrefab;

        [Tooltip("特效生成点（留空则使用物体自身位置）"), SerializeField]
        private Transform effectSpawnPoint;
        #endregion
        
        #region 内部数据与结构体
        // 最大录制时长, 若为0则使用默认20秒
        protected int recordSeconds => recordSecondsConfig == 0 ? 20 : recordSecondsConfig;

        // 每秒录制帧率, 若为0则使用默认1/Time.deltaTime
        protected int recordFPS => recordFPSConfig == 0 ? (int)(1f / Time.deltaTime) : recordFPSConfig;

        // 回溯速度倍率
        private float? _runtimeRewindSpeed = null; // 这是个运行时可空的回溯速度, 若不为null则使用该值
        protected float rewindSpeed => Mathf.Max(0f, _runtimeRewindSpeed ?? rewindSpeedConfig);
        

        RingBuffer<TransformValuesSnapshot> transformHistory;           // transform组件值的记录
        float recordInterval;                                           // 每次记录间隔

        /// <summary>
        /// 回溯进度计时器（秒）。在回放模式下按 <see cref="Time.fixedDeltaTime"/> 与 <see cref="rewindSpeed"/> 累加，
        /// 用于根据 <see cref="recordInterval"/> 逐步执行回溯。
        /// </summary>
        float rewindTimer = 0f;

        /// <summary>
        /// 是否处于回溯状态（true 表示正在回放历史快照，false 表示正常录制）。
        /// </summary>
        protected bool isRewinding = false;

        /// <summary>
        /// 是否处于录制状态（true 表示正在录制历史快照，false 表示暂停录制）。
        /// </summary>
        private bool isRecording = true;

        /// <summary>
        /// 是否处于暂停状态（独立于回溯系统，用于冻结物体行为)
        /// </summary>
        private bool isPaused = false;

        /// <summary>
        /// 录制进度计时器（秒）。在录制模式下按 <see cref="Time.fixedDeltaTime"/> 累加，
        /// 用于当累计时间达到 <see cref="recordInterval"/> 时触发一次采样。
        /// </summary>
        float recordTimer = 0f;

        /// <summary>
        /// 由于正常来说每个历史文件中的帧数是固定的, 所以这里用一个计数器记录当前剩了多少帧, 以便实现复用代码
        /// 子类或许也可以改写这个？
        /// </summary>
        protected int frameCount => transformHistory?.Count ?? 0;

        /// <summary>
        /// 当前正在播放的回溯特效实例
        /// </summary>
        private GameObject _activeRewindEffect;

        /// <summary>
        /// 当前正在播放的暂停特效实例
        /// </summary>
        private GameObject _activePauseEffect;

        /// <summary>
        /// 特效生成点（若用户未配置则使用物体自身）
        /// </summary>
        private Transform EffectSpawnPoint => effectSpawnPoint != null ? effectSpawnPoint : transform;

        /// <summary>
        /// 用于保存 Transform 的快照数据（位置、旋转与缩放）。
        /// </summary>
        public struct TransformValuesSnapshot
        {
            /// <summary>世界空间中的位置（对应 <see cref="Transform.position"/>）。</summary>
            public Vector3 Position;

            /// <summary>世界空间中的旋转（对应 <see cref="Transform.rotation"/>）。</summary>
            public Quaternion Rotation;

            /// <summary>本地缩放（对应 <see cref="Transform.localScale"/>）。</summary>
            public Vector3 Scale;
        }
        #endregion
        #region 初始化
        void Awake()
        {

            MainInit();

        }

        /// <summary>
        /// 初始化一个新的 <see cref="RingBuffer{T}"/> 用于以指定间隔录制数据。
        /// </summary>
        /// <remarks>录制间隔是根据录制的每秒帧数（FPS）的倒数来计算的。缓冲区的大小
        /// 由录制的每秒帧数（FPS）和录制持续时间（秒）的乘积决定，确保至少存储一帧。</remarks>
        /// <typeparam name="T">存储在循环缓冲区中的元素的类型。</typeparam>
        /// <param name="interval">当方法返回时，包含每帧录制之间的时间间隔（秒）。</param>
        /// <returns>一个已配置的 <see cref="RingBuffer{T}"/> 实例，可以存储最大数量的帧，
        /// 基于录制的每秒帧数（FPS）和持续时间（秒）。</returns>
        protected RingBuffer<T> RewindInit<T>(out float interval)
        {
            int fps = Mathf.Max(1, recordFPS);
            int seconds = Mathf.Max(1, recordSeconds);
            int maxFrames = Mathf.Max(1, Mathf.RoundToInt(seconds * fps));

            interval = 1f / fps;                        //每次记录间隔
            return new RingBuffer<T>(maxFrames);
        }

        /// <summary>
        /// 实现主要初始化逻辑。注意子类在重写此方法时应注意调用基类实现。
        /// </summary>
        protected virtual void MainInit()
        {
            transformHistory = RewindInit<TransformValuesSnapshot>(out recordInterval);
        }
        #endregion

        #region 主记录/回溯方法
        protected virtual void FixedUpdate()
        {
            // ✅ 暂停时既不录制也不回溯
            if (isPaused) return;
            
            if (isRewinding)
            {
                RewindFixedStep();
            }
            else if(isRecording)
            {
                RecordFixedStep();
            }
        }


        /// <summary>
        /// 记录一个快照到历史记录中。
        /// </summary>
        protected virtual void RecordFixedStep()
        {
            recordTimer += Time.fixedDeltaTime;
            while (recordTimer >= recordInterval)
            {
                recordTimer -= recordInterval;
                RecordOneSnap();
            }
        }

        /// <summary>
        /// 记录一个快照, 子类可重写以记录更多数据, 注意要调用基类方法记录Transform数据
        /// </summary>
        protected virtual void RecordOneSnap()
        {
            var snap = new TransformValuesSnapshot
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.localScale
            };
            transformHistory.Push(snap);
        }

        /// <summary>
        /// 回溯一次固定步长
        /// </summary>
        protected virtual void RewindFixedStep()
        {
            // 用 fixedDeltaTime * rewindSpeed 推进回溯进度，可能一帧回退多步
            rewindTimer += Time.fixedDeltaTime * rewindSpeed;
            while (rewindTimer >= recordInterval)
            {
                rewindTimer -= recordInterval;

                if (frameCount == 0)
                {
                    // 没历史了，停止回放
                    StopRewind();
                    return;
                }

                RewindOneSnap();

                // 子类可在 Pop 后做额外处理（例如 velocity 回写）
                OnAppliedSnapshotDuringRewind();
            }
        }

        /// <summary>
        /// 记录一个快照, 子类可重写以记录更多数据, 注意要调用基类方法记录Transform数据
        /// </summary>
        protected virtual void RewindOneSnap()
        {
            // 取出最新快照并应用，然后 pop
            var snap = transformHistory.PopBack();
            transform.position = snap.Position;
            transform.rotation = snap.Rotation;
            transform.localScale = snap.Scale;
        }

        #endregion

        #region 子类扩展
        /// <summary>
        /// 子类扩展, 在回溯开始时触发, 若需要则改写
        /// </summary>
        /// <remarks>
        /// 如在RigidBody中设为kinematic
        /// </remarks>
        protected virtual void OnStartRewind()
        {
        }

        /// <summary>
        /// 子类扩展, 在回溯结束时触发, 若需要则改写
        /// </summary>
        protected virtual void OnStopRewind()
        {

        }


        /// <summary>
        /// 子类扩展, 在暂停开始时触发
        /// </summary>
        /// <remarks>
        /// 暂停时应冻结所有影响物体行为的组件(如Rigidbody、NavMeshAgent、Animator等)。
        /// 示例：rb.isKinematic = true; agent.isStopped = true; animator.speed = 0;
        /// </remarks>
        protected virtual void OnStartPause()
        {
        }

        /// <summary>
        /// 子类扩展, 在暂停结束时触发
        /// </summary>
        /// <remarks>
        /// 应恢复OnStartPause中冻结的所有组件状态。
        /// 示例：rb.isKinematic = originalValue; agent.isStopped = false; animator.speed = 1;
        /// </remarks>
        protected virtual void OnStopPause()
        {
        }


        /// <summary>
        /// 子类在每次应用 snapshot 后可重写处理额外状态
        /// </summary>
        protected virtual void OnAppliedSnapshotDuringRewind() { }

        #endregion

        #region 特效管理

        /// <summary>
        /// 播放回溯特效（在指定生成点创建特效实例）
        /// </summary>
        private void PlayRewindEffect()
        {
            if (rewindEffectPrefab == null) return;

            // 清理已存在的回溯特效
            StopRewindEffect();

            // 在特效生成点创建新的回溯特效
            _activeRewindEffect = Instantiate(
                rewindEffectPrefab, 
                EffectSpawnPoint.position, 
                EffectSpawnPoint.rotation
            );
            
            // 设置父物体为生成点，跟随生成点移动和旋转
            _activeRewindEffect.transform.SetParent(EffectSpawnPoint);
        }

        /// <summary>
        /// 停止回溯特效
        /// </summary>
        private void StopRewindEffect()
        {
            if (_activeRewindEffect != null)
            {
                Destroy(_activeRewindEffect);
                _activeRewindEffect = null;
            }
        }

        /// <summary>
        /// 播放暂停特效（在指定生成点创建特效实例）
        /// </summary>
        private void PlayPauseEffect()
        {
            if (pauseEffectPrefab == null) return;

            // 清理已存在的暂停特效
            StopPauseEffect();

            // 在特效生成点创建新的暂停特效
            _activePauseEffect = Instantiate(
                pauseEffectPrefab, 
                EffectSpawnPoint.position, 
                EffectSpawnPoint.rotation
            );
            
            // 设置父物体为生成点，跟随生成点移动和旋转
            _activePauseEffect.transform.SetParent(EffectSpawnPoint);
        }

        /// <summary>
        /// 停止暂停特效
        /// </summary>
        private void StopPauseEffect()
        {
            if (_activePauseEffect != null)
            {
                Destroy(_activePauseEffect);
                _activePauseEffect = null;
            }
        }

        #endregion

        #region 外部API

        // ========== 外部 API ==========

        public virtual void StartRecord()
        {
            if (isRecording) return;
            isRecording = true;
        }

        public virtual void StopRecord()
        {
            if (!isRecording) return;
            isRecording = false;
            recordTimer = 0f;
        }


        /// <summary>
        /// 供外部调用的方法。如果回溯过程尚未激活，则启动倒带过程。使用默认配置的回溯速度。
        /// </summary>
        /// <remarks>此方法将回溯状态设置为激活，并重置倒带计时器。它还会
        /// 触发 <see cref="OnStartRewind"/> 方法以处理与启动倒带过程相关的任何附加逻辑。</remarks>
        public virtual void StartRewind()
        {
            StartRewind(rewindSpeed);
        }

        /// <summary>
        /// 按照指定速度启动回溯过程的重载方法。如果回溯过程尚未激活，则启动倒带过程。
        /// TODO: 此处耦合了运行时速度修改的功能, 但或许应该拆分出去? 这样会导致每次实际上都在调用 _runtimeRewindSpeed, 并在回溯完毕再修改回去
        /// TODO: 另外, 或许应该加一个指定回溯时长的功能
        /// TODO: 实际上速度只在这里被使用的话, 其实可以尝试直接控制rewindSpeed, 在这里直接赋值就行了
        /// </summary>
        /// <param name="speed">指定回溯速度</param>
        public virtual void StartRewind(float speed)
        {
            if (isRewinding) return;
            _runtimeRewindSpeed = speed;
            isRewinding = true;
            rewindTimer = 0f;
            
            // ✅ 播放回溯特效
            PlayRewindEffect();
            
            OnStartRewind();
        }

        /// <summary>
        /// 供外部调用的方法。如果当前处于激活状态，则停止倒带过程。
        /// </summary>
        /// <remarks>此方法将倒带状态设置为非激活，并触发 <see cref="OnStopRewind"/> 方法。如果倒带未处于激活状态，则该方法不产生任何效果。</remarks>
        public virtual void StopRewind()
        {
            if (!isRewinding) return;
            _runtimeRewindSpeed = null;
            isRewinding = false;
            
            // ✅ 停止回溯特效
            StopRewindEffect();
            
            OnStopRewind();
        }

        /// <summary>
        /// 供外部调用的方法。回溯到指定的秒数。
        /// </summary>
        /// <remarks>
        /// 从最近的历史向后逐帧 pop 
        /// </remarks>
        public virtual void RewindBySeconds(float seconds)
        {
            if (seconds <= 0f || frameCount == 0)
            {
                Debug.Log("回溯秒数小于等于0或记录为空");
                return;
            }
            int frames = Mathf.RoundToInt(seconds / recordInterval);            // 计算需要回溯的帧数
            frames = Mathf.Clamp(frames, 0, frameCount);                  // 限制在已有记录范围内

            bool wasRewinding = isRewinding; // 记录原始状态

            // 这里的核心在于如果已经在渐进回溯中, 则不需要重复调用 OnStartRewind 和 OnStopRewind
            if (!wasRewinding)
            {
                // 如果之前没在回溯，启动回溯状态（冻结 NavMeshAgent 等）
                isRewinding = true;
                
                // ✅ 播放回溯特效
                PlayRewindEffect();
                
                OnStartRewind();
            }

            // TODO: 这里可以优化掉, 直接循环也太弱智了, 应该可以直接跳转到对应帧
                for (int i = 0; i < frames; i++)
            {
                if (frameCount == 0) break;
                RewindOneSnap();
                OnAppliedSnapshotDuringRewind();
            }

            // 这里的核心在于如果已经在渐进回溯中, 则不需要重复调用 OnStartRewind 和 OnStopRewind
            if (!wasRewinding)
            {
                // 如果是我们启动的回溯，恢复状态（解冻 NavMeshAgent 等）
                isRewinding = false;
                
                // ✅ 停止回溯特效
                StopRewindEffect();
                
                OnStopRewind(); // ✅ 关键：调用 StopRewind 恢复组件状态
            }
        }

        /// <summary>
        /// 启动暂停（独立于回溯系统）
        /// </summary>
        /// <remarks>
        /// 暂停时会冻结录制和回溯,但不会消耗历史帧。
        /// 子类可通过重写 OnStartPause 实现额外的冻结逻辑(如冻结NavMeshAgent/Animator/Rigidbody)。
        /// </remarks>
        public virtual void StartPause()
        {
            if (isPaused) return;
            isPaused = true;
            
            // ✅ 播放暂停特效
            PlayPauseEffect();
            
            OnStartPause();
        }

        /// <summary>
        /// 停止暂停,恢复正常状态
        /// </summary>
        public virtual void StopPause()
        {
            if (!isPaused) return;
            isPaused = false;
            
            // ✅ 停止暂停特效
            StopPauseEffect();
            
            OnStopPause();
        }

        /// <summary>
        /// 获取当前是否处于暂停状态
        /// </summary>
        public bool IsPaused => isPaused;

        #endregion

        #region 清理

        private void OnDestroy()
        {
            // 清理特效实例
            StopRewindEffect();
            StopPauseEffect();
        }

        #endregion
    }

}

