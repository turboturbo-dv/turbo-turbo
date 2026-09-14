using UnityModManagerNet;

namespace TurboTurbo;

/// <summary>
/// Logger factory. Call <see cref="ForContext"/> when you need a logger.
/// </summary>
internal static class Log
{
    private static ILogSink _sink = new NullLogSink();

    /// <summary>
    /// Sets up the logger so that <see cref="ForContext"/> works.
    /// Needs to be the first thing called in mod init so everything afterwards can log.
    /// </summary>
    public static void Init(ILogSink sink) => _sink = sink;

    /// <summary>
    /// Generates a logger for the given context. This will normally be your class name or something similar.
    /// </summary>
    public static Logger ForContext(string context) => new Logger(_sink, context);
}