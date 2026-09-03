using System;
using System.IO;

using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Manages the mod's asset bundle, reloading when necessary and providing access to the assets contained.
/// </summary>
internal static class ModAssets
{
    private const string BundleName = "turboturbo_assets";
    private const string HeatShimmerAssetPath = "Assets/Shimmer/HeatShimmer.shader";
    private const string SmokeAssetPath = "Assets/Shimmer/SmokeShader.shader";

    private static readonly Logger Log = TurboTurbo.Log.ForContext("assets");

    private static string _modDirectory;
    private static AssetBundle _bundle;
    private static bool _everLoaded;

    public static Shader HeatShimmerShader { get; private set; }
    public static Shader SmokeShader { get; private set; }

    public static bool ShadersValid => HeatShimmerShader != null && SmokeShader != null;

    /// <summary>
    /// Initializes the asset bundle loader with the mod directory, and loads the assets.
    /// Needs to be called once at startup, so the loader knows where to look.
    /// </summary>
    public static void Initialize(string modDirectory)
    {
        _modDirectory = modDirectory;

        // throwing here ensures our mod will fail to load, which is better than failing silently as it doesn't leave
        // the mod in a half-broken state
        EnsureLoaded(throwOnError: true);
    }

    /// <summary>
    /// Ensures the assets are loaded. They will be destroyed during a game reload, so this will reload them when that
    /// happens. Does nothing if the assets are still valid.
    /// </summary>
    public static void EnsureLoaded()
    {
        EnsureLoaded(throwOnError: false);
    }

    private static void EnsureLoaded(bool throwOnError)
    {
        if (ShadersValid) return;

        var reload = _everLoaded;
        try
        {
            // after UnloadAllAssetBundles the handle is destroyed already;
            // Unload(true) is the correct teardown in any other stale case
            if (_bundle != null)
            {
                _bundle.Unload(true);
                _bundle = null;
            }

            var bundlePath = Path.Combine(_modDirectory, BundleName);
            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException(
                    $"asset bundle '{BundleName}' not found in '{_modDirectory}', make sure the mod is installed correctly ");
            }

            _bundle = AssetBundle.LoadFromFile(bundlePath);
            if (_bundle == null)
            {
                throw new IOException($"asset bundle '{bundlePath}' failed to load");
            }

            HeatShimmerShader = _bundle.LoadAsset<Shader>(HeatShimmerAssetPath);
            SmokeShader = _bundle.LoadAsset<Shader>(SmokeAssetPath);
            if (!ShadersValid)
            {
                throw new FileNotFoundException(
                    $"asset bundle '{BundleName}' does not contain the expected shaders " +
                    $"('{HeatShimmerAssetPath}', '{SmokeAssetPath}')");
            }

            _everLoaded = true;
            if (reload)
            {
                Log.Info("shaders lost, bundle reloaded");
            }
            else
            {
                Log.Info($"bundle loaded from '{bundlePath}'");
            }
        }
        catch (Exception e)
        {
            HeatShimmerShader = null;
            SmokeShader = null;
            if (throwOnError)
            {
                throw;
            }

            Log.Exception(e);
            Log.Error($"failed to load asset bundle '{BundleName}': {e.Message}");
        }
    }
}