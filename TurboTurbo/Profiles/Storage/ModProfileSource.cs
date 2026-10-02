using System.Collections.Generic;
using System.Linq;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Profiles loaded from TurboConfig.xml, located in another mod.</summary>
internal sealed class ModProfileSource : IProfileSource
{
    private readonly Dictionary<string, ProfileLoader.ModProfile> _profiles = new();

    public ProfileTier Tier => ProfileTier.Mod;

    public void Replace(Dictionary<string, ProfileLoader.ModProfile> profiles)
    {
        _profiles.Clear();
        if (profiles == null) return;

        foreach (var pair in profiles) _profiles[pair.Key] = pair.Value;
    }

    public void Put(string liveryId, ProfileLoader.ModProfile profile) => _profiles[liveryId] = profile;

    public List<string> RemoveAllFrom(string sourceId)
    {
        var removed = _profiles.Where(pair => pair.Value.SourceId == sourceId).Select(pair => pair.Key).ToList();
        foreach (var id in removed) _profiles.Remove(id);
        return removed;
    }

    public LocoProfile GetProfile(string liveryId) =>
        _profiles.TryGetValue(liveryId, out var profile) ? profile.Profile : null;

    public bool IsFrom(string liveryId, string sourceId) =>
        _profiles.TryGetValue(liveryId, out var profile) && profile.SourceId == sourceId;

    public bool TryGet(string liveryId, out ProfileEntry entry)
    {
        if (_profiles.TryGetValue(liveryId, out var profile))
        {
            entry = new ProfileEntry(profile.Profile, Tier, profile.SourceId, profile.ModName);
            return true;
        }

        entry = default;
        return false;
    }
}
