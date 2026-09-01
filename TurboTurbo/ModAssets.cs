using System.IO;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Loads the mod's asset bundle (built and deployed via WorkBench:
/// TurboTurbo/Build and Deploy) and exposes its shaders. The bundle is
/// expected in the mod directory, next to info.json.
///
/// Fail-fast: any problem throws out of Main.Load, so UMM flags the mod as
/// errored instead of running with missing visuals.
/// </summary>
internal static class ModAssets
{
    private const string BundleName = "turboturbo_assets";
    private const string HeatShimmerAssetPath = "Assets/Shimmer/HeatShimmer.shader";
    private const string SmokeAssetPath = "Assets/Shimmer/SmokeShader.shader";

    internal static Shader HeatShimmerShader { get; private set; }
    internal static Shader SmokeShader { get; private set; }

    internal static void Load(string modDirectory, Logger log)
    {
        string bundlePath = Path.Combine(modDirectory, BundleName);
        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException(
                $"asset bundle '{BundleName}' not found in '{modDirectory}' - " +
                "build and deploy it via WorkBench: TurboTurbo/Build and Deploy");
        }

        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            throw new IOException($"asset bundle '{bundlePath}' failed to load");
        }

        HeatShimmerShader = bundle.LoadAsset<Shader>(HeatShimmerAssetPath);
        SmokeShader = bundle.LoadAsset<Shader>(SmokeAssetPath);
        if (HeatShimmerShader == null || SmokeShader == null)
        {
            throw new FileNotFoundException(
                $"asset bundle '{BundleName}' does not contain the expected shaders " +
                $"('{HeatShimmerAssetPath}', '{SmokeAssetPath}') - rebuild via WorkBench: TurboTurbo/Build and Deploy");
        }

        log.LogInfo($"[assets] bundle loaded from '{bundlePath}'");
    }
}
