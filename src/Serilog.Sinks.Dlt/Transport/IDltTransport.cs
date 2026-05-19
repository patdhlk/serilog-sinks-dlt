using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Transport;

internal interface IDltTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    ValueTask ConnectAsync(CancellationToken ct);
    ValueTask WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken ct);
}

internal sealed class DltTransportException : Exception
{
    public DltTransportException(string message) : base(message) { }
    public DltTransportException(string message, Exception inner) : base(message, inner) { }
}
