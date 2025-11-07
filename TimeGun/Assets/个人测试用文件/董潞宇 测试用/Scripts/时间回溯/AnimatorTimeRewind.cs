using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace TimeRewind
{
    /// <summary>
    /// 专用于 Animator 的时间回溯组件（不录制 Transform）
    /// - 仅录制和回放 Animator 的层状态、归一化时间和参数
    /// - 回溯期间会冻结 Animator（speed = 0），防止动画继续播放
    /// - 适用于需要独立控制动画回溯的场景（如UI动画、独立动画对象等）
    /// </summary>
    /// <remarks>
    /// 使用场景：
    /// 1. 需要回溯动画但不需要回溯位置的物体
    /// 2. UI 动画的时间倒流效果
    /// 3. 独立于物体移动的动画效果
    /// 
    /// 注意：由于基类强制录制 Transform，这里通过重写方法来"禁用" Transform 录制
    /// </remarks>
    public class AnimatorTimeRewind : AbstractTimeRewindObject
    {
        //==================== 组件缓存 ====================
        [SerializeField, Tooltip("要控制的 Animator 组件")]
        private Animator targetAnimator;

        private Animator Anim => targetAnimator ??= GetComponent<Animator>();

        //==================== 历史缓冲 ====================
        private AnimatorRecorder _animRecorder;

        //==================== 冻结管理器 ====================
        /// <summary>
        /// Animator 的冻结状态
        /// </summary>
        private struct AnimatorFreezeState
        {
            public bool HadAnimator;
            public float OrigSpeed;
            public AnimatorRecorder.Snapshot OrigSnapshot;
        }

        private ComponentFreezeManager<AnimatorFreezeState> _freezeManager 
            = new ComponentFreezeManager<AnimatorFreezeState>();

        protected override int frameCount => _animRecorder?.Count ?? 0;
        //==================== 初始化 ====================
        /// <summary>
        /// 初始化 Animator 录制器
        /// </summary>
        protected override void MainInit()
        {
            // ✅ 仍然调用基类初始化（创建 Transform 历史缓冲，虽然不会使用）
            base.MainInit();

            // 创建 Animator 专用的历史缓冲
            var animBuffer = RewindInit<AnimatorRecorder.Snapshot>(out _);
            _animRecorder = new AnimatorRecorder(Anim, animBuffer);
        }

        //==================== 录制与回放 ====================
        /// <summary>
        /// 录制一帧快照（仅录制 Animator，忽略 Transform）
        /// </summary>
        protected override void RecordOneSnap()
        {
            // ❌ 不调用基类的 Transform 录制
            // base.RecordOneSnap();

            // ✅ 仅录制 Animator
            _animRecorder?.RecordOneSnap();
        }

        /// <summary>
        /// 回放一帧快照（仅回放 Animator，忽略 Transform）
        /// </summary>
        protected override void RewindOneSnap()
        {
            // ❌ 不调用基类的 Transform 回放
            // base.RewindOneSnap();

            // ✅ 仅回放 Animator
            _animRecorder?.RewindOneSnap();
        }


        //==================== 冻结控制 ====================
        /// <summary>
        /// 回溯开始：冻结 Animator
        /// </summary>
        protected override void OnStartRewind()
        {
            base.OnStartRewind();
            RequestFreezeAnimator();
        }

        /// <summary>
        /// 回溯结束：解冻 Animator（恢复到回溯后的状态）
        /// </summary>
        protected override void OnStopRewind()
        {
            base.OnStopRewind();
            ReleaseFreezeAnimator(restoreToSnapshot: true);
        }

        /// <summary>
        /// 暂停开始：冻结 Animator
        /// </summary>
        protected override void OnStartPause()
        {
            base.OnStartPause();
            RequestFreezeAnimator();
        }

        /// <summary>
        /// 暂停结束：解冻 Animator（恢复到暂停前的状态）
        /// </summary>
        protected override void OnStopPause()
        {
            base.OnStopPause();
            ReleaseFreezeAnimator(restoreToSnapshot: false);
        }

        /// <summary>
        /// 请求冻结 Animator（引用计数+1）
        /// </summary>
        private void RequestFreezeAnimator()
        {
            var currentState = new AnimatorFreezeState();

            if (Anim != null)
            {
                currentState.HadAnimator = true;
                currentState.OrigSpeed = Anim.speed;
                currentState.OrigSnapshot = _animRecorder?.CaptureCurrentSnapshot();
            }

            // 仅首次冻结时执行冻结操作
            if (_freezeManager.RequestFreeze(currentState))
            {
                if (Anim != null)
                {
                    Anim.speed = 0f; // 冻结动画
                }
            }
        }

        /// <summary>
        /// 释放冻结 Animator（引用计数-1）
        /// </summary>
        /// <param name="restoreToSnapshot">
        /// true = 恢复到回溯后的状态（回溯结束）
        /// false = 恢复到原始状态（暂停结束）
        /// </param>
        private void ReleaseFreezeAnimator(bool restoreToSnapshot)
        {
            // 仅完全解冻时执行解冻操作
            if (!_freezeManager.ReleaseFreeze(out var savedState))
            {
                return;
            }

            if (savedState.HadAnimator && Anim != null)
            {
                // 恢复动画速度
                Anim.speed = savedState.OrigSpeed;

                // 根据场景恢复状态
                if (!restoreToSnapshot && savedState.OrigSnapshot != null)
                {
                    // 暂停结束：恢复到冻结前的原始状态
                    _animRecorder?.ApplySnapshot(savedState.OrigSnapshot);
                }
                // 回溯结束：不需要恢复，因为已在 RewindOneSnap 中应用了最后一帧
            }
        }

        //==================== 清理 ====================
        private void OnDestroy()
        {
            _animRecorder = null;
            Debug.Log($"{gameObject.name} 的 AnimatorTimeRewind 已清理");
        }

        //==================== Animator Recorder（复用 EnemyTimeRewind 的实现）====================
        /// <summary>
        /// Animator 的历史录制与回放封装（与 EnemyTimeRewind 中的实现一致）
        /// </summary>
        private sealed class AnimatorRecorder
        {
            /// <summary>
            /// 快照仅存储动态数据（层状态 + 参数值）
            /// </summary>
            public sealed class Snapshot
            {
                public int[] LayerStateHashes; // 每层的动画状态哈希
                public float[] LayerNormalizedTimes; // 每层的归一化播放时间
                public ParamValue[] ParameterValues; // 参数值数组
            }

            /// <summary>
            /// 参数元数据结构（仅初始化时构建一次）
            /// </summary>
            private struct ParamMetadata
            {
                public int Hash;
                public AnimatorControllerParameterType Type;
            }

            /// <summary>
            /// Union 优化：参数值使用显式内存布局，节省内存
            /// </summary>
            [System.Runtime.InteropServices.StructLayout(
                System.Runtime.InteropServices.LayoutKind.Explicit)]
            public struct ParamValue
            {
                [System.Runtime.InteropServices.FieldOffset(0)]
                public float F;

                [System.Runtime.InteropServices.FieldOffset(0)]
                public int I;

                [System.Runtime.InteropServices.FieldOffset(0)]
                public bool B;
            }

            private readonly Animator _anim;
            private readonly RingBuffer<Snapshot> _history;
            private readonly ParamMetadata[] _paramMetadata;

            public int Count => _history?.Count ?? 0;

            public AnimatorRecorder(Animator anim, RingBuffer<Snapshot> buffer)
            {
                _anim = anim;
                _history = buffer;
                _paramMetadata = BuildParamMetadata(anim);
            }

            /// <summary>
            /// 提取所有非 Trigger 参数的元数据
            /// </summary>
            private static ParamMetadata[] BuildParamMetadata(Animator anim)
            {
                if (anim == null) return Array.Empty<ParamMetadata>();

                var parameters = anim.parameters;
                var list = new List<ParamMetadata>(parameters.Length);

                foreach (var p in parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger) continue;
                    list.Add(new ParamMetadata
                    {
                        Hash = p.nameHash,
                        Type = p.type
                    });
                }

                return list.ToArray();
            }

            /// <summary>
            /// 录制当前帧的 Animator 状态快照
            /// </summary>
            public void RecordOneSnap()
            {
                if (_anim == null) return;

                int layerCount = _anim.layerCount;
                var snap = new Snapshot
                {
                    LayerStateHashes = new int[layerCount],
                    LayerNormalizedTimes = new float[layerCount],
                    ParameterValues = CaptureParameterValues(_anim, _paramMetadata)
                };

                for (int layer = 0; layer < layerCount; layer++)
                {
                    var st = _anim.GetCurrentAnimatorStateInfo(layer);
                    snap.LayerStateHashes[layer] = st.fullPathHash;
                    snap.LayerNormalizedTimes[layer] = st.normalizedTime;
                }

                _history.Push(snap);
            }

            /// <summary>
            /// 回放一帧 Animator 快照
            /// </summary>
            public void RewindOneSnap()
            {
                if (_anim == null || _history.Count == 0) return;

                var snap = _history.PopBack();

                var layerLen = Mathf.Min(_anim.layerCount, snap.LayerStateHashes.Length);
                for (int layer = 0; layer < layerLen; layer++)
                {
                    _anim.Play(snap.LayerStateHashes[layer], layer, snap.LayerNormalizedTimes[layer]);
                }

                ApplyParameters(_anim, _paramMetadata, snap.ParameterValues);
                _anim.Update(0f);
            }

            /// <summary>
            /// 捕获当前 Animator 完整状态
            /// </summary>
            public Snapshot CaptureCurrentSnapshot()
            {
                if (_anim == null) return null;

                int layerCount = _anim.layerCount;
                var snap = new Snapshot
                {
                    LayerStateHashes = new int[layerCount],
                    LayerNormalizedTimes = new float[layerCount],
                    ParameterValues = CaptureParameterValues(_anim, _paramMetadata)
                };

                for (int layer = 0; layer < layerCount; layer++)
                {
                    var st = _anim.GetCurrentAnimatorStateInfo(layer);
                    snap.LayerStateHashes[layer] = st.fullPathHash;
                    snap.LayerNormalizedTimes[layer] = st.normalizedTime;
                }

                return snap;
            }

            /// <summary>
            /// 应用 Animator 快照
            /// </summary>
            public void ApplySnapshot(Snapshot snap)
            {
                if (_anim == null || snap == null) return;

                var layerLen = Mathf.Min(_anim.layerCount, snap.LayerStateHashes.Length);
                for (int layer = 0; layer < layerLen; layer++)
                {
                    _anim.Play(snap.LayerStateHashes[layer], layer, snap.LayerNormalizedTimes[layer]);
                }

                ApplyParameters(_anim, _paramMetadata, snap.ParameterValues);
                _anim.Update(0f);
            }

            /// <summary>
            /// 基于预构建的元数据捕获参数值
            /// </summary>
            private static ParamValue[] CaptureParameterValues(Animator anim, ParamMetadata[] metadata)
            {
                if (metadata == null || metadata.Length == 0)
                    return Array.Empty<ParamValue>();

                var values = new ParamValue[metadata.Length];

                for (int i = 0; i < metadata.Length; i++)
                {
                    var meta = metadata[i];

                    switch (meta.Type)
                    {
                        case AnimatorControllerParameterType.Float:
                            values[i].F = anim.GetFloat(meta.Hash);
                            break;
                        case AnimatorControllerParameterType.Int:
                            values[i].I = anim.GetInteger(meta.Hash);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            values[i].B = anim.GetBool(meta.Hash);
                            break;
                    }
                }

                return values;
            }

            /// <summary>
            /// 基于元数据 + 值数组恢复参数
            /// </summary>
            private static void ApplyParameters(Animator anim, ParamMetadata[] metadata, ParamValue[] values)
            {
                if (metadata == null || values == null) return;

                int count = Mathf.Min(metadata.Length, values.Length);
                for (int i = 0; i < count; i++)
                {
                    var meta = metadata[i];
                    var val = values[i];

                    switch (meta.Type)
                    {
                        case AnimatorControllerParameterType.Float:
                            anim.SetFloat(meta.Hash, val.F);
                            break;
                        case AnimatorControllerParameterType.Int:
                            anim.SetInteger(meta.Hash, val.I);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            anim.SetBool(meta.Hash, val.B);
                            break;
                    }
                }
            }
        }
    }
}
    