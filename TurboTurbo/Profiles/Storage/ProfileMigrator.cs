using TurboTurbo.Profiles.Storage.V1;

namespace TurboTurbo.Profiles.Storage;

/// <summary>
/// Upgrades a deserialized profile to the current persistence schema version before it is mapped
/// to the runtime model. V1 is the only schema so far, so this is currently a pass-through.
/// </summary>
internal static class ProfileMigrator
{
    public static LocoProfileXml Migrate(LocoProfileXml profile)
    {
        return profile;
    }
}
