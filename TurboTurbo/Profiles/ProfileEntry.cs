namespace TurboTurbo.Profiles;

/// <summary>One profile together with where it came from.</summary>
internal record struct ProfileEntry(LocoProfile Profile, ProfileTier Tier, string SourceId, string Origin);
