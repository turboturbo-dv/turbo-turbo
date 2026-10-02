namespace TurboTurbo.Profiles;

/// <summary>One tier of candidate profiles.</summary>
internal interface IProfileSource
{
    ProfileTier Tier { get; }

    bool TryGet(string liveryId, out ProfileEntry entry);
}
