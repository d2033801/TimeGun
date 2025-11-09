using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 敌人视野可视化组件（Unity 6.2 + URP 优化版本）
/// 功能：
/// - 运行时实时绘制扇形视野范围（使用 LineRenderer）
/// - 支持颜色渐变（正常/警戒/发现玩家）
/// - 自动从 Enemy 组件读取视野参数（零配置）
/// - 可选：半透明扇形填充（使用 Mesh）
/// </summary>
[RequireComponent(typeof(Enemy))]
[AddComponentMenu("Enemy/Vision Visualizer")]
public class EnemyVisionVisualizer : MonoBehaviour
{
    #region 配置参数
    [Header("可视化设置")]
    [Tooltip("是否启用视野可视化")]
    public bool enableVisualization = true;

    [Tooltip("视野扇形的分段数（越高越平滑，建议 20-60）")]
    [Range(10, 100)]
    public int arcSegments = 40;

    [Tooltip("是否绘制扇形边界线")]
    public bool showArcBorder = true;

    [Tooltip("是否绘制视野边界射线")]
    public bool showBoundaryRays = true;

    [Tooltip("是否填充扇形区域（需要 Mesh）")]
    public bool fillArc = true;

    [Header("材质配置（可选）")]
    [Tooltip("LineRenderer 使用的材质（留空则自动创建）")]
    public Material lineMaterial;

    [Tooltip("扇形填充 Mesh 使用的材质（留空则自动创建透明材质）")]
    public Material fillMaterial;

    [Header("颜色配置")]
    [Tooltip("正常巡逻状态的视野颜色")]
    public Color normalColor = new Color(0f, 1f, 1f, 0.3f); // 青色半透明

    [Tooltip("警戒状态的视野颜色")]
    public Color alertColor = new Color(1f, 0.65f, 0f, 0.5f); // 橙色

    [Tooltip("发现玩家后的视野颜色")]
    public Color detectedColor = new Color(1f, 0f, 0f, 0.7f); // 红色

    [Header("性能优化")]
    [Tooltip("更新频率（帧/秒），0 表示每帧更新")]
    [Range(0, 60)]
    public int updateRate = 30;

    [Tooltip("最大可视距离（超出此距离不渲染，0 表示无限制）")]
    [Min(0)]
    public float maxVisibleDistance = 50f;
    #endregion

    #region 内部变量
    private Enemy _enemy;
    private LineRenderer _lineRenderer;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _visionMesh;

    private float _updateTimer = 0f;
    private float _updateInterval;

    // 缓存的视野参数（减少每帧访问 Enemy）
    private Transform _headTransform;
    private float _viewRadius;
    private float _viewAngle;
    #endregion

    #region 初始化
    private void Awake()
    {
        _enemy = GetComponent<Enemy>();

        // 缓存 Enemy 的视野参数
        _headTransform = _enemy.headTransform;
        _viewRadius = _enemy.viewRadius;
        _viewAngle = _enemy.viewAngle;

        // 计算更新间隔
        _updateInterval = updateRate > 0 ? 1f / updateRate : 0f;

        InitializeVisuals();
    }

    private void OnEnable()
    {
        UpdateVisualization(); // 立即更新一次
    }

    /// <summary>
    /// 初始化可视化组件（LineRenderer + MeshRenderer）
    /// </summary>
    private void InitializeVisuals()
    {
        // 创建 LineRenderer（边界线）
        if (showArcBorder || showBoundaryRays)
        {
            var lineObj = new GameObject("VisionLine");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;

            _lineRenderer = lineObj.AddComponent<LineRenderer>();
            
            // ✅ Unity 6.2 现代化配置
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.alignment = LineAlignment.TransformZ; // 面向相机
            _lineRenderer.textureMode = LineTextureMode.Stretch;
            _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.allowOcclusionWhenDynamic = false;

            // 设置线宽（世界空间）
            _lineRenderer.startWidth = 0.05f;
            _lineRenderer.endWidth = 0.05f;

            // ✅ 使用自定义材质或自动创建
            if (lineMaterial != null)
            {
                _lineRenderer.material = lineMaterial;
            }
            else
            {
                _lineRenderer.material = CreateDefaultLineMaterial();
            }
        }

        // 创建 Mesh（扇形填充）
        if (fillArc)
        {
            var meshObj = new GameObject("VisionMesh");
            meshObj.transform.SetParent(transform);
            meshObj.transform.localPosition = Vector3.zero;

            _meshFilter = meshObj.AddComponent<MeshFilter>();
            _meshRenderer = meshObj.AddComponent<MeshRenderer>();

            // 创建 Mesh
            _visionMesh = new Mesh
            {
                name = "VisionArcMesh"
            };
            _meshFilter.mesh = _visionMesh;

            // ✅ 使用自定义材质或自动创建
            if (fillMaterial != null)
            {
                _meshRenderer.material = fillMaterial;
            }
            else
            {
                _meshRenderer.material = CreateDefaultFillMaterial();
            }

            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }
    }

    /// <summary>
    /// 创建默认 LineRenderer 材质（URP Unlit 不透明）
    /// </summary>
    private Material CreateDefaultLineMaterial()
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = normalColor;
        return material;
    }

    /// <summary>
    /// 创建默认 Mesh 材质（URP Unlit 透明）
    /// </summary>
    private Material CreateDefaultFillMaterial()
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        
        // ✅ 配置透明模式（关键步骤）
        material.SetFloat("_Surface", 1); // Transparent
        material.SetFloat("_Blend", 0);   // Alpha
        material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.color = normalColor;

        return material;
    }
    #endregion

    #region 更新逻辑
    private void Update()
    {
        if (!enableVisualization || _enemy == null || _enemy.IsDead)
        {
            HideVisualization();
            return;
        }

        // 距离裁剪（性能优化）
        if (maxVisibleDistance > 0 && Camera.main != null)
        {
            float distToCamera = Vector3.Distance(transform.position, Camera.main.transform.position);
            if (distToCamera > maxVisibleDistance)
            {
                HideVisualization();
                return;
            }
        }

        // 帧率限制
        if (_updateInterval > 0)
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer < _updateInterval)
                return;
            _updateTimer = 0f;
        }

        UpdateVisualization();
    }

    /// <summary>
    /// 更新视野可视化（绘制扇形 + 更新颜色）
    /// </summary>
    private void UpdateVisualization()
    {
        if (_headTransform == null) return;

        // 重新启用渲染器（如果之前被隐藏）
        if (_lineRenderer != null) _lineRenderer.enabled = true;
        if (_meshRenderer != null) _meshRenderer.enabled = true;

        // 获取当前视野颜色（根据敌人状态）
        Color currentColor = GetCurrentVisionColor();

        // 更新 LineRenderer
        if (_lineRenderer != null && (showArcBorder || showBoundaryRays))
        {
            DrawVisionArc(currentColor);
        }

        // 更新 Mesh
        if (_meshFilter != null && fillArc)
        {
            UpdateVisionMesh(currentColor);
        }
    }

    /// <summary>
    /// 根据敌人状态获取视野颜色
    /// </summary>
    private Color GetCurrentVisionColor()
    {
        // 检测是否发现玩家
        if (_enemy.CanSeePlayer())
        {
            return detectedColor;
        }

        // 检测是否处于警戒状态（通过状态机判断）
        if (_enemy.stateMachine?.CurrentState == _enemy.alertState)
        {
            return alertColor;
        }

        return normalColor;
    }

    /// <summary>
    /// 绘制视野扇形边界线（使用 LineRenderer）
    /// </summary>
    private void DrawVisionArc(Color color)
    {
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;

        int totalPoints = 0;

        // 计算需要的点数
        if (showBoundaryRays)
            totalPoints += 4; // 左右边界 + 中心到两端

        if (showArcBorder)
            totalPoints += arcSegments + 1;

        _lineRenderer.positionCount = totalPoints;

        int index = 0;
        Vector3 origin = _headTransform.position;

        // 绘制左右边界射线
        if (showBoundaryRays)
        {
            Vector3 leftDir = DirFromAngle(-_viewAngle / 2);
            Vector3 rightDir = DirFromAngle(_viewAngle / 2);

            // 左边界
            _lineRenderer.SetPosition(index++, origin);
            _lineRenderer.SetPosition(index++, origin + leftDir * _viewRadius);

            // 右边界
            _lineRenderer.SetPosition(index++, origin);
            _lineRenderer.SetPosition(index++, origin + rightDir * _viewRadius);
        }

        // 绘制扇形弧线
        if (showArcBorder)
        {
            float angleStep = _viewAngle / arcSegments;
            float startAngle = -_viewAngle / 2;

            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector3 dir = DirFromAngle(angle);
                _lineRenderer.SetPosition(index++, origin + dir * _viewRadius);
            }
        }
    }

    /// <summary>
    /// 更新扇形 Mesh（填充区域）
    /// </summary>
    private void UpdateVisionMesh(Color color)
    {
        if (_visionMesh == null) return;

        _visionMesh.Clear();

        int vertexCount = arcSegments + 2; // 中心点 + 弧线上的点 + 额外一个闭合点
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[arcSegments * 3];

        Vector3 origin = _headTransform.position;

        // 第一个顶点：扇形中心（原点）
        vertices[0] = transform.InverseTransformPoint(origin);

        // 生成弧线上的顶点
        float angleStep = _viewAngle / arcSegments;
        float startAngle = -_viewAngle / 2;

        for (int i = 0; i <= arcSegments; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 dir = DirFromAngle(angle);
            Vector3 worldPos = origin + dir * _viewRadius;
            vertices[i + 1] = transform.InverseTransformPoint(worldPos);
        }

        // 生成三角形（扇形面）
        for (int i = 0; i < arcSegments; i++)
        {
            triangles[i * 3] = 0;           // 中心点
            triangles[i * 3 + 1] = i + 1;   // 当前弧线点
            triangles[i * 3 + 2] = i + 2;   // 下一个弧线点
        }

        _visionMesh.vertices = vertices;
        _visionMesh.triangles = triangles;
        _visionMesh.RecalculateNormals();
        _visionMesh.RecalculateBounds();

        // 更新材质颜色
        if (_meshRenderer != null && _meshRenderer.material != null)
        {
            _meshRenderer.material.color = color;
        }
    }

    /// <summary>
    /// 隐藏可视化（性能优化：超出距离时禁用渲染）
    /// </summary>
    private void HideVisualization()
    {
        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        if (_meshRenderer != null)
            _meshRenderer.enabled = false;
    }

    /// <summary>
    /// 计算视野方向（从角度到世界方向向量）
    /// </summary>
    private Vector3 DirFromAngle(float angleDeg)
    {
        angleDeg += _headTransform.eulerAngles.y;
        return new Vector3(
            Mathf.Sin(angleDeg * Mathf.Deg2Rad),
            0,
            Mathf.Cos(angleDeg * Mathf.Deg2Rad)
        );
    }
    #endregion

    #region 清理
    private void OnDisable()
    {
        HideVisualization();
    }

    private void OnDestroy()
    {
        // 释放 Mesh 内存
        if (_visionMesh != null)
        {
            Destroy(_visionMesh);
        }

        // ✅ 仅释放自动创建的材质（用户提供的材质不销毁）
        if (_lineRenderer != null && _lineRenderer.material != null && lineMaterial == null)
        {
            Destroy(_lineRenderer.material);
        }

        if (_meshRenderer != null && _meshRenderer.material != null && fillMaterial == null)
        {
            Destroy(_meshRenderer.material);
        }
    }
    #endregion

    #region 编辑器辅助
#if UNITY_EDITOR
    /// <summary>
    /// Scene 视图中绘制预览（仅在未运行时显示）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying || _enemy == null) return;

        // 刷新缓存的参数（编辑器模式下）
        _headTransform = _enemy.headTransform;
        _viewRadius = _enemy.viewRadius;
        _viewAngle = _enemy.viewAngle;

        if (_headTransform == null) return;

        // 绘制预览（简化版，使用 Gizmos）
        Gizmos.color = normalColor;
        
        Vector3 origin = _headTransform.position;
        Vector3 leftDir = DirFromAngle(-_viewAngle / 2);
        Vector3 rightDir = DirFromAngle(_viewAngle / 2);

        // 绘制视野边界
        Gizmos.DrawLine(origin, origin + leftDir * _viewRadius);
        Gizmos.DrawLine(origin, origin + rightDir * _viewRadius);

        // 绘制扇形弧线（简化版）
        float angleStep = _viewAngle / 20f;
        float startAngle = -_viewAngle / 2;
        Vector3 prevPoint = origin + DirFromAngle(startAngle) * _viewRadius;

        for (int i = 1; i <= 20; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 dir = DirFromAngle(angle);
            Vector3 point = origin + dir * _viewRadius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
#endif
    #endregion
}
