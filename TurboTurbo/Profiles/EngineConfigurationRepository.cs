using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Configuration;
using TurboTurbo.Setup;

using UnityModManagerNet;

namespace TurboTurbo.Profiles;

/// <summary>
/// Authoritative source for engine configurations. Queries, in order of precedence:
/// user-defined profiles, mod-defined profiles, then built-in configuration.
/// </summary>
internal static class EngineConfigurationRepository
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("profiles");

    private static readonly Dictionary<string, LocoProfile> UserProfiles = new();
    private static readonly Dictionary<string, ProfileLoader.ModProfile> ModProfiles = new();
    private static readonly Dictionary<string, EngineConfiguration> Cache = new();

    private static Settings _settings;
    private static UnityModManager.ModEntry _entry;

    internal static void Initialize(Settings settings, UnityModManager.ModEntry entry = null)
    {
        _settings = settings;
        _entry = entry;
    }

    internal static void SetUserProfiles(Dictionary<string, LocoProfile> profiles)
    {
        UserProfiles.Clear();
        foreach (var entry in profiles) UserProfiles[entry.Key] = entry.Value;
        Cache.Clear();
    }

    internal static EngineConfiguration? TryGetConfiguration(TrainCar car)
    {
        var liveryId = car.carLivery.id;
        if (UserProfiles.TryGetValue(liveryId, out var profile) && profile.Enabled)
            return GetOrBuild(liveryId, profile);
        if (ModProfiles.TryGetValue(liveryId, out var supplied) && supplied.Profile.Enabled)
            return GetOrBuild(liveryId, supplied.Profile);
        return Controller.TryGetConfiguration(car);
    }

    private static EngineConfiguration GetOrBuild(string liveryId, LocoProfile profile)
    {
        if (!Cache.TryGetValue(liveryId, out var configuration))
        {
            var options = new EngineOptions();
            options.ApplyLocoProfile(profile);
            configuration = options.Build();
            Cache[liveryId] = configuration;
        }
        return configuration;
    }

    internal static LocoProfile GetProfile(string liveryId)
    {
        UserProfiles.TryGetValue(liveryId, out var profile);
        return profile;
    }

    internal static void SetSuppliedProfiles(Dictionary<string, ProfileLoader.ModProfile> supplied)
    {
        ModProfiles.Clear();
        foreach (var entry in supplied) ModProfiles[entry.Key] = entry.Value;
        Cache.Clear();
    }

    internal static LocoProfile GetSuppliedProfile(string liveryId)
    {
        return ModProfiles.TryGetValue(liveryId, out var supplied) ? supplied.Profile : null;
    }

    internal static string SaveProfile(LocoProfile profile)
    {
        var error = profile?.Validate();
        if (error != null) return error;
        var stored = _settings.LocoProfiles;
        var index = stored.FindIndex(p => p.LiveryId == profile.LiveryId);
        if (index >= 0) stored[index] = profile;
        else stored.Add(profile);
        UserProfiles[profile.LiveryId] = profile;
        Cache.Remove(profile.LiveryId);
        Persist();
        return null;
    }

    internal static bool DeleteProfile(string liveryId)
    {
        var removed = UserProfiles.Remove(liveryId);
        _settings.LocoProfiles.RemoveAll(p => p.LiveryId == liveryId);
        Cache.Remove(liveryId);
        if (removed) Persist();
        return removed;
    }

    internal static bool SetEnabled(string liveryId, bool enabled)
    {
        if (!UserProfiles.TryGetValue(liveryId, out var profile)) return false;
        profile.Enabled = enabled;
        Cache.Remove(liveryId);
        Persist();
        return true;
    }

    private static void Persist()
    {
        if (_entry != null) _settings.Save(_entry);
    }
}