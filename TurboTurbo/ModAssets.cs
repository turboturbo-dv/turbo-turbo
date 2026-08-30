using System.IO;
using System.Reflection;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Loads the mod's asset bundle (built from the WorkBench Unity project)
/// and exposes its shaders. The bundle is expected next to the plugin DLL,
/// e.g. BepInEx/plugins/turboturbo_assets.
/// </summary>
internal static class ModAssets
{
    private const string ShaderAssetPath = "Assets/Shimmer/HeatShimmer.shader";

    private static AssetBundle _bundle;
    private static Shader _heatShimmerShader;

    internal static Shader HeatShimmerShader
    {
        get
        {
            if (_heatShimmerShader == null && _bundle != null)
            {
                _heatShimmerShader = _bundle.LoadAsset<Shader>(ShaderAssetPath);
            }
            return _heatShimmerShader;
        }
    }

    internal static void Load()
    {
        if (_bundle != null) return;

        string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string bundlePath = Path.Combine(dllDir, "turboturbo_assets");
        if (!File.Exists(bundlePath))
        {
            TurboModel.Log.LogWarning("asset bundle 'turboturbo_assets' not found next to the plugin DLL - " +
                                      "heat shimmer shader unavailable (build it via WorkBench: TurboTurbo -> Build Bundle)");
            return;
        }

        _bundle = AssetBundle.LoadFromFile(bundlePath);
        if (_bundle != null)
        {
            TurboModel.Log.LogInfo($"asset bundle loaded: {bundlePath}");
        }
        else
        {
            TurboModel.Log.LogWarning($"asset bundle exists but failed to load: {bundlePath}");
        }
    }
}
