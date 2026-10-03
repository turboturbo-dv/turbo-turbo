using TurboTurbo.Configuration;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Writes profiles into, and reloads them from, the authored target mod.</summary>
internal static class AuthoringService
{
    public static Error? Write(LocoProfile profile)
    {
        var target = SettingsStore.Current.AuthoringTargetModId;
        if (string.IsNullOrEmpty(target)) return new Error("no authoring target mod selected");

        var entry = ModRegistry.Find(target);
        if (entry == null || string.IsNullOrEmpty(entry.Path)) return new Error($"target mod '{target}' not found");

        var error = profile.Normalize();
        return error ?? ModProfileWriter.Write(profile, entry.Path);
    }

    public static bool HasProfile(string liveryId) =>
        ProfileService.Mod.IsFrom(liveryId, SettingsStore.Current.AuthoringTargetModId);

    public static Error? Delete(string liveryId)
    {
        var target = SettingsStore.Current.AuthoringTargetModId;
        if (string.IsNullOrEmpty(target)) return new Error("no authoring target mod selected");

        var entry = ModRegistry.Find(target);
        if (entry == null || string.IsNullOrEmpty(entry.Path)) return new Error($"target mod '{target}' not found");

        var error = ModProfileWriter.Delete(liveryId, entry.Path);
        if (error != null) return error;

        ReloadTarget();
        return null;
    }

    public static void ReloadTarget()
    {
        var target = SettingsStore.Current.AuthoringTargetModId;
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        var entry = ModRegistry.Find(target);
        if (string.IsNullOrEmpty(entry?.Path))
        {
            return;
        }

        var source = new ProfileLoader.ModSource(entry.Info.Id, entry.Info.DisplayName, entry.Enabled, entry.Path);
        var loaded = ProfileLoader.LoadModProfile(source, SettingsStore.ModId);

        var changed = ProfileService.Mod.RemoveAllFrom(target);
        foreach (var pair in loaded)
        {
            ProfileService.Mod.Put(pair.Key, pair.Value);
            if (!changed.Contains(pair.Key)) changed.Add(pair.Key);
        }
    }
}
