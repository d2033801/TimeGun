using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// NavMesh 动态烘焙管理器
/// 定期重新烘焙 NavMeshSurface，以支持动态场景变化
/// </summary>
public class NavMeshDynamicBaker : MonoBehaviour
{
    [Header("烘焙设置")]
    [Tooltip("烘焙间隔（秒）   ")]
    [SerializeField] private float bakeInterval = 2f;
    
    [Tooltip("是否在启动时立即烘焙")]
    [SerializeField] private bool bakeOnStart = true;
    
    [Tooltip("是否启用自动烘焙")]
    [SerializeField] private bool enableAutoBake = true;
    
    [Header("引用")]
    [Tooltip("要烘焙的 NavMeshSurface（如果为空则自动获取）")]
    [SerializeField] private NavMeshSurface navMeshSurface;
    
    // 计时器
    private float timer = 0f;
    
    void Start()
    {
        // 自动获取 NavMeshSurface 组件
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            
            if (navMeshSurface == null)
            {
                Debug.LogError($"[{gameObject.name}] NavMeshDynamicBaker: 未找到 NavMeshSurface 组件！");
                enabled = false;
                return;
            }
        }
        
        // 启动时立即烘焙
        if (bakeOnStart)
        {
            BakeNavMesh();
        }
        
        Debug.Log($"[{gameObject.name}] NavMeshDynamicBaker 已启动，烘焙间隔: {bakeInterval} 秒");
    }
    
    void Update()
    {
        // 如果未启用自动烘焙，则退出
        if (!enableAutoBake)
            return;
        
        // 更新计时器
        timer += Time.deltaTime;
        
        // 达到烘焙间隔时重新烘焙
        if (timer >= bakeInterval)
        {
            BakeNavMesh();
            timer = 0f; // 重置计时器
        }
    }
    
    /// <summary>
    /// 执行 NavMesh 烘焙
    /// </summary>
    public void BakeNavMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogWarning($"[{gameObject.name}] NavMeshDynamicBaker: NavMeshSurface 为空，无法烘焙");
            return;
        }
        
        // 清除旧的 NavMesh 数据
        navMeshSurface.RemoveData();
        
        // 重新烘焙 NavMesh
        navMeshSurface.BuildNavMesh();
        
        Debug.Log($"[{gameObject.name}] NavMesh 已重新烘焙 (Time: {Time.time:F2}s)");
    }
    
    /// <summary>
    /// 手动触发烘焙（可以从外部调用）
    /// </summary>
    public void ManualBake()
    {
        BakeNavMesh();
        timer = 0f; // 重置计时器
    }
    
    /// <summary>
    /// 启用自动烘焙
    /// </summary>
    public void EnableAutoBake()
    {
        enableAutoBake = true;
        timer = 0f;
        Debug.Log($"[{gameObject.name}] NavMeshDynamicBaker: 自动烘焙已启用");
    }
    
    /// <summary>
    /// 禁用自动烘焙
    /// </summary>
    public void DisableAutoBake()
    {
        enableAutoBake = false;
        Debug.Log($"[{gameObject.name}] NavMeshDynamicBaker: 自动烘焙已禁用");
    }
    
    /// <summary>
    /// 设置烘焙间隔
    /// </summary>
    /// <param name="interval">新的烘焙间隔（秒）</param>
    public void SetBakeInterval(float interval)
    {
        if (interval <= 0)
        {
            Debug.LogWarning($"[{gameObject.name}] NavMeshDynamicBaker: 烘焙间隔必须大于 0");
            return;
        }
        
        bakeInterval = interval;
        timer = 0f; // 重置计时器
        Debug.Log($"[{gameObject.name}] NavMeshDynamicBaker: 烘焙间隔已设置为 {interval} 秒");
    }
    
    void OnDestroy()
    {
        Debug.Log($"[{gameObject.name}] NavMeshDynamicBaker 已销毁");
    }
}
