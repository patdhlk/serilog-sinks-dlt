using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Transport;

internal sealed class ReconnectingTransport : IDltTransport
{
    private readonly IDltTransport _inner;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeProvider _time;
    private int _attempt;
    private DateTimeOffset _nextAllowedConnect;

    public ReconnectingTransport(IDltTransport inner, TimeSpan initialDelay, TimeSpan maxDelay, TimeProvider time)
    {
        _inner = inner;
        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
        _time = time;
        _nextAllowedConnect = DateTimeOffset.MinValue;
    }

    public bool IsConnected => _inner.IsConnected;

    public ValueTask ConnectAsync(CancellationToken ct) => _inner.ConnectAsync(ct);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (!_inner.IsConnected)
        {
            if (_time.GetUtcNow() < _nextAllowedConnect)
                throw new DltTransportException("waiting for reconnect backoff to elapse");

            try
            {
                await _inner.ConnectAsync(ct).ConfigureAwait(false);
                _attempt = 0;
                _nextAllowedConnect = DateTimeOffset.MinValue;
            }
            catch (DltTransportException)
            {
                ScheduleBackoff();
                throw;
            }
        }

        await _inner.WriteAsync(frame, ct).ConfigureAwait(false);
    }

    private void ScheduleBackoff()
    {
        var attempt = _attempt++;
        var rawMs = Math.Min(_maxDelay.TotalMilliseconds, _initialDelay.TotalMilliseconds * Math.Pow(2, attempt));
        var jitter = Random.Shared.NextDouble() * 0.2 + 0.9;
        _nextAllowedConnect = _time.GetUtcNow() + TimeSpan.FromMilliseconds(rawMs * jitter);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
