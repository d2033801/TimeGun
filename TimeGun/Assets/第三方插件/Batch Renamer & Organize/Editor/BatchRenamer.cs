using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Udoc.BatchRenamer
{
    public class BatchRenamerWindow : EditorWindow
    {
        // 1. Public fields (Inspector settings):
        private enum TargetArea { Hierarchy, Project }
        private TargetArea _targetArea = TargetArea.Hierarchy;

        private string _nameTemplate = "NewName_{index}";    // Template for names ("{original}" and "{index}")
        private bool _useOriginalName = true;               // Include original name ("{original}")
        private bool _autoNumbering = true;                 // Append numeric index
        private int _startIndex = 1;                        // Starting index
        private int _numberPadding = 2;                     // Number of digits ("01", "02")

        private bool _moveToFolder = false;                 // Move to folder flag
        private string _targetFolderPath = "Assets/";       // Destination folder (for assets)

        // For Preview: list of "old name → new name"
        private Vector2 _scrollPos;
        private List<KeyValuePair<string, string>> _previewList = new List<KeyValuePair<string, string>>();

        // 2. Add menu item and open window
        [MenuItem("Window/Batch Renamer & Organize")]
        public static void ShowWindow()
        {
            var window = GetWindow<BatchRenamerWindow>("Batch Renamer");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("Batch Renamer & Organize", EditorStyles.boldLabel);

            // 2.1 Select mode (Hierarchy or Project)
            _targetArea = (TargetArea)EditorGUILayout.EnumPopup("Target Area:", _targetArea);

            GUILayout.Space(10);

            // 2.2 Name template and options
            _nameTemplate = EditorGUILayout.TextField("Name Template:", _nameTemplate);
            _useOriginalName = EditorGUILayout.Toggle("Use Original Name (\"{original}\")", _useOriginalName);
            _autoNumbering = EditorGUILayout.Toggle("Auto Numbering", _autoNumbering);
            if (_autoNumbering)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    _startIndex = EditorGUILayout.IntField("Start Index:", _startIndex);
                    _numberPadding = EditorGUILayout.IntField("Padding Digits:", _numberPadding);
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // 2.3 Move-to-folder options (Project mode only)
            if (_targetArea == TargetArea.Project)
            {
                _moveToFolder = EditorGUILayout.Toggle("Move to Folder after Rename", _moveToFolder);
                using (new EditorGUI.DisabledScope(!_moveToFolder))
                {
                    _targetFolderPath = EditorGUILayout.TextField("Target Folder (Assets/...):", _targetFolderPath);
                }
            }

            GUILayout.Space(15);

            // 2.4 Preview & Apply buttons
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Preview"))
                    GeneratePreview();
                if (GUILayout.Button("Apply"))
                    ApplyRename();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);

            // 2.5 Display preview list
            if (_previewList.Count > 0)
            {
                GUILayout.Label("Preview of Renaming:", EditorStyles.boldLabel);
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

                const float oldNameMinWidth = 100f;
                const float arrowWidth = 20f;
                foreach (var kvp in _previewList)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(kvp.Key, GUILayout.MinWidth(oldNameMinWidth), GUILayout.MaxWidth(200));
                        EditorGUILayout.LabelField("→", GUILayout.Width(arrowWidth));
                        EditorGUILayout.LabelField(kvp.Value);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            // --- Free version “Upgrade to PRO” promo banner ---
            GUILayout.FlexibleSpace(); // push promo to bottom

            EditorGUILayout.BeginVertical("box");
            {
                // Light background for promo area
                GUI.color = new Color(1f, 0.9f, 0.6f);
                EditorGUILayout.LabelField("🔒 Upgrade to PRO for more features", EditorStyles.boldLabel);
                GUI.color = Color.white;

                // List PRO-only capabilities not in demo
                EditorGUILayout.LabelField(
                    "- Full Preview & Undo/Redo support\n" +
                    "- Recursive folder scan & batch-organize\n" +
                    "- Advanced name templates & JSON presets\n" +
                    "- Powerful filters & Regex find/replace\n" +
                    "- Export CSV & Copy to Clipboard",
                    EditorStyles.wordWrappedLabel
                );

                GUILayout.Space(5);

                // Green “Buy PRO” button
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Learn More / Buy PRO", GUILayout.Height(30)))
                {
                    // Opens your website until Asset Store link is ready
                    Application.OpenURL("https://u-doc.github.io/");
                }
                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndVertical();

        }

        // 3. Generate preview list
        private void GeneratePreview()
        {
            _previewList.Clear();
            int idx = _startIndex;

            if (_targetArea == TargetArea.Hierarchy)
            {
                foreach (var go in Selection.gameObjects)
                {
                    string oldName = go.name;
                    string newName = BuildNewName(oldName, idx);
                    _previewList.Add(new KeyValuePair<string, string>(oldName, newName));
                    if (_autoNumbering) idx++;
                }
            }
            else
            {
                foreach (var guid in Selection.assetGUIDs)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    string oldName = Path.GetFileNameWithoutExtension(assetPath);
                    string newName = BuildNewName(oldName, idx);
                    _previewList.Add(new KeyValuePair<string, string>(oldName, newName));
                    if (_autoNumbering) idx++;
                }
            }
        }

        // 4. Apply renaming (and moving)
        private void ApplyRename()
        {
            if (Selection.objects.Length == 0)
            {
                EditorUtility.DisplayDialog("Batch Renamer", "Nothing selected!", "OK");
                return;
            }

            if (_targetArea == TargetArea.Project && _moveToFolder)
            {
                if (!AssetDatabase.IsValidFolder(_targetFolderPath))
                {
                    Directory.CreateDirectory(_targetFolderPath);
                    AssetDatabase.Refresh();
                }
            }

            int idx = _startIndex;

            if (_targetArea == TargetArea.Hierarchy)
            {
                foreach (var go in Selection.gameObjects)
                {
                    Undo.RecordObject(go, "Batch Rename");
                    go.name = BuildNewName(go.name, idx);
                    idx++;
                }
            }
            else
            {
                foreach (var guid in Selection.assetGUIDs)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    string newName = BuildNewName(Path.GetFileNameWithoutExtension(assetPath), idx);

                    AssetDatabase.RenameAsset(assetPath, newName);

                    if (_moveToFolder)
                    {
                        string currentPath = AssetDatabase.GUIDToAssetPath(guid);
                        string destPath = $"{_targetFolderPath}/{newName}{Path.GetExtension(assetPath)}";
                        AssetDatabase.MoveAsset(currentPath, destPath);
                    }
                    idx++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _previewList.Clear();
        }

        // 5. Build new name from template
        private string BuildNewName(string originalName, int index)
        {
            string result = _nameTemplate;

            if (_useOriginalName)
                result = result.Replace("{original}", originalName);
            else
                result = result.Replace("{original}", "");

            if (_autoNumbering)
            {
                string number = index.ToString().PadLeft(_numberPadding, '0');
                result = result.Replace("{index}", number);
            }
            else
            {
                result = result.Replace("{index}", "");
            }

            return result.Trim('_');
        }
    }
}
