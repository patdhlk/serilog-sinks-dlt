using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Transport;

internal sealed class TcpTransport : IDltTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly ReadOnlyMemory<byte> _greeting;
    private Socket? _socket;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;

    public TcpTransport(string host, int port, ReadOnlyMemory<byte> greeting = default)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host must be non-empty", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        _host = host;
        _port = port;
        _greeting = greeting;
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

        if (!_greeting.IsEmpty)
        {
            try { await WriteAsync(_greeting, ct).ConfigureAwait(false); }
            catch (DltTransportException) { _socket.Dispose(); _socket = null; throw; }
        }

        _readerCts = new CancellationTokenSource();
        _readerTask = Task.Run(() => DrainInboundAsync(_socket!, _readerCts.Token));
    }

    // See UnixSocketTransport.DrainInboundAsync for rationale.
    private static async Task DrainInboundAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await socket.ReceiveAsync(buffer, SocketFlags.None, ct).ConfigureAwait(false);
                if (n == 0) return;
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
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

    public async ValueTask DisposeAsync()
    {
        _readerCts?.Cancel();
        var reader = _readerTask;
        _socket?.Dispose();
        _socket = null;

        if (reader is not null)
        {
            try { await reader.ConfigureAwait(false); }
            catch { }
        }

        _readerCts?.Dispose();
        _readerCts = null;
        _readerTask = null;
    }
}
