using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace TurboTurbo.Profiles;

internal static class ProfileLoader
{
    internal record struct ModSource(string Id, string ModName, bool Enabled, string Path);

    internal record struct ModProfile(LocoProfile Profile, string ModName);

    private static readonly Logger Log = TurboTurbo.Log.ForContext("profiles");
    private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(TurboConfig));

    internal static Dictionary<string, ModProfile> LoadModProfiles(IEnumerable<ModSource> mods, string ownId)
    {
        var loaded = new Dictionary<string, ModProfile>();
        foreach (var mod in mods)
        {
            var config = LoadConfig(mod, ownId);
            if (config == null) continue;
            MergeInto(loaded, mod, config);
        }
        
        Log.Info($"loaded {loaded.Count} loco profiles from mods");
        return loaded;
    }

    internal static Dictionary<string, LocoProfile> LoadUserProfiles(IEnumerable<LocoProfile> stored)
    {
        var loaded = new Dictionary<string, LocoProfile>();
        if (stored == null) return loaded;
        foreach (var profile in stored)
        {
            if (profile == null) continue;
            var error = profile.Validate();
            if (error != null)
            {
                Log.Warn($"skipping loco profile '{profile.LiveryId}': {error}");
                continue;
            }
            loaded[profile.LiveryId] = profile;
            Log.Info($"added loco profile for '{profile.LiveryId}' from user settings");
        }
        Log.Info($"loaded {loaded.Count} loco profiles from user settings");
        return loaded;
    }

    private static TurboConfig LoadConfig(ModSource mod, string ownId)
    {
        if (mod.Id == ownId) return null;
        if (!mod.Enabled) return null;
        var file = Path.Combine(mod.Path, "TurboConfig.xml");
        if (!File.Exists(file)) return null;
        try
        {
            using var stream = File.OpenRead(file);
            var config = (TurboConfig)Serializer.Deserialize(stream);
            return config?.LocoProfiles == null ? null : config;
        }
        catch (Exception e)
        {
            Log.Warn($"skipping malformed TurboConfig.xml in '{mod.ModName}': {e.Message}");
            return null;
        }
    }

    private static void MergeInto(Dictionary<string, ModProfile> merged, ModSource source, TurboConfig config)
    {
        foreach (var profile in config.LocoProfiles)
        {
            if (profile == null) continue;
            var error = profile.Validate();
            if (error != null)
            {
                Log.Warn($"skipping loco profile '{profile.LiveryId}' from '{source.ModName}': {error}");
                continue;
            }
            if (merged.TryGetValue(profile.LiveryId, out var existing))
            {
                Log.Warn($"loco profile for '{profile.LiveryId}' from '{source.ModName}' overrides profile defined by " +
                         $"'{existing.ModName}' (change load order if this is not correct)");
            }
            else
            {
                Log.Info($"added loco profile for '{profile.LiveryId}' from '{source.ModName}'");
            }

            merged[profile.LiveryId] = new ModProfile(profile, source.ModName);
        }
    }
}