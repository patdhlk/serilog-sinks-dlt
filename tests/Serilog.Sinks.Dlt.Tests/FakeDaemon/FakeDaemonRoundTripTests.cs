using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Dlt.Protocol;
using Serilog.Sinks.Dlt.Sink;
using Serilog.Sinks.Dlt.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.FakeDaemon;

[Trait("Category", "Unix")]
public class FakeDaemonRoundTripTests
{
    [SkippableFact]
    public async Task Event_with_int_and_string_round_trips_through_unix_socket()
    {
        Skip.IfNot(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Unix socket only");

        await using var daemon = new FakeDltDaemon();
        var transport = new UnixSocketTransport(daemon.SocketPath);
        await transport.ConnectAsync(CancellationToken.None);
        await using var sink = new DltSink("ECU1", "MYAP", new CtidResolver("DFLT", null), transport,
            queueCapacity: 100, shutdownTimeout: TimeSpan.FromSeconds(2));

        var parser = new MessageTemplateParser();
        var props = new List<LogEventProperty> { new("count", new ScalarValue(42)) };
        var ev = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            parser.Parse("processed {count}"), props);
        sink.Emit(ev);

        await ((IAsyncDisposable)sink).DisposeAsync();
        await Task.Delay(100);

        daemon.Received.Should().ContainSingle(m =>
            m.AppId == "MYAP" && m.LogLevel == DltLogLevel.Info && m.Arguments.Count >= 2);
    }
}
