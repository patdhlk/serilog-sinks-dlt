using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Transport;

internal sealed class TcpTransport : IDltTransport
{
    private readonly string _host;
    private readonly int _port;
    private Socket? _socket;

    public TcpTransport(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host must be non-empty", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _host = host;
        _port = port;
    }

    public bool IsConnected => _socket is { Connected: true };

    public async ValueTask ConnectAsync(CancellationToken ct)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
            _socket = socket;
        }
        catch (SocketException ex)
        {
            socket.Dispose();
            throw new DltTransportException($"Failed to connect to DLT daemon at {_host}:{_port}", ex);
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
                if (n == 0) throw new DltTransportException("Peer closed the TCP socket");
                remaining = remaining[n..];
            }
        }
        catch (SocketException ex)
        {
            throw new DltTransportException("TCP write failed", ex);
        }
        catch (ObjectDisposedException ex)
        {
            throw new DltTransportException("TCP socket was disposed", ex);
        }
    }

    public ValueTask DisposeAsync()
    {
        _socket?.Dispose();
        _socket = null;
        return ValueTask.CompletedTask;
    }
}
