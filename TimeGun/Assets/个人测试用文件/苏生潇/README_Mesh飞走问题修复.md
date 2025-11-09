# 🐛 Mesh 飞走问题 - 紧急修复

## 问题原因

### 根本原因

```csharp
// ❌ 错误的做法
verts[i] = transform.InverseTransformPoint(worldPos);
```

**问题**：
1. `transform` 是 Enemy 的 Transform
2. `InverseTransformPoint` 将世界坐标转换到 Enemy 的本地空间
3. 但 Mesh 渲染器是 Enemy 的**子对象**
4. 子对象继承了父对象的 Transform
5. 导致坐标被"二次转换"，位置错误

### 图解

```
World Space (世界空间)
    └── Enemy (0, 0, 0)
            └── Frustum_Volume (子对象)
                    └── Mesh (期望的世界坐标)
                    
实际发生的事：
1. worldPos = (5, 1, 3)
2. transform.InverseTransformPoint(5,1,3) 
   → 转换到 Enemy 本地空间 = (5, 1, 3)
3. 但子对象又继承了 Enemy 的 Transform
4. 最终 Mesh 位置 = Enemy.position + (5,1,3) 
   → 如果 Enemy 在 (0,0,0)，Mesh 就在 (5,1,3)
   → 如果 Enemy 在 (10,0,10)，Mesh 就在 (15,1,13) ❌
```

---

## 解决方案 1：使用世界坐标（推荐）

### 修改 `InitComponents()`

```csharp
// Volume Mesh
if (fillVolume)
{
    var volObj = new GameObject("Frustum_Volume");
    volObj.transform.SetParent(null); // ✅ 不设置父对象！
    
    // ✅ 或者设置为父对象但位置归零
    volObj.transform.SetParent(transform);
    volObj.transform.position = Vector3.zero;
    volObj.transform.rotation = Quaternion.identity;
    volObj.transform.localScale = Vector3.one;
    
    _volumeFilter = volObj.AddComponent<MeshFilter>();
    _volumeRenderer = volObj.AddComponent<MeshRenderer>();
    _volumeMesh = new Mesh { name = "FrustumVolume" };
    _volumeFilter.mesh = _volumeMesh;
    
    _volumeRenderer.material = CreateOptimizedTransparentMaterial(normalColor);
    _volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
    _volumeRenderer.receiveShadows = false;
}
```

### 修改 Mesh 生成函数

```csharp
private void GenerateOccludedFrustumMesh(Vector3 origin, Color color)
{
    int segmentCount = occlusionRayCount;
    int verticalSegments = Mathf.Max(2, segments / 10);

    int totalVerts = 1 + segmentCount * (verticalSegments + 1);
    Vector3[] verts = new Vector3[totalVerts];

    // ✅ 直接使用世界坐标
    verts[0] = origin;

    float angleStep = _hFOV / (segmentCount - 1);
    float startAngle = -_hFOV / 2f;

    int vertIndex = 1;
    for (int h = 0; h < segmentCount; h++)
    {
        float hAngle = startAngle + angleStep * h;
        float maxDist = _occlusionDistances[h];

        for (int v = 0; v <= verticalSegments; v++)
        {
            float vAngle = Mathf.Lerp(-verticalFOV / 2f, verticalFOV / 2f, 
                                      v / (float)verticalSegments) + verticalOffset;
            Vector3 dir = GetDirection(hAngle, vAngle);
            
            float distance = Mathf.Lerp(nearClip, maxDist, v / (float)verticalSegments);
            // ✅ 直接使用世界坐标，不转换
            verts[vertIndex++] = origin + dir * distance;
        }
    }

    // ...三角形生成代码不变

    _volumeMesh.vertices = verts;
    _volumeMesh.triangles = tris;
    _volumeMesh.RecalculateNormals();
    _volumeMesh.RecalculateBounds();

    if (_volumeRenderer.material != null)
        _volumeRenderer.material.color = color;
}
```

### 修改简单版 Mesh 生成

```csharp
private void GenerateSimpleFrustumMesh(Vector3 origin, Color color)
{
    Vector3[] corners = GetFrustumCorners(origin);
    Vector3[] nearCorners = GetFrustumCorners(origin, nearClip / _radius);

    // ✅ 直接使用世界坐标
    Vector3[] verts = new Vector3[8];
    for (int i = 0; i < 4; i++)
    {
        verts[i] = nearCorners[i];
        verts[i + 4] = corners[i];
    }

    // ...三角形代码不变

    _volumeMesh.vertices = verts;
    _volumeMesh.triangles = tris;
    _volumeMesh.RecalculateNormals();
    _volumeMesh.RecalculateBounds();

    if (_volumeRenderer.material != null)
        _volumeRenderer.material.color = color;
}
```

### 修改盲区 Mesh

```csharp
private void UpdateBlindSpotMesh()
{
    _blindSpotMesh.Clear();

    Vector3 origin = _head.position + Vector3.up * planeHeightOffset;
    
    float blindAngleStart = _hFOV / 2f;
    float blindAngleEnd = 360f - _hFOV / 2f;

    int hSegs = segments / 2;
    int vSegs = segments / 4;

    int vertCount = (hSegs + 1) * (vSegs + 1);
    Vector3[] verts = new Vector3[vertCount];

    int vIdx = 0;
    for (int v = 0; v <= vSegs; v++)
    {
        float vAngle = Mathf.Lerp(-verticalFOV / 2f, verticalFOV / 2f, 
                                  v / (float)vSegs) + verticalOffset;

        for (int h = 0; h <= hSegs; h++)
        {
            float hAngle = Mathf.Lerp(blindAngleStart, blindAngleEnd, 
                                     h / (float)hSegs);
            
            Vector3 dir = GetDirection(hAngle, vAngle);
            // ✅ 直接使用世界坐标
            verts[vIdx++] = origin + dir * (_radius * 0.7f);
        }
    }

    // ...三角形代码不变

    _blindSpotMesh.vertices = verts;
    _blindSpotMesh.triangles = tris;
    _blindSpotMesh.RecalculateNormals();
    _blindSpotMesh.RecalculateBounds();
}
```

---

## 解决方案 2：保持父子关系（备选）

如果必须保持父子关系，需要正确处理坐标转换：

```csharp
private void InitComponents()
{
    // Volume Mesh
    if (fillVolume)
    {
        var volObj = new GameObject("Frustum_Volume");
        volObj.transform.SetParent(transform);
        
        // ✅ 重置本地Transform
        volObj.transform.localPosition = Vector3.zero;
        volObj.transform.localRotation = Quaternion.identity;
        volObj.transform.localScale = Vector3.one;
        
        _volumeFilter = volObj.AddComponent<MeshFilter>();
        _volumeRenderer = volObj.AddComponent<MeshRenderer>();
        _volumeMesh = new Mesh { name = "FrustumVolume" };
        _volumeFilter.mesh = _volumeMesh;
        
        _volumeRenderer.material = CreateOptimizedTransparentMaterial(normalColor);
        _volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _volumeRenderer.receiveShadows = false;
    }
}

// 生成 Mesh 时：
private void GenerateOccludedFrustumMesh(Vector3 origin, Color color)
{
    // ...顶点计算
    
    // ✅ 使用 Mesh 渲染器子对象的 Transform
    verts[vertIndex++] = _volumeFilter.transform.InverseTransformPoint(worldPos);
}
```

---

## 快速修复步骤

### 第一步：修改初始化代码

找到 `InitComponents()` 方法中的这些部分：

```csharp
// Volume Mesh
var volObj = new GameObject("Frustum_Volume");
volObj.transform.SetParent(transform);
// ✅ 添加以下三行
volObj.transform.localPosition = Vector3.zero;
volObj.transform.localRotation = Quaternion.identity;
volObj.transform.localScale = Vector3.one;
```

```csharp
// Blind Spot Mesh
var blindObj = new GameObject("BlindSpot_Volume");
blindObj.transform.SetParent(transform);
// ✅ 添加以下三行
blindObj.transform.localPosition = Vector3.zero;
blindObj.transform.localRotation = Quaternion.identity;
blindObj.transform.localScale = Vector3.one;
```

### 第二步：修改所有 `InverseTransformPoint` 调用

**查找所有出现的位置**：
- `GenerateOccludedFrustumMesh()`
- `GenerateSimpleFrustumMesh()`
- `UpdateBlindSpotMesh()`

**修改前**：
```csharp
verts[i] = transform.InverseTransformPoint(worldPos);
```

**修改后（方案1 - 直接使用世界坐标）**：
```csharp
verts[i] = worldPos;
```

**修改后（方案2 - 使用正确的Transform）**：
```csharp
verts[i] = _volumeFilter.transform.InverseTransformPoint(worldPos);
// 或者盲区：
verts[i] = _blindSpotFilter.transform.InverseTransformPoint(worldPos);
```

---

## 验证修复

### 运行前检查

1. 在 Unity 中运行游戏
2. 选中 Enemy GameObject
3. 在 Hierarchy 中展开，查看子对象：
   - `Frustum_Volume` → 位置应该是 `(0, 0, 0)`
   - `BlindSpot_Volume` → 位置应该是 `(0, 0, 0)`

### 运行时检查

1. 运行游戏
2. 在 Scene 视图中观察 Mesh
3. 应该看到：
   - Mesh 和线框在同一位置 ✅
   - Mesh 跟随敌人移动 ✅
   - Mesh 不会飞到远处 ✅

---

## 推荐方案

**方案 1（直接使用世界坐标）**：

优点：
- 最简单
- 性能最优（少一次坐标转换）
- 不会出错

缺点：
- Mesh 不会自动跟随 Enemy Transform（但我们每帧都重新生成，所以无所谓）

**结论**：使用方案 1

---

## 完整修复代码

请看下一个文件：`EnemyVisionVisualizer3D_Fixed.cs`

---

**修复状态**：📝 待应用  
**优先级**：🔴 最高（影响核心功能）  
**预计修复时间**：5分钟
