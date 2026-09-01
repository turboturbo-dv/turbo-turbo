namespace TurboTurbo;

using UnityModManagerNet;

/// <summary>Log bridge: routes mod messages to the UMM console/log.</summary>
public class Logger
{
    private readonly UnityModManager.ModEntry.ModLogger _logger;

    public Logger(UnityModManager.ModEntry.ModLogger modLogger)
    {
        _logger = modLogger;
    }

    public void LogInfo(string message) => _logger.Log(message);
    public void LogWarning(string message) => _logger.Warning(message);
    public void LogError(string message) => _logger.Error(message);
}
