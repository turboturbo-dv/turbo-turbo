namespace TurboTurbo.Profiles;

/// <summary>What a livery is currently running.</summary>
internal enum ProfileStatusKind
{
    NotConfigured,
    BuiltIn,
    Supplied,
    User,
    SuppliedDisabled,
    UserDisabled,
}

/// <summary>
/// The effective profile for one livery, plus which tiers it shadows. Precedence is
/// user, then supplied, then built-in, and a present-but-disabled profile stops the
/// chain rather than falling through to the tier below.
/// </summary>
internal record struct ProfileStatus(ProfileStatusKind Kind, string Origin, string Overrides)
{
    /// <summary>Resolves the effective status from the three profile sources for one livery.</summary>
    public static ProfileStatus Resolve(LocoProfile user, LocoProfile mod, string suppliedOrigin, LocoProfile builtIn)
    {
        if (user != null)
        {
            return user.Enabled
                ? new ProfileStatus(ProfileStatusKind.User, null, Shadowed(mod != null, builtIn != null))
                : new ProfileStatus(ProfileStatusKind.UserDisabled, null, null);
        }

        if (mod != null)
        {
            return mod.Enabled
                ? new ProfileStatus(ProfileStatusKind.Supplied, suppliedOrigin, builtIn != null ? "built-in" : null)
                : new ProfileStatus(ProfileStatusKind.SuppliedDisabled, suppliedOrigin, null);
        }

        return builtIn != null
            ? new ProfileStatus(ProfileStatusKind.BuiltIn, null, null)
            : new ProfileStatus(ProfileStatusKind.NotConfigured, null, null);
    }

    /// <summary>Human-readable status line.</summary>
    public string Label
    {
        get
        {
            var text = Kind switch
            {
                ProfileStatusKind.User => "yours",
                ProfileStatusKind.UserDisabled => "yours (disabled)",
                ProfileStatusKind.Supplied => $"mod: {Origin}",
                ProfileStatusKind.SuppliedDisabled => $"mod: {Origin} (disabled)",
                ProfileStatusKind.BuiltIn => "built-in",
                _ => "not configured",
            };

            return Overrides != null ? $"{text} (overrides {Overrides})" : text;
        }
    }

    private static string Shadowed(bool hasMod, bool hasBuiltIn) =>
        (hasMod, hasBuiltIn) switch
        {
            (true, true) => "mod + built-in",
            (true, false) => "mod",
            (false, true) => "built-in",
            _ => null,
        };
}