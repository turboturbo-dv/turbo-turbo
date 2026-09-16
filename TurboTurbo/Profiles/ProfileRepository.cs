using System.Collections.Generic;

using TurboTurbo.Configuration;

using UnityModManagerNet;

namespace TurboTurbo.Profiles;

/// <summary>
/// Authoritative source for loco profiles. Queries, in order of precedence:
/// user-defined profiles, mod-defined profiles, then built-in configuration.
/// </summary>
internal static class ProfileRepository
{
    private static readonly Dictionary<string, LocoProfile> UserProfiles = new();
    private static readonly Dictionary<string, ProfileLoader.ModProfile> ModProfiles = new();

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
    }

    internal static void SetSuppliedProfiles(Dictionary<string, ProfileLoader.ModProfile> supplied)
    {
        ModProfiles.Clear();
        foreach (var entry in supplied) ModProfiles[entry.Key] = entry.Value;
    }

    internal static LocoProfile TryGetProfile(TrainCar car)
    {
        var liveryId = car.carLivery.id;
        LocoProfile profile = null;

        if (UserProfiles.TryGetValue(liveryId, out var user))
        {
            // note: a disabled user profile still overrides a mod profile, that's deliberate
            if (user.Enabled) profile = user;
        }
        else if (ModProfiles.TryGetValue(liveryId, out var supplied))
        {
            // note: a disabled mod profile still overrides a built-in profile, that's deliberate too
            if (supplied.Profile.Enabled) profile = supplied.Profile;
        }
        else
        {
            profile = Controller.TryGetConfiguration(liveryId);
        }

        // a fresh instance per query, so callers may tune it freely
        return profile?.Clone();
    }

    internal static LocoProfile TryGetUserProfile(string liveryId)
    {
        UserProfiles.TryGetValue(liveryId, out var profile);
        return profile;
    }

    internal static LocoProfile TryGetModProfile(string liveryId)
    {
        return ModProfiles.TryGetValue(liveryId, out var profile) ? profile.Profile : null;
    }

    internal static ValidationError? SaveProfile(LocoProfile profile)
    {
        var error = profile.Complete();
        if (error != null) return error;

        var stored = _settings.LocoProfiles;
        var index = stored.FindIndex(p => p.LiveryId == profile.LiveryId);
        if (index >= 0) stored[index] = profile;
        else stored.Add(profile);

        UserProfiles[profile.LiveryId] = profile;
        Persist();
        return null;
    }

    internal static bool DeleteProfile(string liveryId)
    {
        var removed = UserProfiles.Remove(liveryId);
        _settings.LocoProfiles.RemoveAll(p => p.LiveryId == liveryId);
        if (removed) Persist();
        return removed;
    }

    internal static bool SetEnabled(string liveryId, bool enabled)
    {
        if (!UserProfiles.TryGetValue(liveryId, out var profile)) return false;
        profile.Enabled = enabled;
        Persist();
        return true;
    }

    private static void Persist()
    {
        if (_entry != null) _settings.Save(_entry);
    }
}