using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Sinks.Dlt.Transport;

namespace Serilog.Sinks.Dlt.Tests.Transport;

internal sealed class FakeTransport : IDltTransport
{
    public List<byte[]> WrittenFrames { get; } = new();
    public int ConnectCalls;
    public bool FailNextConnect;
    public int FailNextNWrites;
    public Action? OnWriteStart = null;
    public bool IsConnected { get; private set; }

    public ValueTask ConnectAsync(CancellationToken ct)
    {
        ConnectCalls++;
        if (FailNextConnect)
        {
            FailNextConnect = false;
            throw new DltTransportException("simulated connect failure");
        }
        IsConnected = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        OnWriteStart?.Invoke();
        if (FailNextNWrites > 0)
        {
            FailNextNWrites--;
            IsConnected = false;
            throw new DltTransportException("simulated write failure");
        }
        WrittenFrames.Add(frame.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
