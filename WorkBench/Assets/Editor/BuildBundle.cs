using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildBundle
{
    private const string ShaderAsset = "Assets/Shimmer/HeatShimmer.shader";
    private const string OutputPath = "AssetBundles";

    [MenuItem("TurboTurbo/Build Bundle")]
    public static void Build()
    {
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "turboturbo_assets",
                assetNames = new[] { ShaderAsset },
            },
        };

        if (!Directory.Exists(OutputPath))
        {
            Directory.CreateDirectory(OutputPath);
        }

        BuildPipeline.BuildAssetBundles(
            OutputPath,
            builds,
            BuildAssetBundleOptions.UncompressedAssetBundle,
            BuildTarget.StandaloneWindows64);

        Debug.Log("Turboturbo bundle build complete.");
    }

    [MenuItem("TurboTurbo/Verify Bundle")]
    public static void Verify()
    {
        var bundle = AssetBundle.LoadFromFile($"{OutputPath}/turboturbo_assets");
        if (bundle == null)
        {
            Debug.LogError("VERIFY FAILED: bundle did not load");
            return;
        }
        var shader = bundle.LoadAsset<Shader>(ShaderAsset);
        Debug.Log($"VERIFY OK: shader={(shader != null ? shader.name : "NULL")}, supported={(shader != null && shader.isSupported)}");
        bundle.Unload(false);
    }
}
