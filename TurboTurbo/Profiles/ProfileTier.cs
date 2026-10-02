namespace TurboTurbo.Profiles;

/// <summary>A profile source, ordered by precedence: built-in &lt; mod &lt; user.</summary>
internal enum ProfileTier
{
    BuiltIn,
    Mod,
    User,
}
