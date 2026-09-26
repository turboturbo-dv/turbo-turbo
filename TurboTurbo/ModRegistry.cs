using System.Collections.Generic;
using System.Linq;

using UnityModManagerNet;

namespace TurboTurbo;

/// <summary>
/// Lookup helpers over the UMM mod list.
/// </summary>
internal static class ModRegistry
{
    public static UnityModManager.ModEntry Find(string modId)
    {
        if (string.IsNullOrEmpty(modId)) return null;
        return UnityModManager.modEntries?.FirstOrDefault(e => e.Info.Id == modId);
    }

    public static string DisplayName(string modId) =>
        Find(modId)?.Info.DisplayName ?? modId;

    public static List<UnityModManager.ModEntry> EligibleTargets(string excludeId)
    {
        var mods = new List<UnityModManager.ModEntry>();
        if (UnityModManager.modEntries == null) return mods;

        foreach (var mod in UnityModManager.modEntries)
        {
            if (!mod.Enabled || mod.Info.Id == excludeId) continue;
            if (string.IsNullOrEmpty(mod.Path)) continue;
            mods.Add(mod);
        }

        return mods;
    }
}