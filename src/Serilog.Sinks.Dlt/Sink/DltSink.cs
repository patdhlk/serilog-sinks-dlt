using System;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Dlt.Protocol;
using Serilog.Sinks.Dlt.Transport;

namespace Serilog.Sinks.Dlt.Sink;

public sealed class DltSink : ILogEventSink, IDisposable, IAsyncDisposable
{
    private readonly LogEventConverter _converter;
    private readonly DltDispatcher _dispatcher;
    private bool _disposed;

    internal DltSink(
        string ecuId,
        string appId,
        CtidResolver ctidResolver,
        IDltTransport transport,
        int queueCapacity,
        TimeSpan shutdownTimeout,
        DltFramingMode framingMode = DltFramingMode.None)
    {
        _converter = new LogEventConverter(ecuId, appId, ctidResolver);
        _dispatcher = new DltDispatcher(transport, queueCapacity, shutdownTimeout, framingMode);
    }

    public void Emit(LogEvent logEvent)
    {
        if (_disposed) return;
        Protocol.DltMessage msg;
        try { msg = _converter.Convert(logEvent); }
        catch (Exception ex)
        {
            SelfLog.WriteLine("DLT sink conversion failed: {0}", ex);
            return;
        }
        _dispatcher.TryEnqueue(msg);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        return _dispatcher.DisposeAsync();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
