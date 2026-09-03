using UnityModManagerNet;

namespace TurboTurbo;

/// <summary>
/// Logger factory. Initialised once by Main.Load with the UMM ModLogger;
/// every component derives its own context-tagged Logger via ForContext.
/// </summary>
internal static class Log
{
    private static UnityModManager.ModEntry.ModLogger _modLogger;

    /// <summary>Must be the first thing Main.Load does. Idempotent, so a
    /// UMM mod reload can call it again.</summary>
    internal static void Init(UnityModManager.ModEntry.ModLogger modLogger) => _modLogger = modLogger;

    internal static Logger ForContext(string context) => new Logger(_modLogger, context);
}
