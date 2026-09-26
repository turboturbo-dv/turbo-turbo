using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Configuration;

using UnityModManagerNet;

namespace TurboTurbo.Profiles;

/// <summary>
/// Authoritative source for loco profiles. Queries, in order of precedence:
/// user-defined profiles, mod-defined profiles, then built-in configuration.
/// While authoring a mod, user profiles are ignored and the authored mod's
/// profiles take precedence.
/// </summary>
internal static class ProfileRepository
{
    private static readonly Dictionary<string, LocoProfile> UserProfiles = new();
    private static readonly Dictionary<string, ProfileLoader.ModProfile> ModProfiles = new();

    internal static void Initialize()
    {
        SetUserProfiles(ProfileLoader.LoadUserProfiles(SettingsStore.Current?.LocoProfiles));

        var entries = UnityModManager.modEntries;
        var sources = entries == null
            ? Enumerable.Empty<ProfileLoader.ModSource>()
            : entries.Select(e => new ProfileLoader.ModSource(e.Info.Id, e.Info.DisplayName, e.Enabled, e.Path));
        SetSuppliedProfiles(ProfileLoader.LoadModProfiles(sources, SettingsStore.ModId));
    }

    /// <summary>True while a target mod is selected for authoring.</summary>
    private static bool IsAuthoring => SettingsStore.Current?.IsAuthoring == true;

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
        if (IsAuthoring) return TryGetAuthoringProfile(liveryId);

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

    /// <summary>
    /// Resolves the profile to serve while authoring: the target mod's profile for
    /// the livery wins, otherwise the ordinary mod tier, otherwise built-in. User
    /// profiles are ignored entirely.
    /// </summary>
    internal static LocoProfile TryGetAuthoringProfile(string liveryId)
    {
        var target = SettingsStore.Current.AuthoringTargetModId;

        if (ModProfiles.TryGetValue(liveryId, out var authored)
            && authored.SourceId == target
            && authored.Profile.Enabled)
            return authored.Profile.Clone();

        if (ModProfiles.TryGetValue(liveryId, out var supplied) && supplied.Profile.Enabled)
            return supplied.Profile.Clone();

        return Controller.TryGetConfiguration(liveryId)?.Clone();
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

    internal static string TryGetModName(string liveryId)
    {
        return ModProfiles.TryGetValue(liveryId, out var profile) ? profile.ModName : null;
    }

    internal static ProfileStatus TryGetStatus(string liveryId)
    {
        var builtIn = Controller.TryGetConfiguration(liveryId);

        if (IsAuthoring)
        {
            if (ModProfiles.TryGetValue(liveryId, out var authored)
                && authored.SourceId == SettingsStore.Current.AuthoringTargetModId
                && authored.Profile.Enabled)
                return ProfileStatus.Resolve(null, authored.Profile, authored.ModName, builtIn);

            return ModProfiles.TryGetValue(liveryId, out var supplied) && supplied.Profile.Enabled
                ? ProfileStatus.Resolve(null, supplied.Profile, supplied.ModName, builtIn)
                : ProfileStatus.Resolve(null, null, null, builtIn);
        }

        UserProfiles.TryGetValue(liveryId, out var user);
        var hasSupplied = ModProfiles.TryGetValue(liveryId, out var mod);
        return ProfileStatus.Resolve(
            user,
            hasSupplied ? mod.Profile : null,
            hasSupplied ? mod.ModName : null,
            builtIn);
    }

    internal static ValidationError? SaveProfile(LocoProfile profile)
    {
        var error = profile.Complete();
        if (error != null) return error;

        var stored = SettingsStore.Current.LocoProfiles;
        var index = stored.FindIndex(p => p.LiveryId == profile.LiveryId);
        if (index >= 0) stored[index] = profile;
        else stored.Add(profile);

        UserProfiles[profile.LiveryId] = profile;
        SettingsStore.Save();
        return null;
    }

    /// <summary>Writes the profile into the authored mod's config, returning an error or null.</summary>
    internal static string WriteToAuthoringMod(LocoProfile profile)
    {
        var target = SettingsStore.Current.AuthoringTargetModId;
        if (string.IsNullOrEmpty(target)) return "no authoring target mod selected";

        var entry = ModRegistry.Find(target);
        if (entry == null || string.IsNullOrEmpty(entry.Path)) return $"target mod '{target}' not found";

        var error = profile.Complete();
        if (error != null) return error.Value.Message;

        return ProfileWriter.Write(profile, entry.Path);
    }

    /// <summary>True when the authored mod supplies a profile for the livery.</summary>
    internal static bool ModSuppliesAuthoringLivery(string liveryId) =>
        ModProfiles.TryGetValue(liveryId, out var profile) && profile.SourceId == SettingsStore.Current.AuthoringTargetModId;

    /// <summary>Removes the profile for <paramref name="liveryId"/> from the authored mod, returning an error or null.</summary>
    internal static string DeleteFromAuthoringMod(string liveryId)
    {
        var target = SettingsStore.Current.AuthoringTargetModId;
        if (string.IsNullOrEmpty(target)) return "no authoring target mod selected";

        var entry = ModRegistry.Find(target);
        if (entry == null || string.IsNullOrEmpty(entry.Path)) return $"target mod '{target}' not found";

        var error = ProfileWriter.Delete(liveryId, entry.Path);
        if (error != null) return error;

        ReloadAuthoringMod();
        return null;
    }

    /// <summary>Reloads the authored mod's profiles from disk, returning the affected livery ids.</summary>
    internal static List<string> ReloadAuthoringMod()
    {
        var changed = new List<string>();
        var target = SettingsStore.Current.AuthoringTargetModId;
        if (string.IsNullOrEmpty(target)) return changed;

        var entry = ModRegistry.Find(target);
        if (entry == null || string.IsNullOrEmpty(entry.Path)) return changed;

        var source = new ProfileLoader.ModSource(entry.Info.Id, entry.Info.DisplayName, entry.Enabled, entry.Path);
        var loaded = ProfileLoader.LoadModProfile(source, SettingsStore.ModId);

        foreach (var livery in ModProfiles.Where(pair => pair.Value.SourceId == target).Select(pair => pair.Key).ToList())
        {
            ModProfiles.Remove(livery);
            changed.Add(livery);
        }

        foreach (var pair in loaded)
        {
            ModProfiles[pair.Key] = pair.Value;
            if (!changed.Contains(pair.Key)) changed.Add(pair.Key);
        }

        return changed;
    }

    internal static bool DeleteProfile(string liveryId)
    {
        var removed = UserProfiles.Remove(liveryId);
        SettingsStore.Current.LocoProfiles.RemoveAll(p => p.LiveryId == liveryId);
        if (removed) SettingsStore.Save();
        return removed;
    }

    internal static bool SetEnabled(string liveryId, bool enabled)
    {
        if (!UserProfiles.TryGetValue(liveryId, out var profile)) return false;
        profile.Enabled = enabled;
        SettingsStore.Save();
        return true;
    }
}