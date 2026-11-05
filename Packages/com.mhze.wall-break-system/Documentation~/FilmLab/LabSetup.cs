using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Lab only: makes sure a URP pipeline asset exists and is the default, so the lab renders like the game.</summary>
[InitializeOnLoad]
public static class LabSetup
{
    static LabSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Lab/Ensure URP")]
    public static void Ensure()
    {
        const string dir = "Assets/LabURP";
        const string assetPath = dir + "/LabURP.asset";
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "LabURP");
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, dir + "/LabRenderer.asset");
            asset = UniversalRenderPipelineAsset.Create(data);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
        }
        if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
        if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
    }
}
