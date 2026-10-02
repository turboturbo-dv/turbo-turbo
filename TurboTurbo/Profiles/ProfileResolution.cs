using System.Collections.Generic;
using System.Linq;

namespace TurboTurbo.Profiles;

/// <summary>
/// The result of resolving one livery: the candidate stack (highest precedence
/// first), which profile actually runs, and the mode that produced it.
/// </summary>
internal sealed class ProfileResolution
{
    public ProfileResolution(ResolutionMode mode, IReadOnlyList<ProfileEntry> present, ProfileEntry? effective)
    {
        Mode = mode;
        Present = present;
        Effective = effective;
    }

    public ResolutionMode Mode { get; }

    /// <summary>Present candidates, highest precedence first.</summary>
    public IReadOnlyList<ProfileEntry> Present { get; }

    /// <summary>The profile that actually runs, or null when nothing applies.</summary>
    public ProfileEntry? Effective { get; }

    /// <summary>The highest-priority present candidate, or null when none exist.</summary>
    public ProfileEntry? Winner => Present.Count > 0 ? Present[0] : null;

    /// <summary>The present candidates the winner overrides.</summary>
    public IReadOnlyList<ProfileEntry> Shadowed => Present.Skip(1).ToArray();

    /// <summary>True when the winner is present but disabled.</summary>
    public bool Disabled => Winner.HasValue && !Winner.Value.Profile.Enabled;
}
