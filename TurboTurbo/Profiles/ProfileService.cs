using System.Linq;

using TurboTurbo.Configuration;
using TurboTurbo.Profiles.Storage;

using UnityModManagerNet;

namespace TurboTurbo.Profiles;

/// <summary>
/// Source of truth for profile resolution. Holds all profile sources alongside a resolver
/// to determine which profile should be applied to a locomotive.
/// </summary>
internal static class ProfileService
{
    public static BuiltInProfileSource BuiltIn { get; } = new();

    public static ModProfileSource Mod { get; } = new();

    public static UserProfileSource User { get; } = new();

    private static readonly ProfileResolver Resolver = new(User, Mod, BuiltIn);

    public static void Initialize()
    {
        User.Load(SettingsStore.Current);

        var entries = UnityModManager.modEntries;
        var sources = entries == null
            ? []
            : entries.Select(e => new ProfileLoader.ModSource(e.Info.Id, e.Info.DisplayName, e.Enabled, e.Path));
        Mod.Replace(ProfileLoader.LoadModProfiles(sources, SettingsStore.ModId));
    }

    public static ProfileResolution Resolve(string liveryId, ResolutionMode mode) => Resolver.Resolve(liveryId, mode);
}
