using System.Collections.Generic;
using FluentAssertions;
using Serilog.Sinks.Dlt.Sink;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Sink;

public class CtidResolverTests
{
    [Fact]
    public void Same_input_yields_same_ctid()
    {
        var r = new CtidResolver("DFLT", overrides: null);
        r.Resolve("MyApp.Service.OrderProcessor").Should().Be(r.Resolve("MyApp.Service.OrderProcessor"));
    }

    [Fact]
    public void Returns_default_for_null_source_context()
    {
        var r = new CtidResolver("DFLT", overrides: null);
        r.Resolve(null).Should().Be("DFLT");
    }

    [Fact]
    public void Override_takes_priority_over_hash()
    {
        var overrides = new Dictionary<string, string> { ["MyApp.Service.OrderProcessor"] = "ORDR" };
        var r = new CtidResolver("DFLT", overrides);
        r.Resolve("MyApp.Service.OrderProcessor").Should().Be("ORDR");
    }

    [Fact]
    public void Resolved_ctid_is_4_ascii_uppercase_alphanumeric()
    {
        var r = new CtidResolver("DFLT", overrides: null);
        var ctid = r.Resolve("some.context");
        ctid.Length.Should().Be(4);
        foreach (var c in ctid)
            (char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)).Should().BeTrue();
    }
}
