using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Transport;

internal sealed class UnixSocketTransport : IDltTransport
{
    private readonly string _socketPath;
    private readonly ReadOnlyMemory<byte> _greeting;
    private Socket? _socket;

    public UnixSocketTransport(string socketPath, ReadOnlyMemory<byte> greeting = default)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
            throw new ArgumentException("socketPath must be non-empty", nameof(socketPath));
        _socketPath = socketPath;
        _greeting = greeting;
    }

    public bool IsConnected => _socket is { Connected: true };

    public async ValueTask ConnectAsync(CancellationToken ct)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct).ConfigureAwait(false);
            _socket = socket;
        }
        catch (SocketException ex)
        {
            socket.Dispose();
            throw new DltTransportException($"Failed to connect to DLT daemon at {_socketPath}", ex);
        }

        if (!_greeting.IsEmpty)
        {
            try { await WriteAsync(_greeting, ct).ConfigureAwait(false); }
            catch (DltTransportException) { _socket.Dispose(); _socket = null; throw; }
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (_socket is null) throw new DltTransportException("Transport not connected");
        try
        {
            var remaining = frame;
            while (!remaining.IsEmpty)
            {
                var n = await _socket.SendAsync(remaining, SocketFlags.None, ct).ConfigureAwait(false);
                if (n == 0) throw new DltTransportException("Peer closed the Unix socket");
                remaining = remaining[n..];
            }
        }
        catch (SocketException ex)
        {
            throw new DltTransportException("Unix socket write failed", ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw new DltTransportException("Unix socket was disposed", ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        _socket?.Dispose();
        _socket = null;
        return ValueTask.CompletedTask;
    }
}
