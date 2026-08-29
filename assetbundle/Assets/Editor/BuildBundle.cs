using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildBundle
{
    [MenuItem("TurboTurbo/Build Bundle")]
    public static void Build()
    {
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "turboturbo_assets",
                assetNames = new[] { "Assets/TurboTurbo/HeatShimmer.shader" },
            },
        };

        const string outputPath = "AssetBundles";
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        BuildPipeline.BuildAssetBundles(
            outputPath,
            builds,
            BuildAssetBundleOptions.UncompressedAssetBundle,
            BuildTarget.StandaloneWindows64);

        Debug.Log("Turboturbo bundle build complete.");
    }

    [MenuItem("TurboTurbo/Verify Bundle")]
    public static void Verify()
    {
        var bundle = AssetBundle.LoadFromFile("AssetBundles/turboturbo_assets");
        if (bundle == null)
        {
            Debug.LogError("VERIFY FAILED: bundle did not load");
            return;
        }
        var shader = bundle.LoadAsset<Shader>("Assets/TurboTurbo/HeatShimmer.shader");
        Debug.Log($"VERIFY OK: shader={(shader != null ? shader.name : "NULL")}, supported={(shader != null && shader.isSupported)}");
        bundle.Unload(false);
    }
}
