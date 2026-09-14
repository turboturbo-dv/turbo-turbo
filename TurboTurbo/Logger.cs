using System;

using UnityModManagerNet;

namespace TurboTurbo;

internal class Logger
{
    private readonly ILogSink _sink;
    private readonly string _context;

    internal Logger(ILogSink sink, string context)
    {
        _sink = sink;
        _context = context;
    }

    public void Info(string message) => _sink.Log(Format(message));
    public void Warn(string message) => _sink.Warning(Format(message));
    public void Error(string message) => _sink.Error(Format(message));
    public void Exception(Exception exception) => _sink.LogException(exception);

    private string Format(string message) => $"[{_context}] {message}";
}

internal interface ILogSink
{
    void Log(string message);
    void Warning(string message);
    void Error(string message);
    void LogException(Exception exception);
}

/// <summary>Forwards to UMM's mod logger.</summary>
internal sealed class UmmLogSink : ILogSink
{
    private readonly UnityModManager.ModEntry.ModLogger _logger;

    internal UmmLogSink(UnityModManager.ModEntry.ModLogger logger)
    {
        _logger = logger;
    }

    public void Log(string message) => _logger.Log(message);
    public void Warning(string message) => _logger.Warning(message);
    public void Error(string message) => _logger.Error(message);
    public void LogException(Exception exception) => _logger.LogException(exception);
}

/// <summary>Drops everything. Ensures pre-init log calls cannot cause a null dereference.</summary>
internal sealed class NullLogSink : ILogSink
{
    public void Log(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Error(string message)
    {
    }

    public void LogException(Exception exception)
    {
    }
}