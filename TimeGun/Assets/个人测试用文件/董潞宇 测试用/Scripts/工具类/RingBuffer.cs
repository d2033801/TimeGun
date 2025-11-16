using System;
using System.Collections;
using System.Collections.Generic;

namespace Utility
{
    /// <summary>
    /// 固定容量的环形缓冲区，写满后继续写入会覆盖最旧元素。
    /// 支持 Push、按下标访问、PeekBack、PopBack（索引 0 始终表示最旧，Count-1 表示最新）。
    /// </summary>
    /// <typeparam name="T">缓冲区中存储的元素类型。</typeparam>
    /// <remarks>
    /// - 当未写满时：最旧元素位于内部数组下标 0；<br/>
    /// - 当写满时：最旧元素位于内部数组下标 <c>head</c>。
    /// </remarks>
    public class RingBuffer<T> : IEnumerable<T>
    {
        #region 变量区
        /// <summary>
        /// 内部存储数组，长度等于 <see cref="Capacity"/>。
        /// </summary>
        private readonly T[] buffer;

        private int head;               // 下一个写入位置（循环递增，范围 0..Capacity-1）。

        /// <summary>
        /// 当前元素数量，范围 0..Capacity。
        /// </summary>
        public int Count { get; private set; }

        public int Capacity { get; }    //缓冲区容量（最大可存放的元素个数）
        #endregion

        #region 可被外部调用函数
        /// <summary>
        /// 使用指定容量创建环形缓冲区。
        /// </summary>
        /// <param name="capacity">缓冲区容量，必须 > 0</param>
        /// <exception cref="ArgumentException">当 <paramref name="capacity"/> 小于等于 0 时抛出。</exception>
        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentException("capacity must be > 0");
            Capacity = capacity;
            buffer = new T[capacity];
            head = 0;
            Count = 0;
        }


        /// <summary>
        /// 写入元素 (会覆盖最旧的元素)
        /// </summary>
        /// <param name="item">要写入的元素</param>
        public void Push(T item)
        {
            buffer[head] = item;
            head = (head + 1) % Capacity;
            if (Count < Capacity) Count++;
        }

        /// <summary>
        /// 读取（0 = 最旧，Count-1 = 最新）
        /// </summary>
        /// <param name="index">索引号</param>
        /// <returns></returns>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new IndexOutOfRangeException();
                // 如果未满，最旧在 0 位置；若已满，最旧为 head
                int realIndex = (Count == Capacity) ? (head + index) % Capacity : index;
                return buffer[realIndex];
            }
            set
            {
                if (index < 0 || index >= Count) throw new IndexOutOfRangeException();
                int realIndex = (Count == Capacity) ? (head + index) % Capacity : index;
                buffer[realIndex] = value;
            }
        }

        /// <summary> 返回最新元素（Count-1）但不移除 </summary>
        public T PeekBack()
        {
            if (Count == 0) throw new InvalidOperationException("buffer is empty");
            int lastIndex = (head - 1 + Capacity) % Capacity;
            return buffer[lastIndex];
        }

        /// <summary> 弹出最新元素（Count-1），减少 Count 并返回该元素 </summary>
        public T PopBack()
        {
            if (Count == 0) throw new InvalidOperationException("buffer is empty");
            head = (head - 1 + Capacity) % Capacity;
            T val = buffer[head];
            Count--;
            return val;
        }

        /// <summary> 清空缓冲区 </summary>
        public void Clear()
        {
            // ✅ 优化：对于所有类型都清理（结构体可能包含引用类型字段）
            if (Count > 0)
            {
                // 清理数组中的所有元素，防止引用保持
                for (int i = 0; i < Capacity; i++)
                {
                    buffer[i] = default;
                }
                head = 0;
                Count = 0;
            }
        }
        #endregion

        #region 迭代器
        // 以「最旧→最新」迭代
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion
    }
}
