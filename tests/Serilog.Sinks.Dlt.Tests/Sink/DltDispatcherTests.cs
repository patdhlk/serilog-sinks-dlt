using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog.Sinks.Dlt.Protocol;
using Serilog.Sinks.Dlt.Sink;
using Serilog.Sinks.Dlt.Tests.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Sink;

public class DltDispatcherTests
{
    private static DltMessage SampleMessage(byte mcnt = 0) =>
        new("ECU1", "APID", "CTID", mcnt, 0, DltLogLevel.Info,
            new[] { DltArgument.String("x") }, 1, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Enqueued_messages_flow_through_to_transport()
    {
        var fake = new FakeTransport();
        var dispatcher = new DltDispatcher(fake, queueCapacity: 100, shutdownTimeout: TimeSpan.FromSeconds(2));

        for (var i = 0; i < 5; i++) dispatcher.TryEnqueue(SampleMessage((byte)i));
        await dispatcher.DisposeAsync();

        fake.WrittenFrames.Should().HaveCount(5);
    }

    [Fact]
    public async Task Bounded_queue_drops_oldest_under_overflow()
    {
        var fake = new FakeTransport();
        var dispatcher = new DltDispatcher(fake, queueCapacity: 4, shutdownTimeout: TimeSpan.FromSeconds(2));

        var release = new TaskCompletionSource();
        fake.OnWriteStart = () => release.Task.GetAwaiter().GetResult();
        for (var i = 0; i < 100; i++) dispatcher.TryEnqueue(SampleMessage((byte)i));
        release.SetResult();
        await dispatcher.DisposeAsync();

        dispatcher.DroppedCount.Should().BeGreaterThan(0);
        fake.WrittenFrames.Count.Should().BeLessThanOrEqualTo(100);
    }
}
