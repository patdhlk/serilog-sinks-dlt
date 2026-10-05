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

    [Fact]
    public void Verbatim_option_uses_valid_short_context_as_is()
    {
        var r = new CtidResolver("DFLT", overrides: null, useSourceContextAsCtid: true);
        r.Resolve("SENS").Should().Be("SENS");
        r.Resolve("AX").Should().Be("AX");
    }

    [Fact]
    public void Verbatim_option_off_still_hashes_short_context()
    {
        var r = new CtidResolver("DFLT", overrides: null);
        r.Resolve("SENS").Should().NotBe("SENS");
    }

    [Theory]
    [InlineData("SENS ")]          // 5 chars
    [InlineData("MyApp.Service")]  // long
    [InlineData("ÄB")]             // non-ASCII
    [InlineData("A\tB")]           // control char
    public void Verbatim_option_hashes_invalid_ctid(string ctx)
    {
        var r = new CtidResolver("DFLT", overrides: null, useSourceContextAsCtid: true);
        var hashed = new CtidResolver("DFLT", overrides: null).Resolve(ctx);
        r.Resolve(ctx).Should().Be(hashed);
    }

    [Fact]
    public void Verbatim_option_does_not_beat_explicit_override()
    {
        var overrides = new Dictionary<string, string> { ["SENS"] = "SNS1" };
        var r = new CtidResolver("DFLT", overrides, useSourceContextAsCtid: true);
        r.Resolve("SENS").Should().Be("SNS1");
    }

    [Fact]
    public void Verbatim_option_empty_context_yields_default()
    {
        var r = new CtidResolver("DFLT", overrides: null, useSourceContextAsCtid: true);
        r.Resolve("").Should().Be("DFLT");
        r.Resolve(null).Should().Be("DFLT");
    }

    [Fact]
    public void Verbatim_option_tilde_resolves_verbatim()
    {
        var r = new CtidResolver("DFLT", overrides: null, useSourceContextAsCtid: true);
        r.Resolve("~~~~").Should().Be("~~~~");
    }

    [Fact]
    public void Verbatim_option_space_resolves_verbatim()
    {
        var r = new CtidResolver("DFLT", overrides: null, useSourceContextAsCtid: true);
        r.Resolve(" ").Should().Be(" ");
    }

    [Fact]
    public void Verbatim_option_del_character_hashes()
    {
        var r = new CtidResolver("DFLT", overrides: null, useSourceContextAsCtid: true);
        var hashed = new CtidResolver("DFLT", overrides: null).Resolve("A\u007F");
        r.Resolve("A\u007F").Should().Be(hashed);
    }
}
