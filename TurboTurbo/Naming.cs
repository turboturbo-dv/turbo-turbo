using System;

namespace TurboTurbo;

/// <summary>
/// Names for every scene object and asset the mod creates, so its own emitters
/// and markers can be told apart from those created by the game or other mods.
/// </summary>
internal static class Naming
{
    private const string Prefix = "TurboTurbo.";

    /// <summary>Names an object or asset as one of the mod's own.</summary>
    public static string Create(string name) => Prefix + name;

    /// <summary>True when a scene object or asset belongs to the mod.</summary>
    public static bool IsOurs(string name) =>
        name != null && name.StartsWith(Prefix, StringComparison.Ordinal);
}