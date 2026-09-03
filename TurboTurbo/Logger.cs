using System;
using UnityModManagerNet;

namespace TurboTurbo;

/// <summary>
/// Context-tagged logger: prefixes every message with [context]. Created
/// exclusively through Log.ForContext.
/// </summary>
internal class Logger
{
    private readonly UnityModManager.ModEntry.ModLogger _logger;
    private readonly string _context;

    internal Logger(UnityModManager.ModEntry.ModLogger logger, string context)
    {
        _logger = logger;
        _context = context;
    }

    public void Info(string message) => _logger.Log(Format(message));
    public void Warn(string message) => _logger.Warning(Format(message));
    public void Error(string message) => _logger.Error(Format(message));
    public void Exception(Exception exception) => _logger.LogException(exception);

    private string Format(string message) => $"[{_context}] {message}";
}
