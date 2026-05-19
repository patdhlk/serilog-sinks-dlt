using FluentAssertions;
using Serilog.Sinks.Dlt.Protocol;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Protocol;

public class DltArgumentTests
{
    [Fact]
    public void String_factory_stores_value()
    {
        var arg = DltArgument.String("hello");
        arg.Kind.Should().Be(DltArgumentKind.String);
        arg.AsString().Should().Be("hello");
    }

    [Fact]
    public void Bool_factory_stores_true_and_false()
    {
        DltArgument.Bool(true).AsBool().Should().BeTrue();
        DltArgument.Bool(false).AsBool().Should().BeFalse();
    }

    [Theory]
    [InlineData((sbyte)-12)]
    [InlineData((sbyte)0)]
    [InlineData(sbyte.MaxValue)]
    [InlineData(sbyte.MinValue)]
    public void Int8_factory_round_trips(sbyte v)
    {
        var arg = DltArgument.Int8(v);
        arg.Kind.Should().Be(DltArgumentKind.Int8);
        arg.AsInt8().Should().Be(v);
    }

    [Fact]
    public void Float64_factory_preserves_NaN()
    {
        var arg = DltArgument.Float64(double.NaN);
        arg.Kind.Should().Be(DltArgumentKind.Float64);
        double.IsNaN(arg.AsFloat64()).Should().BeTrue();
    }

    [Fact]
    public void Raw_factory_keeps_byte_reference()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        var arg = DltArgument.Raw(data);
        arg.Kind.Should().Be(DltArgumentKind.Raw);
        arg.AsRaw().ToArray().Should().Equal(data);
    }
}
