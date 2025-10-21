# Batch Renamer & Organize

---

# Batch Renamer & Organize

## Description (English)

**Batch Renamer & Organize** is a Unity Editor utility that allows you to:
- **Bulk-rename GameObjects** in the open scene (mode **Hierarchy**).
- **Bulk-rename assets and folders** in the Project window (mode **Project**).
- Optionally **move renamed files** into a specified folder under `Assets/`.

### Key Features
1. **Two Operating Modes**  
   - **Hierarchy** — renames selected GameObjects in the scene.  
   - **Project** — renames selected files or folders under `Assets/` in the Project window.

2. **Flexible Name Template** (`Name Template`)  
   - Supports two placeholders:  
     - `{original}` — inserts the current object/asset name (without extension).  
     - `{index}` — inserts a sequential number.  

   Examples:  
   - `Enemy_{index}` → `Enemy_01`, `Enemy_02`, …  
   - `Item_{original}_v2` → if original is `Sword`, becomes `Item_Sword_v2`.

3. **“Use Original Name ({original})” Option**  
   - If checked, `{original}` is replaced with the current name.  
   - If unchecked, `{original}` is replaced with an empty string.

4. **“Auto Numbering” Option**  
   - If checked, `{index}` is replaced with a number starting from `Start Index`.  
   - If unchecked, `{index}` is removed (replaced with an empty string).

5. **Index Parameters**  
   - **Start Index**: the starting number for `{index}` (default = `1`).  
   - **Padding Digits**: how many digits to pad (e.g., `01` instead of `1`).

6. **Move Files Option (Project Mode Only)**  
   - If **Move to Folder after Rename** is checked, renamed assets will be moved to the folder specified in `Target Folder` under `Assets/`.  
   - If the folder does not exist, it will be created automatically.

---

## Installation & Package Structure (English)

1. **Copy or extract** the contents of the downloaded archive/UnityPackage.  
2. **Copy the script**:
   ```
   Assets/Editor/BatchRenamer.cs
   ```
   → into your project under `Assets/Editor/`.  
3. **(Optional) Copy demo assets**:
   ```
   Assets/ExampleAssets/     ← demo prefabs, textures, materials  
   Assets/ExampleScene/      ← demo scene with GameObjects  
   ```
4. **(Optional) Place README** in the root:
   ```
   Assets/README_BatchRenamer.md
   ```

Unity will automatically compile the script, and the tool will appear under the **Window** menu.

---

## How to Use (English)

### 1. Open the Window
- In Unity: **Window → Batch Renamer**

### 2. Window Interface

```
┌─────────────────────────────────────────────────────────┐
│ Batch Renamer & Organize                              │
│--------------------------------------------------------│
│ Target Area: [ ▼ Hierarchy ]                           │  ← switch between “Hierarchy” and “Project”
│                                                        │
│ Name Template: [ Enemy_{index}        ]                │  ← new name template
│ Use Original Name (“{original}”) [✓]                   │  ← include {original} or not
│ Auto Numbering [✓]                                     │  ← enable/disable numbering
│    Start Index: [ 1 ] Padding Digits: [ 2 ]            │  ← parameters for {index}
│                                                        │
│ [ Preview ]   [ Apply ]                                │  ← preview and apply buttons
│                                                        │
│ Preview of Renaming:                                   │
│   OldName1    →  NewName1                              │  ← shows “old → new” list
│   OldName2    →  NewName2                              │
│   ...                                                  │
│                                                        │
└─────────────────────────────────────────────────────────┘
```

#### Field Explanations
- **Target Area**  
  - `Hierarchy` — bulk-rename GameObjects in the scene.  
  - `Project` — bulk-rename files and folders in the Project window.

- **Name Template**  
  Enter a string template for the new name.  
  - `{original}` — inserts the current name (without extension).  
  - `{index}` — inserts a sequential number (using Start Index and Padding Digits).

- **Use Original Name (“{original}”)**  
  - ☑ Checked → `{original}` replaced with current name.  
  - ☐ Unchecked → `{original}` replaced with an empty string.

- **Auto Numbering**  
  - ☑ Checked → `{index}` replaced by a number starting from `Start Index`.  
  - ☐ Unchecked → `{index}` removed (becomes empty string).

- **Start Index**  
  The number to start `{index}` from (e.g., `1`). Next items will be `2, 3, …`.

- **Padding Digits**  
  The number of digits used for `{index}`, e.g., `01, 02` instead of `1, 2`.

- **Move to Folder after Rename** (only when `Project` is selected)  
  - ☑ Checked → renamed assets will be moved to the folder specified in `Target Folder`.  
  - ☐ Unchecked → assets are only renamed, but remain in place.

- **Target Folder (Assets/…)** (enabled only if `Move to Folder` is checked)  
  The folder path under `Assets/` where renamed files will be moved. For example:
```
Assets/ExampleAssets/Renamed
```
  If the folder does not exist, it will be created.

- **Preview**  
  Generates a list of old names and corresponding new names (does not apply changes).

- **Apply**  
  Performs the renaming (and moves files if “Move to Folder” is checked) according to the template and settings.

---

## Usage Examples (English)

### A. Bulk-Rename GameObjects in Hierarchy

1. Open or create a scene and add a few objects, for example:
   - `Cube1`  
   - `Sphere_old`  
   - `EnemyA`

2. Select them in **Hierarchy** (Ctrl/Cmd + click each).

3. Open **Window → Batch Renamer**.

4. Ensure **Target Area** is set to `Hierarchy`.

5. In **Name Template**, enter:
   ```
   mod_{index}
   ```

6. Make sure:
   - `Use Original Name` is ☑ (your template does not use `{original}`, so it does not matter).  
   - `Auto Numbering` is ☑  
   - `Start Index = 1`  
   - `Padding Digits = 2`

7. Click **Preview**. You will see:
   ```
   Cube1      →  mod_01
   Sphere_old →  mod_02
   EnemyA     →  mod_03
   ```

8. If it looks correct, click **Apply**.  
   The GameObjects in the Hierarchy will be renamed:
   ```
   mod_01  
   mod_02  
   mod_03
   ```

---

### B. Bulk-Rename Assets in Project

1. In the **Project** window, create a folder:
   ```
   Assets/ExampleAssets
   ```

2. Inside **ExampleAssets**, create a few assets:
   - Right-click → Create → Prefab → Empty (name it “OldPrefab”)  
   - Right-click → Create → Material (name it “OldMaterial”)  
   - Right-click → Create → Folder → “NestedFolder”;
     enter it and create a new **Texture2D** named “OldTexture.png”

   Final structure:
   ```
   Assets/
     ExampleAssets/
       ├─ OldPrefab.prefab
       ├─ OldMaterial.mat
       └─ NestedFolder/
          └─ OldTexture.png
   ```

3. Select all three assets (Ctrl/Cmd + click each).

4. Open **Window → Batch Renamer**.

5. Set **Target Area** to `Project`.

6. In **Name Template**, enter:
   ```
   Asset_{original}_{index}
   ```

7. Make sure:
   - `Use Original Name` is ☑  
   - `Auto Numbering` is ☑  
   - `Start Index = 1`  
   - `Padding Digits = 2`

8. Check **Move to Folder after Rename** and set **Target Folder** to:
   ```
   Assets/ExampleAssets/Renamed
   ```

9. Click **Preview**. You will see:
   ```
   OldPrefab   →  Asset_OldPrefab_01
   OldMaterial →  Asset_OldMaterial_02
   OldTexture  →  Asset_OldTexture_03
   ```

10. Click **Apply**:  
    - A new folder `Assets/ExampleAssets/Renamed` will be created (if not already).  
    - The renamed files will be moved there:
      ```
      Assets/ExampleAssets/Renamed/Asset_OldPrefab_01.prefab
      Assets/ExampleAssets/Renamed/Asset_OldMaterial_02.mat
      Assets/ExampleAssets/Renamed/Asset_OldTexture_03.png
      ```
    - The originals (`OldPrefab.prefab`, `OldMaterial.mat`, `OldTexture.png`) are effectively gone from their old locations because they have been renamed and moved in one operation.

---

## Additional Tips (English)

- **No Selection**  
  If you click **Apply** with nothing selected, you will see a message:  
  ```
  Nothing selected!  
  ```
  — and no changes will be made.

- **Folder Creation**  
  If **Move to Folder** is enabled and the path in `Target Folder` does not exist, it will be created automatically.

- **Renaming Folders**  
  If you select an entire folder in `Project` mode, the script will rename the folder itself (including its contents). If you need to rename only the files inside, select the files rather than the folder.

- **Avoid Extra Underscores**  
  If your template leaves an unfilled placeholder (e.g., `{index}` with `Auto Numbering` off), you may end up with double underscores `__`. The code automatically `Trim('_')` at start and end, but it’s better to adjust your template to avoid undesired underscores.

- **Work in Small Batches**  
  If you want to control numbering precisely, select only those objects you wish to rename. Numbering proceeds in the order of selection.

---

## Testing in a “Clean” Project (English)

1. Create a new Unity project (recommended version 2019.4 or above).  
2. Import the package `BatchRenamer.unitypackage`:
   - **Assets → Import Package → Custom Package**  
   - Select `BatchRenamer.unitypackage` and click **Import**.

3. Check the Demo Scene:  
   - Open **ExampleScene/BatchRenamerDemoScene.unity**.  
   - Ensure it opens without errors.  
   - Select some GameObjects, open **Window → Batch Renamer**, test **Preview** and **Apply**.

4. Check the Demo Assets:  
   - In the Project window, locate **ExampleAssets**.  
   - Select the demo files, run **Preview** and **Apply** in `Project` mode.  
   - Confirm assets get renamed and moved into `Renamed` folder.

If everything works in the clean project, the package is ready for publication.

---

## License & Notes (English)

- All code in `BatchRenamer.cs` is authored by you and is licensed as part of this package (you may specify your own license on publication).  
- If demo assets contain third-party materials (textures, models), ensure they are under an appropriate license (e.g., CC0/Public Domain).  
- **Requirements:** Unity 2019.4 LTS or newer.

---