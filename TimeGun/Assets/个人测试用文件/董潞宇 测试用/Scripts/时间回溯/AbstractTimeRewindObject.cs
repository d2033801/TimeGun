using System;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;
using Utility;

namespace TimeGun
{ 
    public abstract class AbstractTimeRewindObject : MonoBehaviour
    {
        [Header("Config")]
        [Min(0), SerializeField, Tooltip("录制时长, 0为默认值6秒")]private int recordSecondsConfig = 0;
        [Min(0), SerializeField, Tooltip("每秒录制帧率(0为默认值)")] private int recordFPSConfig = 0;

        private int recordSeconds => recordSecondsConfig == 0 ? 6 : recordSecondsConfig;
        private int recordFPS => recordFPSConfig == 0 ? (int)(1f / Time.deltaTime) : recordFPSConfig;

        RingBuffer<TransformValuesSnapshot> TransformHistory;
        float recordInterval;
        float timer;
        bool isRewinding = false;
        private float interval;
        
        public struct TransformValuesSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

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

        protected virtual void MainInit()
        {
            TransformHistory = RewindInit<TransformValuesSnapshot>(out _);
        }

        public AbstractTimeRewindObject()
        {

        }

        public void RewindTransform()
        {

        }


    }

}

