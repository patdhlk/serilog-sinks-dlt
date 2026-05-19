using System;

namespace Serilog.Sinks.Dlt.Protocol;

internal readonly struct DltMessage
{
    public string EcuId { get; }
    public string AppId { get; }
    public string ContextId { get; }
    public byte MessageCounter { get; }
    public uint Timestamp { get; }            // 0.1 ms units since process start
    public DltLogLevel LogLevel { get; }
    public DltArgument[] Arguments { get; }   // pooled — see ArgumentCount
    public int ArgumentCount { get; }
    public DateTimeOffset StorageWallClock { get; }  // used only by EncodeWithStorageHeader

    public DltMessage(
        string ecuId,
        string appId,
        string contextId,
        byte messageCounter,
        uint timestamp,
        DltLogLevel logLevel,
        DltArgument[] arguments,
        int argumentCount,
        DateTimeOffset storageWallClock)
    {
        EcuId = ecuId;
        AppId = appId;
        ContextId = contextId;
        MessageCounter = messageCounter;
        Timestamp = timestamp;
        LogLevel = logLevel;
        Arguments = arguments;
        ArgumentCount = argumentCount;
        StorageWallClock = storageWallClock;
    }
}
