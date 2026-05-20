using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Sinks.Dlt.Protocol;
using Serilog.Sinks.Dlt.Transport;

namespace Serilog.Sinks.Dlt.Sink;

internal sealed class DltDispatcher : IAsyncDisposable, IDisposable
{
    private readonly Channel<DltMessage> _channel;
    private readonly IDltTransport _transport;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Task _worker;
    private readonly CancellationTokenSource _workerCts = new();
    private readonly bool _useStorageHeader;
    private long _droppedCount;
    private bool _disposed;

    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public DltDispatcher(IDltTransport transport, int queueCapacity, TimeSpan shutdownTimeout, bool useStorageHeader = false)
    {
        _transport = transport;
        _shutdownTimeout = shutdownTimeout;
        _useStorageHeader = useStorageHeader;
        _channel = Channel.CreateBounded<DltMessage>(
            new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            },
            itemDropped: _ => Interlocked.Increment(ref _droppedCount));
        _worker = Task.Run(RunAsync);
    }

    public bool TryEnqueue(DltMessage msg)
    {
        if (_disposed) { Interlocked.Increment(ref _droppedCount); return false; }
        return _channel.Writer.TryWrite(msg);
    }

    private async Task RunAsync()
    {
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        var lastSelfLog = Stopwatch.StartNew();

        try
        {
            await foreach (var msg in _channel.Reader.ReadAllAsync(_workerCts.Token).ConfigureAwait(false))
            {
                buffer.ResetWrittenCount();
                try
                {
                    if (_useStorageHeader) DltEncoder.EncodeWithStorageHeader(msg, buffer);
                    else DltEncoder.Encode(msg, buffer);
                    await _transport.WriteAsync(buffer.WrittenMemory, _workerCts.Token).ConfigureAwait(false);
                }
                catch (DltTransportException ex)
                {
                    SelfLog.WriteLine("DLT transport write failed: {0}", ex);
                }
                catch (OperationCanceledException) when (_workerCts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SelfLog.WriteLine("DLT sink unexpected error: {0}", ex);
                }

                if (lastSelfLog.Elapsed > TimeSpan.FromSeconds(10))
                {
                    var dropped = Interlocked.Read(ref _droppedCount);
                    if (dropped > 0)
                    {
                        SelfLog.WriteLine("DLT sink dropped {0} events in last interval", dropped);
                        Interlocked.Exchange(ref _droppedCount, 0);
                    }
                    lastSelfLog.Restart();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Writer.TryComplete();

        try
        {
            var done = await Task.WhenAny(_worker, Task.Delay(_shutdownTimeout)).ConfigureAwait(false);
            if (done != _worker)
            {
                _workerCts.Cancel();
                SelfLog.WriteLine("DLT dispatcher shutdown timed out after {0}", _shutdownTimeout);
            }
        }
        catch (Exception ex) { SelfLog.WriteLine("DLT dispatcher shutdown error: {0}", ex); }

        await _transport.DisposeAsync().ConfigureAwait(false);
        _workerCts.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
