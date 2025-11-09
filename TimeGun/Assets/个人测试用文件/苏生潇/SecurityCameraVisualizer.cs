using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 安全摄像头3D视锥体可视化组件（Unity 6.2 + URP）
/// 扩展功能：
/// - 3D视锥体显示（支持垂直视角）
/// - 垂直视野从6点钟方向（向下）到9点钟方向（水平）
/// - 可控制的垂直偏移和平面高度
/// - 遮挡检测（墙壁后不绘制）
/// - 自动材质生成（无需手动配置）
/// - 摄像头摆动跟随
/// - 检测状态颜色渐变
/// </summary>
[RequireComponent(typeof(SecurityCamera))]
[AddComponentMenu("Security/Camera Visualizer 3D")]
public class SecurityCameraVisualizer : MonoBehaviour
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

    [Tooltip("是否启用遮挡检测（墙壁后不绘制）")]
    public bool enableOcclusionTest = true;

    [Header("3D 视锥体配置")]
    [Tooltip("垂直视野角度（度）- 从6点钟到9点钟，建议90度")]
    [Range(0f, 90f)]
    public float verticalFOV = 90f;

    [Tooltip("垂直偏移（度，负值=向下）- 建议-45度使视野从向下到水平")]
    [Range(-90f, 45f)]
    public float verticalOffset = -45f;

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
    [Tooltip("正常巡逻状态的视野颜色")]
    public Color normalColor = new Color(0f, 1f, 0f, 0.3f); // 绿色半透明

    [Tooltip("检测中状态的视野颜色")]
    public Color detectingColor = new Color(1f, 0.65f, 0f, 0.5f); // 橙色

    [Tooltip("警报状态的视野颜色")]
    public Color alarmColor = new Color(1f, 0f, 0f, 0.7f); // 红色

    [Header("性能优化")]
    [Tooltip("更新频率（帧/秒），0 表示每帧更新")]
    [Range(0, 60)]
    public int updateRate = 30;

    [Tooltip("最大渲染距离（超出此距离不渲染，0 表示无限制）")]
    [Min(0)]
    public float maxRenderDistance = 50f;
    #endregion

    #region 内部变量
    private SecurityCamera _camera;
    private LineRenderer _lineRenderer;
    private MeshFilter _volumeFilter;
    private MeshRenderer _volumeRenderer;
    private Mesh _volumeMesh;

    private float _updateTimer;
    private Transform _head;
    private float _radius, _hFOV;
    #endregion

    #region 初始化
    private void Awake()
    {
        _camera = GetComponent<SecurityCamera>();
        _head = _camera.cameraHead != null ? _camera.cameraHead : transform;
        _radius = _camera.viewRadius;
        _hFOV = _camera.viewAngle;

        if (occlusionMask == -1)
        {
            occlusionMask = _camera.obstacleMask;
        }

        InitComponents();
    }

    private void InitComponents()
    {
        // LineRenderer - 使用 transform 作为父级
        if (showWireframe)
        {
            var lineObj = new GameObject("Frustum_Wireframe");
            lineObj.transform.SetParent(transform);
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
            _volumeMesh = new Mesh { name = "CameraFrustumVolume" };
            _volumeFilter.mesh = _volumeMesh;
            
            _volumeRenderer.material = CreateOptimizedTransparentMaterial(normalColor);
            _volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _volumeRenderer.receiveShadows = false;
        }
    }

    private Material CreateOptimizedLineMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[SecurityCameraVisualizer] 未找到URP/Unlit着色器，使用默认着色器");
            shader = Shader.Find("Unlit/Color");
        }

        var mat = new Material(shader)
        {
            color = color,
            name = "CameraVision_Line_Auto"
        };

        return mat;
    }

    private Material CreateOptimizedTransparentMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[SecurityCameraVisualizer] 未找到URP/Unlit着色器，使用默认透明着色器");
            shader = Shader.Find("Transparent/Diffuse");
            var fallbackMat = new Material(shader) { color = color };
            return fallbackMat;
        }

        var mat = new Material(shader)
        {
            name = "CameraVision_Transparent_Auto"
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
        if (!enableVisualization || _camera == null)
        {
            HideAll();
            return;
        }

        // 距离剔除
        if (maxRenderDistance > 0 && Camera.main != null)
        {
            if (Vector3.Distance(transform.position, Camera.main.transform.position) > maxRenderDistance)
            {
                HideAll();
                return;
            }
        }

        // 更新频率控制
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
    }

    private Color GetStateColor()
    {
        if (_camera.IsDetectingPlayer)
        {
            // 检测中：根据进度在检测色和警报色之间插值
            float progress = _camera.DetectionProgress;
            return Color.Lerp(detectingColor, alarmColor, progress);
        }
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
        // 从中心点到四个角的射线
        for (int i = 0; i < 4; i++)
        {
            _lineRenderer.SetPosition(idx++, origin);
            _lineRenderer.SetPosition(idx++, corners[i]);
        }

        // 四个角之间的连线
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
    /// ? 扇形视锥体（切片形状）- 带遮挡检测
    /// 完全模仿 EnemyVisionVisualizer3D 的实现
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
                // ? 关键修改：垂直角度从 -verticalFOV/2 到 +verticalFOV/2，加上偏移
                // 当 verticalOffset = -45, verticalFOV = 90 时：
                // 范围从 -45 + (-45) = -90度（向下，6点钟）到 -45 + 45 = 0度（水平，9点钟）
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

        // ? 生成扇形三角形（从中心点辐射）- 完全相同的拓扑
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
    /// ? 简单扇形视锥体（无遮挡检测）
    /// 完全模仿 EnemyVisionVisualizer3D 的实现
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
                // ? 相同的垂直角度计算
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

    private Vector3[] GetFrustumCorners(Vector3 origin, float scale = 1f)
    {
        Vector3[] corners = new Vector3[4];
        float halfH = _hFOV / 2f;
        float halfV = verticalFOV / 2f;

        // ? 四个角：左上、右上、右下、左下
        corners[0] = origin + GetDirection(-halfH, halfV + verticalOffset) * (_radius * scale);
        corners[1] = origin + GetDirection(halfH, halfV + verticalOffset) * (_radius * scale);
        corners[2] = origin + GetDirection(halfH, -halfV + verticalOffset) * (_radius * scale);
        corners[3] = origin + GetDirection(-halfH, -halfV + verticalOffset) * (_radius * scale);

        return corners;
    }

    /// <summary>
    /// ? 计算方向向量 - 完全模仿 EnemyVisionVisualizer3D
    /// </summary>
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
    }

    private void OnDisable() => HideAll();

    private void OnDestroy()
    {
        if (_volumeMesh != null) Destroy(_volumeMesh);

        if (_volumeFilter != null) Destroy(_volumeFilter.gameObject);

        if (_lineRenderer?.material != null) Destroy(_lineRenderer.material);
        if (_volumeRenderer?.material != null) Destroy(_volumeRenderer.material);
    }
    #endregion

    #region 编辑器预览
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying || _camera == null) return;

        _head = _camera.cameraHead != null ? _camera.cameraHead : transform;
        _radius = _camera.viewRadius;
        _hFOV = _camera.viewAngle;

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

        // 绘制遮挡检测射线预览（黄色）
        if (enableOcclusionTest && occlusionRayCount > 0)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            float angleStep = _hFOV / (occlusionRayCount - 1);
            float startAngle = -_hFOV / 2f;

            Vector3 rayOrigin = _head.position + Vector3.up * planeHeightOffset;
            
            // ? 显示垂直扫描范围（6点钟到9点钟）
            for (int i = 0; i < occlusionRayCount; i++)
            {
                float angle = startAngle + angleStep * i;
                
                // 绘制上边界（接近9点钟，水平）
                Vector3 dirTop = GetDirection(angle, verticalFOV / 2f + verticalOffset);
                Gizmos.DrawRay(rayOrigin, dirTop * _radius);
                
                // 绘制下边界（接近6点钟，向下）
                Vector3 dirBottom = GetDirection(angle, -verticalFOV / 2f + verticalOffset);
                Gizmos.DrawRay(rayOrigin, dirBottom * _radius);
            }

            // 绘制中间的射线（显示垂直扫描）
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            int vRayCount = 5;
            for (int v = 0; v <= vRayCount; v++)
            {
                float vAngle = Mathf.Lerp(-verticalFOV / 2f, verticalFOV / 2f, v / (float)vRayCount) + verticalOffset;
                Vector3 dir = GetDirection(0f, vAngle); // 中间水平角度
                Gizmos.DrawRay(rayOrigin, dir * _radius * 0.8f);
            }
        }
    }
#endif
    #endregion
}
