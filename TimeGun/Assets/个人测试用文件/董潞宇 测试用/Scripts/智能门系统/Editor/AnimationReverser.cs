using UnityEngine;
using UnityEditor;

namespace TimeGun.Editor
{
    /// <summary>
    /// 动画反转工具 - 用于从开门动画生成关门动画
    /// 使用方法：
    /// 1. 在Project窗口选中开门动画文件
    /// 2. 右键 → TimeGun → Reverse Animation Clip
    /// 3. 会在同目录生成 [原文件名]_Reversed.anim
    /// </summary>
    public static class AnimationReverser
    {
        [MenuItem("Assets/TimeGun/Reverse Animation Clip", false, 100)]
        private static void ReverseAnimationClip()
        {
            var clip = Selection.activeObject as AnimationClip;
            if (clip == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选中一个Animation Clip", "确定");
                return;
            }

            // 创建新的动画剪辑
            AnimationClip reversedClip = new AnimationClip();
            reversedClip.frameRate = clip.frameRate;

            // 获取所有曲线绑定
            var bindings = AnimationUtility.GetCurveBindings(clip);
            
            float clipLength = clip.length;

            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var reversedCurve = new AnimationCurve();

                // 反转关键帧
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    var key = curve.keys[i];
                    var reversedKey = new Keyframe(
                        clipLength - key.time,  // 反转时间
                        key.value,
                        -key.outTangent,        // 反转切线
                        -key.inTangent
                    );
                    reversedCurve.AddKey(reversedKey);
                }

                AnimationUtility.SetEditorCurve(reversedClip, binding, reversedCurve);
            }

            // 保存新动画
            string path = AssetDatabase.GetAssetPath(clip);
            string newPath = path.Replace(".anim", "_Reversed.anim");
            
            AssetDatabase.CreateAsset(reversedClip, newPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"✅ 反向动画已生成：{newPath}");
            EditorUtility.DisplayDialog("成功", $"反向动画已生成：\n{newPath}", "确定");
        }

        [MenuItem("Assets/TimeGun/Reverse Animation Clip", true)]
        private static bool ValidateReverseAnimationClip()
        {
            return Selection.activeObject is AnimationClip;
        }
    }
}
