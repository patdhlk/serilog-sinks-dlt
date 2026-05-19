using FluentAssertions;
using Serilog.Events;
using Serilog.Sinks.Dlt.Protocol;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Protocol;

public class DltLogLevelTests
{
    [Theory]
    [InlineData(LogEventLevel.Verbose,     DltLogLevel.Verbose)]
    [InlineData(LogEventLevel.Debug,       DltLogLevel.Debug)]
    [InlineData(LogEventLevel.Information, DltLogLevel.Info)]
    [InlineData(LogEventLevel.Warning,     DltLogLevel.Warn)]
    [InlineData(LogEventLevel.Error,       DltLogLevel.Error)]
    [InlineData(LogEventLevel.Fatal,       DltLogLevel.Fatal)]
    public void FromSerilog_maps_each_level(LogEventLevel serilog, DltLogLevel expected)
    {
        DltLogLevelExtensions.FromSerilog(serilog).Should().Be(expected);
    }

    [Theory]
    [InlineData(DltLogLevel.Fatal,   1)]
    [InlineData(DltLogLevel.Error,   2)]
    [InlineData(DltLogLevel.Warn,    3)]
    [InlineData(DltLogLevel.Info,    4)]
    [InlineData(DltLogLevel.Debug,   5)]
    [InlineData(DltLogLevel.Verbose, 6)]
    public void Enum_value_matches_DLT_spec(DltLogLevel level, int expected)
    {
        ((int)level).Should().Be(expected);
    }
}
