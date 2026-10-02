using System.Collections.Generic;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Profiles registered in code.</summary>
internal sealed class BuiltInProfileSource : IProfileSource
{
    private readonly Dictionary<string, LocoProfile> _profiles = new();

    public ProfileTier Tier => ProfileTier.BuiltIn;

    public void Register(string liveryId, LocoProfile profile) => _profiles[liveryId] = profile;

    public LocoProfile Get(string liveryId) => _profiles.TryGetValue(liveryId, out var profile) ? profile : null;

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
