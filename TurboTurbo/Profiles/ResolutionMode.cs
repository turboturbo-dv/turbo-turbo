namespace TurboTurbo.Profiles;

/// <summary>Which precedence rules apply.</summary>
internal enum ResolutionMode
{
    /// <summary>user &gt; mod &gt; built-in</summary>
    Normal,

    /// <summary>The user tier is ignored</summary>
    Authoring,
}
