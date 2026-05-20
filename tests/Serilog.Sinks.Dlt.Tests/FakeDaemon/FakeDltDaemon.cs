using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Tests.FakeDaemon;

internal sealed class FakeDltDaemon : IAsyncDisposable
{
    private readonly Socket _listener;
    private readonly ConcurrentBag<DecodedMessage> _received = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;

    public string SocketPath { get; }
    public IReadOnlyCollection<DecodedMessage> Received => _received;

    public FakeDltDaemon()
    {
        SocketPath = Path.Combine(Path.GetTempPath(), $"dlt-fake-{Guid.NewGuid():N}.sock");
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        _listener.Listen(1);
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            var client = await _listener.AcceptAsync(_cts.Token).ConfigureAwait(false);
            var buffer = new byte[64 * 1024];
            var stash = new List<byte>();
            while (!_cts.IsCancellationRequested)
            {
                var n = await client.ReceiveAsync(buffer, SocketFlags.None, _cts.Token).ConfigureAwait(false);
                if (n == 0) break;
                stash.AddRange(buffer.AsSpan(0, n).ToArray());
                ParseAvailable(stash);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
    }

    private void ParseAvailable(List<byte> stash)
    {
        while (stash.Count >= 22)
        {
            var len = (stash[2] << 8) | stash[3];
            if (stash.Count < len) return;
            var (msg, consumed) = DltDecoder.ParseFrame(stash.GetRange(0, len).ToArray());
            _received.Add(msg);
            stash.RemoveRange(0, consumed);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _acceptLoop.ConfigureAwait(false); } catch { }
        try { _listener.Close(); } catch { }
        try { File.Delete(SocketPath); } catch { }
    }
}
