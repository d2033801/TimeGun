using UnityEngine;
using UnityEditor;

namespace TimeGun.Editor
{
    /// <summary>
    /// IK 系统迁移验证工具
    /// 使用方法：Tools → TimeGun → Validate IK Migration
    /// </summary>
    public class IKMigrationValidator : EditorWindow
    {
        private GameObject playerObject;
        private Vector2 scrollPos;
        private bool autoFix = false;

        [MenuItem("Tools/TimeGun/Validate IK Migration")]
        public static void ShowWindow()
        {
            var window = GetWindow<IKMigrationValidator>("IK 迁移验证");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("IK 系统迁移验证工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            playerObject = (GameObject)EditorGUILayout.ObjectField(
                "Player GameObject",
                playerObject,
                typeof(GameObject),
                true
            );

            EditorGUILayout.Space();

            if (playerObject == null)
            {
                EditorGUILayout.HelpBox("请选择场景中的 Player GameObject", MessageType.Info);
                return;
            }

            autoFix = EditorGUILayout.Toggle("自动修复问题", autoFix);

            if (GUILayout.Button("验证配置", GUILayout.Height(30)))
            {
                ValidateSetup();
            }

            EditorGUILayout.Space();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            ShowValidationResults();
            EditorGUILayout.EndScrollView();
        }

        private void ValidateSetup()
        {
            Debug.Log("========== IK 迁移验证开始 ==========");

            // 1. 检查 PlayerController
            var playerController = playerObject.GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("❌ 未找到 PlayerController 组件！", playerObject);
            }
            else
            {
                Debug.Log("✅ 找到 PlayerController", playerObject);
            }

            // 2. 检查 WeaponManager
            var weaponManager = playerObject.GetComponent<WeaponManager>();
            if (weaponManager == null)
            {
                Debug.LogError("❌ 未找到 WeaponManager 组件！", playerObject);
            }
            else
            {
                Debug.Log("✅ 找到 WeaponManager", playerObject);
            }

            // 3. 查找 Animator
            var animator = playerObject.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("❌ 未找到 Animator 组件！", playerObject);
            }
            else
            {
                Debug.Log($"✅ 找到 Animator：{animator.gameObject.name}", animator.gameObject);

                // 检查 Humanoid
                if (!animator.isHuman)
                {
                    Debug.LogError($"❌ Animator 不是 Humanoid 类型！", animator.gameObject);
                }
                else
                {
                    Debug.Log("✅ Animator 是 Humanoid 类型", animator.gameObject);
                }

                // 4. 检查 WeaponIKHandler
                var ikHandler = animator.GetComponent<WeaponIKHandler>();
                if (ikHandler == null)
                {
                    Debug.LogWarning($"⚠️ 未找到 WeaponIKHandler！", animator.gameObject);
                    
                    if (autoFix)
                    {
                        Debug.Log("🔧 自动添加 WeaponIKHandler...");
                        ikHandler = animator.gameObject.AddComponent<WeaponIKHandler>();
                        EditorUtility.SetDirty(animator.gameObject);
                        Debug.Log("✅ WeaponIKHandler 已添加", animator.gameObject);
                    }
                }
                else
                {
                    Debug.Log("✅ 找到 WeaponIKHandler", animator.gameObject);
                }

                // 5. 检查 WeaponIKHandler 配置
                if (ikHandler != null)
                {
                    var serializedHandler = new SerializedObject(ikHandler);
                    var weaponManagerProp = serializedHandler.FindProperty("weaponManager");
                    
                    if (weaponManagerProp.objectReferenceValue == null)
                    {
                        Debug.LogWarning("⚠️ WeaponIKHandler 的 WeaponManager 引用为空");
                        
                        if (autoFix && weaponManager != null)
                        {
                            Debug.Log("🔧 自动设置 WeaponManager 引用...");
                            weaponManagerProp.objectReferenceValue = weaponManager;
                            serializedHandler.ApplyModifiedProperties();
                            EditorUtility.SetDirty(ikHandler);
                            Debug.Log("✅ WeaponManager 引用已设置");
                        }
                    }
                    else
                    {
                        Debug.Log($"✅ WeaponManager 引用已设置：{weaponManagerProp.objectReferenceValue.name}");
                    }
                }
            }

            // 6. 检查武器预制体
            if (weaponManager != null && Application.isPlaying)
            {
                var weapon = weaponManager.CurrentWeapon;
                if (weapon != null)
                {
                    ValidateWeapon(weapon);
                }
                else
                {
                    Debug.LogWarning("⚠️ 当前没有装备武器（需要运行游戏才能检测）");
                }
            }

            Debug.Log("========== IK 迁移验证完成 ==========");
        }

        private void ValidateWeapon(AbstractWeaponBase weapon)
        {
            Debug.Log($"--- 验证武器：{weapon.name} ---");

            if (weapon.rightHandIkTarget == null)
                Debug.LogWarning("⚠️ 右手 IK 目标未设置", weapon);
            else
                Debug.Log($"✅ 右手 IK 目标：{weapon.rightHandIkTarget.name}", weapon);

            if (weapon.leftHandIkTarget == null)
                Debug.LogWarning("⚠️ 左手 IK 目标未设置", weapon);
            else
                Debug.Log($"✅ 左手 IK 目标：{weapon.leftHandIkTarget.name}", weapon);

            if (weapon.muzzlePoint == null)
                Debug.LogError("❌ 枪口位置（MuzzlePoint）未设置！", weapon);
            else
                Debug.Log($"✅ 枪口位置：{weapon.muzzlePoint.name}", weapon);
        }

        private void ShowValidationResults()
        {
            if (playerObject == null) return;

            EditorGUILayout.LabelField("配置状态", EditorStyles.boldLabel);

            // PlayerController 状态
            var playerController = playerObject.GetComponent<PlayerController>();
            DrawStatus("PlayerController", playerController != null);

            // WeaponManager 状态
            var weaponManager = playerObject.GetComponent<WeaponManager>();
            DrawStatus("WeaponManager", weaponManager != null);

            // Animator 状态
            var animator = playerObject.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                DrawStatus($"Animator ({animator.gameObject.name})", true);
                DrawStatus("  ├─ Humanoid 类型", animator.isHuman);

                var ikHandler = animator.GetComponent<WeaponIKHandler>();
                DrawStatus("  └─ WeaponIKHandler", ikHandler != null);

                if (ikHandler != null)
                {
                    var serializedHandler = new SerializedObject(ikHandler);
                    var weaponManagerProp = serializedHandler.FindProperty("weaponManager");
                    bool hasReference = weaponManagerProp.objectReferenceValue != null;
                    DrawStatus("      └─ WeaponManager 引用", hasReference);
                }
            }
            else
            {
                DrawStatus("Animator", false);
            }

            // 层级结构建议
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("推荐层级结构", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Player (PlayerController + WeaponManager)\n" +
                "└── Model (Animator + WeaponIKHandler)\n" +
                "    └── Armature\n" +
                "        └── RightHand\n" +
                "            └── Weapon\n" +
                "                ├── RightHandIK\n" +
                "                ├── LeftHandIK\n" +
                "                └── MuzzlePoint",
                MessageType.Info
            );
        }

        private void DrawStatus(string label, bool isOk)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(250));
            
            var color = isOk ? Color.green : Color.red;
            var prevColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(isOk ? "✅" : "❌", GUILayout.Width(30));
            GUI.color = prevColor;
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
