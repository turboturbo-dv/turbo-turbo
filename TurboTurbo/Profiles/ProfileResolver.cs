using System.Collections.Generic;

namespace TurboTurbo.Profiles;

/// <summary>
/// Single authority for which profile a livery runs. Normal precedence is
/// user &gt; mod &gt; built-in and a present-but-disabled profile stops the
/// chain; authoring ignores the user tier and skips disabled profiles.
/// </summary>
internal sealed class ProfileResolver
{
    private readonly IProfileSource _user;
    private readonly IProfileSource _mod;
    private readonly IProfileSource _builtIn;

    public ProfileResolver(IProfileSource user, IProfileSource mod, IProfileSource builtIn)
    {
        _user = user;
        _mod = mod;
        _builtIn = builtIn;
    }

    public ProfileResolution Resolve(string liveryId, ResolutionMode mode)
    {
        var present = new List<ProfileEntry>(3);

        var user = mode == ResolutionMode.Normal ? Get(_user, liveryId, present) : null;
        var mod = Get(_mod, liveryId, present);
        var builtIn = Get(_builtIn, liveryId, present);

        var effective = mode == ResolutionMode.Authoring
            // authoring skips disabled entries and falls through to the next enabled one
            ? FirstEnabled(mod, builtIn)
            // if the winning profile is disabled, the livery is skipped entirely
            : ApplyOrStop(user ?? mod ?? builtIn);

        return new ProfileResolution(mode, present, effective);
    }

    private static ProfileEntry? Get(IProfileSource source, string liveryId, List<ProfileEntry> present)
    {
        if (!source.TryGet(liveryId, out var entry)) return null;
        present.Add(entry);
        return entry;
    }

    private static ProfileEntry? ApplyOrStop(ProfileEntry? winner) =>
        winner is { Profile.Enabled: true } ? winner : null;

    private static ProfileEntry? FirstEnabled(params ProfileEntry?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is { Profile.Enabled: true }) return candidate;
        }

        return null;
    }
}
