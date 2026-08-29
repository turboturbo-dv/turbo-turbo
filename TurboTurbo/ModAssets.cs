using System.IO;
using System.Reflection;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Loads the mod's asset bundle (built from the assetbundle/ Unity project)
/// and exposes its shaders. The bundle is expected next to the plugin DLL,
/// e.g. BepInEx/plugins/turboturbo_assets.
/// </summary>
internal static class ModAssets
{
    private static AssetBundle _bundle;
    private static Shader _heatShimmerShader;

    internal static Shader HeatShimmerShader
    {
        get
        {
            if (_heatShimmerShader == null && _bundle != null)
            {
                _heatShimmerShader = _bundle.LoadAsset<Shader>("Assets/TurboTurbo/HeatShimmer.shader");
            }
            return _heatShimmerShader;
        }
    }

    internal static void Load()
    {
        if (_bundle != null) return;

        string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        foreach (string candidate in new[]
        {
            Path.Combine(dllDir, "turboturbo_assets"),
            Path.Combine(dllDir, "TurboTurbo", "turboturbo_assets"),
        })
        {
            if (!File.Exists(candidate)) continue;
            _bundle = AssetBundle.LoadFromFile(candidate);
            if (_bundle != null)
            {
                TurboModel.Log.LogInfo($"asset bundle loaded: {candidate}");
                break;
            }
            TurboModel.Log.LogWarning($"asset bundle exists but failed to load: {candidate}");
        }

        if (_bundle == null)
        {
            TurboModel.Log.LogWarning("asset bundle 'turboturbo_assets' not found next to the plugin DLL - " +
                                      "heat shimmer shader unavailable (build it with assetbundle/build.ps1)");
        }
    }
}
