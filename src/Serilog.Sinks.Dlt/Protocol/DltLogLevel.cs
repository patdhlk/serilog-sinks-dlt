using Serilog.Events;

namespace Serilog.Sinks.Dlt.Protocol;

public enum DltLogLevel : byte
{
    Fatal = 1,
    Error = 2,
    Warn = 3,
    Info = 4,
    Debug = 5,
    Verbose = 6,
}

internal static class DltLogLevelExtensions
{
    public static DltLogLevel FromSerilog(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal       => DltLogLevel.Fatal,
        LogEventLevel.Error       => DltLogLevel.Error,
        LogEventLevel.Warning     => DltLogLevel.Warn,
        LogEventLevel.Information => DltLogLevel.Info,
        LogEventLevel.Debug       => DltLogLevel.Debug,
        LogEventLevel.Verbose     => DltLogLevel.Verbose,
        _                         => DltLogLevel.Info,
    };
}
