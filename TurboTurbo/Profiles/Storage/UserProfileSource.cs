using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Configuration;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Profiles the player created in-game.</summary>
internal sealed class UserProfileSource : IProfileSource
{
    private readonly Dictionary<string, LocoProfile> _profiles = new();

    public ProfileTier Tier => ProfileTier.User;

    public IReadOnlyCollection<LocoProfile> All => _profiles.Values;

    public void Replace(IEnumerable<LocoProfile> profiles)
    {
        _profiles.Clear();
        if (profiles == null) return;

        foreach (var profile in profiles)
        {
            if (profile != null) _profiles[profile.LiveryId] = profile;
        }
    }

    public void Put(LocoProfile profile) => _profiles[profile.LiveryId] = profile;

    public bool Remove(string liveryId) => _profiles.Remove(liveryId);

    public LocoProfile Get(string liveryId) => _profiles.TryGetValue(liveryId, out var profile) ? profile : null;

    /// <summary>Reloads the in-memory set from persisted settings.</summary>
    public void Load(Settings settings) => Replace(ProfileLoader.LoadUserProfiles(settings?.LocoProfiles).Values);

    /// <summary>Validates, stores, and persists a profile. Returns an error or null.</summary>
    public Error? Save(LocoProfile profile)
    {
        var error = profile.Normalize();
        if (error != null) return error;

        var stored = SettingsStore.Current.LocoProfiles;
        var index = stored.FindIndex(p => p.LiveryId == profile.LiveryId);
        if (index >= 0) stored[index] = profile;
        else stored.Add(profile);

        Put(profile);
        SettingsStore.Save();
        return null;
    }

    /// <summary>Removes a profile from memory and settings, returning whether one was removed.</summary>
    public bool DeleteProfile(string liveryId)
    {
        var removed = Remove(liveryId);
        SettingsStore.Current.LocoProfiles.RemoveAll(p => p.LiveryId == liveryId);
        if (removed) SettingsStore.Save();
        return removed;
    }

    public bool SetEnabled(string liveryId, bool enabled)
    {
        var profile = Get(liveryId);
        if (profile == null) return false;

        profile.Enabled = enabled;
        SettingsStore.Save();
        return true;
    }

    public bool TryGet(string liveryId, out ProfileEntry entry)
    {
        if (_profiles.TryGetValue(liveryId, out var profile))
        {
            entry = new ProfileEntry(profile, Tier, null, null);
            return true;
        }

        entry = default;
        return false;
    }
}
