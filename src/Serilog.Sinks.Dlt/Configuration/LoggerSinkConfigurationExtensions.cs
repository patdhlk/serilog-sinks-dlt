using System;
using System.Collections.Generic;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Dlt.Sink;
using Serilog.Sinks.Dlt.Transport;

namespace Serilog;

public static class LoggerSinkConfigurationExtensions
{
    public static LoggerConfiguration Dlt(
        this LoggerSinkConfiguration sinkConfig,
        string appId,
        string socketPath = "/tmp/dlt",
        string ecuId = "ECU1",
        string defaultContextId = "DFLT",
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null,
        int queueCapacity = 10_000,
        TimeSpan? reconnectInitialDelay = null,
        TimeSpan? reconnectMaxDelay = null,
        TimeSpan? shutdownTimeout = null,
        IReadOnlyDictionary<string, string>? contextIdMap = null)
    {
        ArgumentNullException.ThrowIfNull(sinkConfig);
        Validate.Id(appId, nameof(appId));
        Validate.Id(ecuId, nameof(ecuId));
        Validate.Id(defaultContextId, nameof(defaultContextId));

        var inner = new UnixSocketTransport(socketPath);
        var transport = new ReconnectingTransport(inner,
            reconnectInitialDelay ?? TimeSpan.FromMilliseconds(500),
            reconnectMaxDelay     ?? TimeSpan.FromSeconds(30),
            TimeProvider.System);

        var sink = new Serilog.Sinks.Dlt.Sink.DltSink(
            ecuId, appId, new CtidResolver(defaultContextId, contextIdMap),
            transport, queueCapacity, shutdownTimeout ?? TimeSpan.FromSeconds(5),
            useStorageHeader: false);

        return sinkConfig.Sink(sink, restrictedToMinimumLevel, levelSwitch);
    }

    public static LoggerConfiguration DltTcp(
        this LoggerSinkConfiguration sinkConfig,
        string appId,
        string host,
        int port = 3490,
        string ecuId = "ECU1",
        string defaultContextId = "DFLT",
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null,
        int queueCapacity = 10_000,
        TimeSpan? reconnectInitialDelay = null,
        TimeSpan? reconnectMaxDelay = null,
        TimeSpan? shutdownTimeout = null,
        IReadOnlyDictionary<string, string>? contextIdMap = null)
    {
        ArgumentNullException.ThrowIfNull(sinkConfig);
        Validate.Id(appId, nameof(appId));
        Validate.Id(ecuId, nameof(ecuId));
        Validate.Id(defaultContextId, nameof(defaultContextId));
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host must be non-empty", nameof(host));

        var inner = new TcpTransport(host, port);
        var transport = new ReconnectingTransport(inner,
            reconnectInitialDelay ?? TimeSpan.FromMilliseconds(500),
            reconnectMaxDelay     ?? TimeSpan.FromSeconds(30),
            TimeProvider.System);

        var sink = new Serilog.Sinks.Dlt.Sink.DltSink(
            ecuId, appId, new CtidResolver(defaultContextId, contextIdMap),
            transport, queueCapacity, shutdownTimeout ?? TimeSpan.FromSeconds(5),
            useStorageHeader: false);

        return sinkConfig.Sink(sink, restrictedToMinimumLevel, levelSwitch);
    }

    public static LoggerConfiguration DltFile(
        this LoggerSinkConfiguration sinkConfig,
        string path,
        string appId,
        string ecuId = "ECU1",
        string defaultContextId = "DFLT",
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch? levelSwitch = null,
        long fileSizeLimitBytes = 100L * 1024 * 1024,
        int retainedFileCountLimit = 10,
        int queueCapacity = 10_000,
        TimeSpan? shutdownTimeout = null,
        IReadOnlyDictionary<string, string>? contextIdMap = null)
    {
        ArgumentNullException.ThrowIfNull(sinkConfig);
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path must be non-empty", nameof(path));
        Validate.Id(appId, nameof(appId));
        Validate.Id(ecuId, nameof(ecuId));
        Validate.Id(defaultContextId, nameof(defaultContextId));
        if (fileSizeLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(fileSizeLimitBytes));
        if (retainedFileCountLimit < 1) throw new ArgumentOutOfRangeException(nameof(retainedFileCountLimit));

        var transport = new FileTransport(path, fileSizeLimitBytes, retainedFileCountLimit);
        var sink = new Serilog.Sinks.Dlt.Sink.DltSink(
            ecuId, appId, new CtidResolver(defaultContextId, contextIdMap),
            transport, queueCapacity, shutdownTimeout ?? TimeSpan.FromSeconds(5),
            useStorageHeader: true);

        return sinkConfig.Sink(sink, restrictedToMinimumLevel, levelSwitch);
    }
}

internal static class Validate
{
    public static void Id(string value, string paramName)
    {
        if (value is null) throw new ArgumentNullException(paramName);
        if (value.Length < 1 || value.Length > 4) throw new ArgumentException($"{paramName} must be 1-4 chars", paramName);
        foreach (var c in value)
        {
            if (c is < (char)0x20 or > (char)0x7E)
                throw new ArgumentException($"{paramName} must be printable ASCII", paramName);
        }
    }
}
