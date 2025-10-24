using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Utility
{
    /// <summary>
    /// 循环数组, 作为固定大小的缓冲区，当添加新元素时会覆盖最旧的元素
    /// </summary>
    /// <typeparam name="T">数组元素类型</typeparam>
    public class RingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] buffer;
        private int head; // 下一个写入位置
        private bool full; // 是否已满

        public int Capacity { get; }
        public int Count => full ? Capacity : head;

        /// <summary>
        /// 构造函数, 创建一个指定容量的循环数组
        /// </summary>
        /// <param name="capacity">指定容量大小</param>
        /// <exception cref="ArgumentException">表示传递给方法的参数无效或超出预期范围的情况</exception>
        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("capacity must be > 0");
            Capacity = capacity;
            buffer = new T[capacity];
            head = 0;
            full = false;

        }

        /// <summary>
        /// 写入（添加）一个新元素，若已满则覆盖最旧元素
        /// </summary>
        /// <param name="item">写入的元素</param>
        public void Push(T item)
        {
            buffer[head] = item;
            head = (head + 1) % Capacity;
            if (head == 0) full = true;
        }

        /// <summary> 按「时间顺序」读取（0 = 最旧，Count-1 = 最新） </summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new IndexOutOfRangeException();

                int realIndex = full
                    ? (head + index) % Capacity
                    : index;

                return buffer[realIndex];
            }
        }

        // 让 foreach 支持以「最旧→最新」 迭代
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }
}