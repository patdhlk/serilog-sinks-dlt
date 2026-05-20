using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Serilog.Sinks.Dlt.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Transport;

public class ReconnectingTransportTests
{
    [Fact]
    public async Task Initial_write_triggers_connect()
    {
        var fake = new FakeTransport();
        await using var rc = new ReconnectingTransport(fake, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1), TimeProvider.System);
        await rc.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        fake.ConnectCalls.Should().Be(1);
        fake.WrittenFrames.Should().HaveCount(1);
    }

    [Fact]
    public async Task Write_failure_marks_disconnected_and_next_write_reconnects()
    {
        var fake = new FakeTransport();
        await using var rc = new ReconnectingTransport(fake, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(10), TimeProvider.System);
        await rc.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        fake.FailNextNWrites = 1;

        Func<Task> first = async () => await rc.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        await first.Should().ThrowAsync<DltTransportException>();

        await rc.WriteAsync(new byte[] { 3 }, CancellationToken.None);
        fake.ConnectCalls.Should().Be(2);
        fake.WrittenFrames.Should().HaveCount(2);
    }

    [Fact]
    public async Task Backoff_uses_injected_TimeProvider()
    {
        var timeProvider = new FakeTimeProvider();
        var fake = new FakeTransport { FailNextConnect = true };
        await using var rc = new ReconnectingTransport(fake, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), timeProvider);

        Func<Task> first = async () => await rc.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        await first.Should().ThrowAsync<DltTransportException>();

        Func<Task> second = async () => await rc.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        await second.Should().ThrowAsync<DltTransportException>();

        fake.FailNextConnect = false;
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        await rc.WriteAsync(new byte[] { 3 }, CancellationToken.None);
        fake.WrittenFrames.Should().HaveCount(1);
    }
}
