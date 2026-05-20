using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Dlt.Sink;
using Serilog.Sinks.Dlt.Tests.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Sink;

public class DltSinkTests
{
    [Fact]
    public async Task Emit_writes_frame_to_transport()
    {
        var fake = new FakeTransport();
        var sink = new DltSink("ECU1", "APID", new CtidResolver("DFLT", null), fake,
            queueCapacity: 100, shutdownTimeout: TimeSpan.FromSeconds(2));

        var parser = new MessageTemplateParser();
        var ev = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            parser.Parse("hello"), new List<LogEventProperty>());
        sink.Emit(ev);
        await ((IAsyncDisposable)sink).DisposeAsync();

        fake.WrittenFrames.Should().HaveCount(1);
    }

    [Fact]
    public async Task Emit_never_throws_when_converter_fails()
    {
        var fake = new FakeTransport();
        var sink = new DltSink("ECU1", "APID", new CtidResolver("DFLT", null), fake,
            queueCapacity: 10, shutdownTimeout: TimeSpan.FromSeconds(1));
        var parser = new MessageTemplateParser();
        var props = new List<LogEventProperty> { new("bad", new ScalarValue(new ThrowingToString())) };
        var ev = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            parser.Parse("hi"), props);

        Action act = () => sink.Emit(ev);
        act.Should().NotThrow();

        await ((IAsyncDisposable)sink).DisposeAsync();
    }

    private sealed class ThrowingToString
    {
        public override string ToString() => throw new InvalidOperationException("nope");
    }
}
