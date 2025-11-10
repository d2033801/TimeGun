using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 敌人3D视锥体可视化组件（Unity 6.2 + URP）
/// 扩展功能：
/// - 3D视锥体显示（支持垂直视角）
/// - 可控制的垂直偏移和平面高度
/// - 盲区显示（视野死角）
/// - 遮挡检测（墙壁后不绘制）
/// - 自动材质生成（无需手动配置）
/// - 敌人死亡时自动关闭
/// </summary>
[RequireComponent(typeof(Enemy))]
[AddComponentMenu("Enemy/Vision Visualizer 3D")]
public class EnemyVisionVisualizer3D : MonoBehaviour
{
    #region 配置参数
    [Header("可视化设置")]
    [Tooltip("是否启用视野可视化")]
    public bool enableVisualization = true;

    [Tooltip("视锥体分段数（越高越平滑，建议 20-40）")]
    [Range(10, 60)]
    public int segments = 30;

    [Tooltip("是否绘制视锥体线框")]
    public bool showWireframe = true;

    [Tooltip("是否填充视锥体体积")]
    public bool fillVolume = true;

    [Tooltip("是否显示盲区（背后区域）")]
    public bool showBlindSpot = true;

    [Tooltip("是否启用遮挡检测（墙壁后不绘制）")]
    public bool enableOcclusionTest = true;

    [Header("3D 视锥体配置")]
    [Tooltip("垂直视野角度（度）")]
    [Range(0f, 90f)]
    public float verticalFOV = 30f;

    [Tooltip("垂直偏移（度，正=向上，负=向下）")]
    [Range(-45f, 45f)]
    public float verticalOffset = 0f;

    [Tooltip("平面高度偏移（米，调整视锥体离地高度）")]
    [Range(-2f, 2f)]
    public float planeHeightOffset = 0f;

    [Tooltip("近裁剪距离（米）")]
    [Min(0.1f)]
    public float nearClip = 0.5f;

    [Header("遮挡检测配置")]
    [Tooltip("遮挡检测的射线数量（越多越精确但性能消耗越大）")]
    [Range(5, 30)]
    public int occlusionRayCount = 15;

    [Tooltip("遮挡检测使用的LayerMask")]
    public LayerMask occlusionMask = -1;

    [Header("颜色配置")]
    public Color normalColor = new Color(0f, 1f, 1f, 0.3f);
    public Color alertColor = new Color(1f, 0.65f, 0f, 0.5f);
    public Color detectedColor = new Color(1f, 0f, 0f, 0.7f);
    public Color blindSpotColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    [Header("性能优化")]
    [Range(0, 60)]
    public int updateRate = 30;

    [Min(0)]
    public float maxRenderDistance = 50f;
    #endregion

    #region 内部变量
    private Enemy _enemy;
    private LineRenderer _lineRenderer;
    private MeshFilter _volumeFilter, _blindSpotFilter;
    private MeshRenderer _volumeRenderer, _blindSpotRenderer;
    private Mesh _volumeMesh, _blindSpotMesh;

    private float _updateTimer;
    private Transform _head;
    private float _radius, _hFOV;
    #endregion

    #region 初始化
    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _head = _enemy.headTransform;
        _radius = _enemy.viewRadius;
        _hFOV = _enemy.viewAngle;

        if (occlusionMask == -1)
        {
            occlusionMask = _enemy.obstacleMask;
        }

        InitComponents();
    }

    private void InitComponents()
    {
        // LineRenderer - 使用 _enemy.transform 作为父级
        if (showWireframe)
        {
            var lineObj = new GameObject("Frustum_Wireframe");
            lineObj.transform.SetParent(_enemy.transform);
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localRotation = Quaternion.identity;
            lineObj.transform.localScale = Vector3.one;
            
            _lineRenderer = lineObj.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = _lineRenderer.endWidth = 0.05f;
            _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.allowOcclusionWhenDynamic = false;
            
            _lineRenderer.material = CreateOptimizedLineMaterial(normalColor);
        }

        // Volume Mesh - 独立存在
        if (fillVolume)
        {
            var volObj = new GameObject("Frustum_Volume");
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

        // Blind Spot Mesh - 独立存在
        if (showBlindSpot)
        {
            var blindObj = new GameObject("BlindSpot_Volume");
            blindObj.transform.position = Vector3.zero;
            blindObj.transform.rotation = Quaternion.identity;
            blindObj.transform.localScale = Vector3.one;
            
            _blindSpotFilter = blindObj.AddComponent<MeshFilter>();
            _blindSpotRenderer = blindObj.AddComponent<MeshRenderer>();
            _blindSpotMesh = new Mesh { name = "BlindSpot" };
            _blindSpotFilter.mesh = _blindSpotMesh;
            
            _blindSpotRenderer.material = CreateOptimizedTransparentMaterial(blindSpotColor);
            _blindSpotRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _blindSpotRenderer.receiveShadows = false;
        }
    }

    private Material CreateOptimizedLineMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("未找到URP/Unlit着色器，使用默认着色器");
            shader = Shader.Find("Unlit/Color");
        }

        var mat = new Material(shader)
        {
            color = color,
            name = "EnemyVision_Line_Auto"
        };

        return mat;
    }

    private Material CreateOptimizedTransparentMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("未找到URP/Unlit着色器，使用默认透明着色器");
            shader = Shader.Find("Transparent/Diffuse");
            var fallbackMat = new Material(shader) { color = color };
            return fallbackMat;
        }

        var mat = new Material(shader)
        {
            name = "EnemyVision_Transparent_Auto"
        };
        
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_Cull", 0);
        mat.renderQueue = (int)RenderQueue.Transparent;
        
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        
        mat.color = color;

        return mat;
    }
    #endregion

    #region 更新
    private void Update()
    {
        if (!enableVisualization || _enemy == null || _enemy.IsDead)
        {
            HideAll();
            return;
        }

        // ✅ 修复：使用 _enemy.transform
        if (maxRenderDistance > 0 && Camera.main != null)
        {
            if (Vector3.Distance(_enemy.transform.position, Camera.main.transform.position) > maxRenderDistance)
            {
                HideAll();
                return;
            }
        }

        if (updateRate > 0)
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer < 1f / updateRate) return;
            _updateTimer = 0f;
        }

        Render();
    }

    private void Render()
    {
        if (_head == null) return;

        Color color = GetStateColor();

        if (showWireframe && _lineRenderer != null)
        {
            _lineRenderer.enabled = true;
            DrawWireframe(color);
        }

        if (fillVolume && _volumeMesh != null)
        {
            _volumeRenderer.enabled = true;
            UpdateVolumeMesh(color);
        }

        if (showBlindSpot && _blindSpotMesh != null)
        {
            _blindSpotRenderer.enabled = true;
            UpdateBlindSpotMesh();
        }
    }

    private Color GetStateColor()
    {
        if (_enemy.CanSeePlayer()) return detectedColor;
        if (_enemy.stateMachine?.CurrentState == _enemy.alertState) return alertColor;
        return normalColor;
    }
    #endregion

    #region 视锥体绘制
    private void DrawWireframe(Color color)
    {
        _lineRenderer.startColor = _lineRenderer.endColor = color;
        _lineRenderer.positionCount = 12;

        Vector3 origin = _head.position + Vector3.up * planeHeightOffset + _head.forward * nearClip;
        Vector3[] corners = GetFrustumCorners(origin);

        int idx = 0;
        for (int i = 0; i < 4; i++)
        {
            _lineRenderer.SetPosition(idx++, origin);
            _lineRenderer.SetPosition(idx++, corners[i]);
        }

        for (int i = 0; i < 4; i++)
        {
            _lineRenderer.SetPosition(idx++, corners[i]);
        }
    }

    private void UpdateVolumeMesh(Color color)
    {
        _volumeMesh.Clear();

        Vector3 origin = _head.position + Vector3.up * planeHeightOffset + _head.forward * nearClip;
        
        if (enableOcclusionTest)
        {
            GenerateOccludedFrustumMesh(origin, color);
        }
        else
        {
            GenerateSimpleFrustumMesh(origin, color);
        }
    }

    /// <summary>
    /// ✅ 扇形视锥体（切片蛋糕形状）- 带遮挡检测
    /// </summary>
    private void GenerateOccludedFrustumMesh(Vector3 origin, Color color)
    {
        int hSegs = occlusionRayCount;
        int vSegs = Mathf.Max(2, segments / 10);

        // 顶点数：中心点 + 每条射线的顶点（从近到远）
        int totalVerts = 1 + hSegs * (vSegs + 1);
        Vector3[] verts = new Vector3[totalVerts];

        // 中心点（扇形的顶点）
        verts[0] = origin;

        float hAngleStep = _hFOV / (hSegs - 1);
        float hStartAngle = -_hFOV / 2f;

        int vertIndex = 1;
        
        // 对每个水平角度生成一条"射线"
        for (int h = 0; h < hSegs; h++)
        {
            float hAngle = hStartAngle + hAngleStep * h;

            // 沿每条射线从近到远生成顶点
            for (int v = 0; v <= vSegs; v++)
            {
                float vAngle = Mathf.Lerp(-verticalFOV / 2f, verticalFOV / 2f, v / (float)vSegs) + verticalOffset;
                Vector3 dir = GetDirection(hAngle, vAngle);
                
                // 每个顶点独立检测遮挡
                float actualDist = _radius;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, _radius, occlusionMask))
                {
                    actualDist = hit.distance;
                }
                
                // 从近裁剪到实际距离插值
                float distance = Mathf.Lerp(nearClip, actualDist, v / (float)vSegs);
                verts[vertIndex++] = origin + dir * distance;
            }
        }

        // ✅ 生成扇形三角形（从中心点辐射）
        List<int> trisList = new List<int>();

        // 1. 连接中心点到第一圈顶点（形成扇形底部）
        for (int h = 0; h < hSegs - 1; h++)
        {
            int baseIndex = 1 + h * (vSegs + 1);
            int nextBaseIndex = 1 + (h + 1) * (vSegs + 1);

            // 从中心点到近裁剪圈的三角形
            trisList.Add(0);
            trisList.Add(baseIndex);
            trisList.Add(nextBaseIndex);
        }

        // 2. 沿射线方向连接形成侧面
        for (int h = 0; h < hSegs - 1; h++)
        {
            for (int v = 0; v < vSegs; v++)
            {
                int i0 = 1 + h * (vSegs + 1) + v;
                int i1 = 1 + (h + 1) * (vSegs + 1) + v;

                // 第一个三角形
                trisList.Add(i0);
                trisList.Add(i1);
                trisList.Add(i0 + 1);

                // 第二个三角形
                trisList.Add(i0 + 1);
                trisList.Add(i1);
                trisList.Add(i1 + 1);
            }
        }

        // 3. 封闭左右两侧（扇形的两条边）
        // 左侧面
        for (int v = 0; v < vSegs; v++)
        {
            int i0 = 1 + v;
            trisList.Add(0);
            trisList.Add(i0 + 1);
            trisList.Add(i0);
        }

        // 右侧面
        int rightStart = 1 + (hSegs - 1) * (vSegs + 1);
        for (int v = 0; v < vSegs; v++)
        {
            int i0 = rightStart + v;
            trisList.Add(0);
            trisList.Add(i0);
            trisList.Add(i0 + 1);
        }

        _volumeMesh.vertices = verts;
        _volumeMesh.triangles = trisList.ToArray();
        _volumeMesh.RecalculateNormals();
        _volumeMesh.RecalculateBounds();

        if (_volumeRenderer.material != null)
            _volumeRenderer.material.color = color;
    }

    /// <summary>
    /// ✅ 简单扇形视锥体（无遮挡检测）
    /// </summary>
    private void GenerateSimpleFrustumMesh(Vector3 origin, Color color)
    {
        int hSegs = occlusionRayCount;
        int vSegs = Mathf.Max(2, segments / 10);

        int totalVerts = 1 + hSegs * (vSegs + 1);
        Vector3[] verts = new Vector3[totalVerts];

        verts[0] = origin;

        float hAngleStep = _hFOV / (hSegs - 1);
        float hStartAngle = -_hFOV / 2f;

        int vertIndex = 1;
        
        for (int h = 0; h < hSegs; h++)
        {
            float hAngle = hStartAngle + hAngleStep * h;

            for (int v = 0; v <= vSegs; v++)
            {
                float vAngle = Mathf.Lerp(-verticalFOV / 2f, verticalFOV / 2f, v / (float)vSegs) + verticalOffset;
                Vector3 dir = GetDirection(hAngle, vAngle);
                
                float distance = Mathf.Lerp(nearClip, _radius, v / (float)vSegs);
                verts[vertIndex++] = origin + dir * distance;
            }
        }

        // 生成三角形（与遮挡版本相同的拓扑）
        List<int> trisList = new List<int>();

        // 从中心点到第一圈
        for (int h = 0; h < hSegs - 1; h++)
        {
            int baseIndex = 1 + h * (vSegs + 1);
            int nextBaseIndex = 1 + (h + 1) * (vSegs + 1);

            trisList.Add(0);
            trisList.Add(baseIndex);
            trisList.Add(nextBaseIndex);
        }

        // 侧面
        for (int h = 0; h < hSegs - 1; h++)
        {
            for (int v = 0; v < vSegs; v++)
            {
                int i0 = 1 + h * (vSegs + 1) + v;
                int i1 = 1 + (h + 1) * (vSegs + 1) + v;

                trisList.Add(i0);
                trisList.Add(i1);
                trisList.Add(i0 + 1);

                trisList.Add(i0 + 1);
                trisList.Add(i1);
                trisList.Add(i1 + 1);
            }
        }

        // 左侧面
        for (int v = 0; v < vSegs; v++)
        {
            int i0 = 1 + v;
            trisList.Add(0);
            trisList.Add(i0 + 1);
            trisList.Add(i0);
        }

        // 右侧面
        int rightStart = 1 + (hSegs - 1) * (vSegs + 1);
        for (int v = 0; v < vSegs; v++)
        {
            int i0 = rightStart + v;
            trisList.Add(0);
            trisList.Add(i0);
            trisList.Add(i0 + 1);
        }

        _volumeMesh.vertices = verts;
        _volumeMesh.triangles = trisList.ToArray();
        _volumeMesh.RecalculateNormals();
        _volumeMesh.RecalculateBounds();

        if (_volumeRenderer.material != null)
            _volumeRenderer.material.color = color;
    }

    /// <summary>
    /// ✅ 扇形盲区（背后的切片蛋糕形状）
    /// </summary>
    private void UpdateBlindSpotMesh()
    {
        _blindSpotMesh.Clear();

        Vector3 origin = _head.position + Vector3.up * planeHeightOffset;
        
        // 盲区角度范围
        float blindAngleStart = _hFOV / 2f;
        float blindAngleEnd = 360f - _hFOV / 2f;
        float blindAngleRange = blindAngleEnd - blindAngleStart;

        int hSegs = segments / 2;
        int vSegs = segments / 4;

        // 顶点数：中心点 + 扇形顶点
        int totalVerts = 1 + (hSegs + 1) * (vSegs + 1);
        Vector3[] verts = new Vector3[totalVerts];

        // 中心点
        verts[0] = origin;

        int vIdx = 1;
        for (int h = 0; h <= hSegs; h++)
        {
            float hAngle = Mathf.Lerp(blindAngleStart, blindAngleEnd, h / (float)hSegs);

            for (int v = 0; v <= vSegs; v++)
            {
                float vAngle = Mathf.Lerp(-verticalFOV / 2f, verticalFOV / 2f, 
                                          v / (float)vSegs) + verticalOffset;
                
                Vector3 dir = GetDirection(hAngle, vAngle);
                
                // 从中心向外扩展
                float distance = Mathf.Lerp(0.1f, _radius * 0.7f, v / (float)vSegs);
                verts[vIdx++] = origin + dir * distance;
            }
        }

        // 生成三角形
        List<int> trisList = new List<int>();

        // 从中心点到第一圈
        for (int h = 0; h < hSegs; h++)
        {
            int baseIndex = 1 + h * (vSegs + 1);
            int nextBaseIndex = 1 + (h + 1) * (vSegs + 1);

            trisList.Add(0);
            trisList.Add(baseIndex);
            trisList.Add(nextBaseIndex);
        }

        // 扇形侧面
        for (int h = 0; h < hSegs; h++)
        {
            for (int v = 0; v < vSegs; v++)
            {
                int i0 = 1 + h * (vSegs + 1) + v;
                int i1 = 1 + (h + 1) * (vSegs + 1) + v;

                trisList.Add(i0);
                trisList.Add(i1);
                trisList.Add(i0 + 1);

                trisList.Add(i0 + 1);
                trisList.Add(i1);
                trisList.Add(i1 + 1);
            }
        }

        // 左右两侧封闭面
        // 左侧
        for (int v = 0; v < vSegs; v++)
        {
            int i0 = 1 + v;
            trisList.Add(0);
            trisList.Add(i0 + 1);
            trisList.Add(i0);
        }

        // 右侧
        int rightStart = 1 + hSegs * (vSegs + 1);
        for (int v = 0; v < vSegs; v++)
        {
            int i0 = rightStart + v;
            trisList.Add(0);
            trisList.Add(i0);
            trisList.Add(i0 + 1);
        }

        _blindSpotMesh.vertices = verts;
        _blindSpotMesh.triangles = trisList.ToArray();
        _blindSpotMesh.RecalculateNormals();
        _blindSpotMesh.RecalculateBounds();
    }

    private Vector3[] GetFrustumCorners(Vector3 origin, float scale = 1f)
    {
        Vector3[] corners = new Vector3[4];
        float halfH = _hFOV / 2f;
        float halfV = verticalFOV / 2f;

        corners[0] = origin + GetDirection(-halfH, halfV + verticalOffset) * (_radius * scale);
        corners[1] = origin + GetDirection(halfH, halfV + verticalOffset) * (_radius * scale);
        corners[2] = origin + GetDirection(halfH, -halfV + verticalOffset) * (_radius * scale);
        corners[3] = origin + GetDirection(-halfH, -halfV + verticalOffset) * (_radius * scale);

        return corners;
    }

    private Vector3 GetDirection(float hAngle, float vAngle)
    {
        float yaw = (_head.eulerAngles.y + hAngle) * Mathf.Deg2Rad;
        float pitch = vAngle * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Sin(yaw) * Mathf.Cos(pitch),
            Mathf.Sin(pitch),
            Mathf.Cos(yaw) * Mathf.Cos(pitch)
        ).normalized;
    }
    #endregion

    #region 清理
    private void HideAll()
    {
        if (_lineRenderer != null) _lineRenderer.enabled = false;
        if (_volumeRenderer != null) _volumeRenderer.enabled = false;
        if (_blindSpotRenderer != null) _blindSpotRenderer.enabled = false;
    }

    private void OnDisable() => HideAll();

    private void OnDestroy()
    {
        if (_volumeMesh != null) Destroy(_volumeMesh);
        if (_blindSpotMesh != null) Destroy(_blindSpotMesh);

        if (_volumeFilter != null) Destroy(_volumeFilter.gameObject);
        if (_blindSpotFilter != null) Destroy(_blindSpotFilter.gameObject);

        // ✅ 修复：检查渲染器是否仍然存在再访问材质
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
    #endregion

    #region 编辑器预览
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying || _enemy == null) return;

        _head = _enemy.headTransform;
        _radius = _enemy.viewRadius;
        _hFOV = _enemy.viewAngle;

        if (_head == null) return;

        Gizmos.color = normalColor;
        Vector3 origin = _head.position + Vector3.up * planeHeightOffset + _head.forward * nearClip;
        Vector3[] corners = GetFrustumCorners(origin);

        // 绘制视锥体线框
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(origin, corners[i]);
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }

        // 绘制盲区提示
        if (showBlindSpot)
        {
            Gizmos.color = blindSpotColor;
            Vector3 backDir = -_head.forward;
            Gizmos.DrawRay(_head.position, backDir * _radius * 0.5f);
        }

        // 绘制遮挡检测射线（黄色）
        if (enableOcclusionTest && occlusionRayCount > 0)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            float angleStep = _hFOV / (occlusionRayCount - 1);
            float startAngle = -_hFOV / 2f;

            Vector3 rayOrigin = _head.position + Vector3.up * planeHeightOffset;
            for (int i = 0; i < occlusionRayCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector3 dir = GetDirection(angle, verticalOffset);
                Gizmos.DrawRay(rayOrigin, dir * _radius);
            }
        }
    }
#endif
    #endregion
}
