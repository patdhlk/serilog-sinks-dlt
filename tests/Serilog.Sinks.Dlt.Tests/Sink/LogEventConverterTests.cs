using System;
using System.Collections.Generic;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Dlt.Protocol;
using Serilog.Sinks.Dlt.Sink;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Sink;

public class LogEventConverterTests
{
    private static LogEvent MakeEvent(string template, params (string Name, object Value)[] props)
    {
        var parser = new MessageTemplateParser();
        var parsed = parser.Parse(template);
        var properties = new List<LogEventProperty>();
        foreach (var (n, v) in props)
            properties.Add(new LogEventProperty(n, new ScalarValue(v)));
        return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null, parsed, properties);
    }

    private static LogEventConverter NewConverter() =>
        new("ECU1", "APID", new CtidResolver("DFLT", null));

    [Fact]
    public void First_argument_is_rendered_template_string()
    {
        var ev = MakeEvent("hello {name}", ("name", "world"));
        var msg = NewConverter().Convert(ev);
        msg.Arguments[0].Kind.Should().Be(DltArgumentKind.String);
        msg.Arguments[0].AsString().Should().Be("hello \"world\"");
    }

    [Fact]
    public void Int_property_becomes_Int32_argument()
    {
        var ev = MakeEvent("count {count}", ("count", 42));
        var msg = NewConverter().Convert(ev);
        msg.Arguments[1].Kind.Should().Be(DltArgumentKind.Int32);
        msg.Arguments[1].AsInt32().Should().Be(42);
    }

    [Fact]
    public void Bool_property_becomes_Bool_argument()
    {
        var ev = MakeEvent("flag {b}", ("b", true));
        var msg = NewConverter().Convert(ev);
        msg.Arguments[1].Kind.Should().Be(DltArgumentKind.Bool);
        msg.Arguments[1].AsBool().Should().BeTrue();
    }

    [Fact]
    public void Double_property_becomes_Float64_argument()
    {
        var ev = MakeEvent("ratio {r}", ("r", 0.5));
        var msg = NewConverter().Convert(ev);
        msg.Arguments[1].Kind.Should().Be(DltArgumentKind.Float64);
        msg.Arguments[1].AsFloat64().Should().Be(0.5);
    }

    [Fact]
    public void SourceContext_property_drives_context_id()
    {
        var parser = new MessageTemplateParser();
        var props = new List<LogEventProperty> {
            new("SourceContext", new ScalarValue("MyApp.Foo"))
        };
        var ev = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, parser.Parse("hi"), props);

        var msg = NewConverter().Convert(ev);
        msg.ContextId.Should().NotBe("DFLT").And.HaveLength(4);
    }

    [Fact]
    public void Exception_appended_as_final_string_argument()
    {
        var parser = new MessageTemplateParser();
        var ev = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error,
            new InvalidOperationException("bad state"),
            parser.Parse("boom"),
            new List<LogEventProperty>());

        var msg = NewConverter().Convert(ev);
        msg.Arguments[msg.ArgumentCount - 1].Kind.Should().Be(DltArgumentKind.String);
        msg.Arguments[msg.ArgumentCount - 1].AsString().Should().Contain("InvalidOperationException")
            .And.Contain("bad state");
    }

    [Fact]
    public void Argument_count_capped_at_255()
    {
        var props = new List<LogEventProperty>();
        for (var i = 0; i < 400; i++)
            props.Add(new LogEventProperty($"p{i}", new ScalarValue(i)));
        var parser = new MessageTemplateParser();
        var ev = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            parser.Parse("many"), props);

        var msg = NewConverter().Convert(ev);
        msg.ArgumentCount.Should().BeLessThanOrEqualTo(DltConstants.MaxArgumentCount);
    }
}
