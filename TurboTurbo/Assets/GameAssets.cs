using System.Linq;

using UnityEngine;

namespace TurboTurbo.Assets;

/// <summary>
/// Runtime access to vanilla assets we build on.
/// </summary>
internal static class GameAssets
{
    private const string SmokeAtlasName = "Cloud01_8x8";
    private static readonly Logger Log = TurboTurbo.Log.ForContext("assets");
    private static bool _warnedMissing;

    internal static Texture2D SmokeAtlas { get; private set; }

    /// <summary>
    /// Resiliently resolves the game's assets, provided they are available.
    /// </summary>
    internal static void EnsureLoaded()
    {
        if (SmokeAtlas != null) return;

        SmokeAtlas = Resources.FindObjectsOfTypeAll<Texture2D>()
            .FirstOrDefault(t => t.name == SmokeAtlasName);

        if (SmokeAtlas == null)
        {
            if (!_warnedMissing)
            {
                _warnedMissing = true;
                Log.Error($"smoke atlas '{SmokeAtlasName}' could not be loaded");
            }
            return;
        }

        Log.Info($"smoke atlas '{SmokeAtlasName}' loaded from game assets");
    }
}