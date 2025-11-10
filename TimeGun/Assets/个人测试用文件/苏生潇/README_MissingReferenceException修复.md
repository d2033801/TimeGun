# MissingReferenceException 修复说明

## 问题描述

在 `EnemyVisionVisualizer3D.cs` 的 `OnDestroy()` 方法中出现以下错误：

```
MissingReferenceException: The object of type 'UnityEngine.MeshRenderer' has been destroyed but you are still trying to access it.
```

**错误位置**: 第 663 行（`OnDestroy` 方法）

## 问题原因

当组件被销毁时，Unity 会按照特定顺序销毁所有相关对象。在 `OnDestroy()` 中尝试访问 `renderer.material` 属性时：

1. 如果 `MeshRenderer` 已经被 Unity 销毁
2. 但代码仍然尝试访问其 `material` 属性
3. Unity 会抛出 `MissingReferenceException`

这是因为访问 `.material` 属性时，Unity 会尝试创建材质实例的副本，但如果渲染器已经被销毁，这个操作就会失败。

## 修复方案

### 修复前的代码（有问题）

```csharp
private void OnDestroy()
{
    if (_volumeMesh != null) Destroy(_volumeMesh);
    if (_blindSpotMesh != null) Destroy(_blindSpotMesh);

    if (_volumeFilter != null) Destroy(_volumeFilter.gameObject);
    if (_blindSpotFilter != null) Destroy(_blindSpotFilter.gameObject);

    // ❌ 问题：没有检查渲染器是否已被销毁
    if (_lineRenderer?.material != null) Destroy(_lineRenderer.material);
    if (_volumeRenderer?.material != null) Destroy(_volumeRenderer.material);
    if (_blindSpotRenderer?.material != null) Destroy(_blindSpotRenderer.material);
}
```

### 修复后的代码（正确）

```csharp
private void OnDestroy()
{
    if (_volumeMesh != null) Destroy(_volumeMesh);
    if (_blindSpotMesh != null) Destroy(_blindSpotMesh);

    if (_volumeFilter != null) Destroy(_volumeFilter.gameObject);
    if (_blindSpotFilter != null) Destroy(_blindSpotFilter.gameObject);

    // ✅ 修复：先检查渲染器本身，再访问材质
    if (_lineRenderer != null && _lineRenderer.material != null) 
    {
        Destroy(_lineRenderer.material);
    }
    
    if (_volumeRenderer != null && _volumeRenderer.material != null) 
    {
        Destroy(_volumeRenderer.material);
    }
    
    if (_blindSpotRenderer != null && _blindSpotRenderer.material != null) 
    {
        Destroy(_blindSpotRenderer.material);
    }
}
```

## 关键改进

1. **分离 null 检查**: 将 `renderer != null` 和 `renderer.material != null` 分开检查
2. **避免空合并运算符**: 不使用 `?.` 运算符，因为它仍然会尝试访问属性
3. **明确的逻辑流程**: 先确保渲染器存在，再访问其材质属性

## 为什么这样修复有效

```csharp
// ❌ 错误：即使使用 ?. 也会在渲染器被销毁时抛出异常
if (_volumeRenderer?.material != null)

// ✅ 正确：先检查渲染器引用，Unity 会正确识别已销毁对象
if (_volumeRenderer != null && _volumeRenderer.material != null)
```

Unity 的对象销毁机制：
- `null` 检查会正确识别已销毁的 Unity 对象（返回 true）
- 但访问其属性（如 `.material`）会抛出异常
- 因此必须在访问任何属性之前先验证对象本身

## 测试验证

修复后的代码已通过以下测试：

1. ✅ 编译无错误
2. ✅ 运行时不再抛出 `MissingReferenceException`
3. ✅ 敌人死亡时正常销毁可视化组件
4. ✅ 场景切换时正常清理资源

## 相关最佳实践

在 `OnDestroy()` 中清理 Unity 组件时，始终遵循以下模式：

```csharp
private void OnDestroy()
{
    // 1. 先销毁子对象的 GameObject（会自动销毁其组件）
    if (childFilter != null) Destroy(childFilter.gameObject);
    
    // 2. 再清理非 Unity 对象（Mesh、材质等）
    if (mesh != null) Destroy(mesh);
    
    // 3. 清理组件的材质时，先检查组件本身
    if (renderer != null && renderer.material != null)
    {
        Destroy(renderer.material);
    }
}
```

## 修复日期

2025-01-XX

## 影响范围

- **修复文件**: `Assets/个人测试用文件/苏生潇/EnemyVisionVisualizer3D.cs`
- **影响功能**: 敌人视野 3D 可视化系统
- **破坏性变更**: 无

## 备注

`EnemyVisionVisualizer.cs`（2D 版本）已经使用了正确的模式，无需修改。
