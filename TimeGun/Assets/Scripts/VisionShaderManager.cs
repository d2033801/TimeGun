using UnityEngine;

/// <summary>
/// 用于在打包时强制引用敌人视野可视化所需的Shader，防止被剥离。
/// 将此预制件的实例放置在 "Assets/Resources" 文件夹下。
/// </summary>
[CreateAssetMenu(fileName = "VisionShaderManager", menuName = "Enemy/Vision Shader Manager")]
public class VisionShaderManager : ScriptableObject
{
    [Header("URP Shaders")]
    [Tooltip("URP的Unlit Shader，用于线框和不透明部分")]
    public Shader urpUnlitShader;

    [Header("Fallback Shaders")]
    [Tooltip("备用的内置Unlit Shader")]
    public Shader fallbackUnlitShader;
    
    [Tooltip("备用的内置透明Shader")]
    public Shader fallbackTransparentShader;

    private static VisionShaderManager _instance;

    public static VisionShaderManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<VisionShaderManager>("VisionShaderManager");
                if (_instance == null)
                {
                    Debug.LogError("[VisionShaderManager] 无法在Resources文件夹中找到'VisionShaderManager'实例！请创建并配置它。");
                }
            }
            return _instance;
        }
    }

    public Shader GetURPUnlit()
    {
        if (urpUnlitShader == null)
        {
            Debug.LogWarning("[VisionShaderManager] URP Unlit Shader 未配置，尝试使用备用Shader。");
            return GetFallbackUnlit();
        }
        return urpUnlitShader;
    }

    public Shader GetFallbackUnlit()
    {
        if (fallbackUnlitShader == null)
        {
            Debug.LogError("[VisionShaderManager] 备用 Unlit Shader 也未配置！线框将不可见。");
            return null;
        }
        return fallbackUnlitShader;
    }
    
    public Shader GetFallbackTransparent()
    {
        if (fallbackTransparentShader == null)
        {
            Debug.LogError("[VisionShaderManager] 备用 Transparent Shader 也未配置！体积将不可见。");
            return null;
        }
        return fallbackTransparentShader;
    }
}
