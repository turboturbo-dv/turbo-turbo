using System.Collections.Generic;

namespace TurboTurbo.Profiles;

/// <summary>Root of a mod-supplied TurboConfig.xml.</summary>
public sealed class TurboConfig
{
    public List<LocoProfile> LocoProfiles = new();
}