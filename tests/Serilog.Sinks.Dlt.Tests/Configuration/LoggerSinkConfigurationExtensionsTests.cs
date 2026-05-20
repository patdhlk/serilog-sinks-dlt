using System;
using FluentAssertions;
using Serilog;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Configuration;

public class LoggerSinkConfigurationExtensionsTests
{
    [Fact]
    public void Dlt_throws_on_appId_too_long()
    {
        var act = () => new LoggerConfiguration().WriteTo.Dlt(appId: "TOO_LONG").CreateLogger();
        act.Should().Throw<ArgumentException>().WithParameterName("appId");
    }

    [Fact]
    public void Dlt_throws_on_non_ascii_ecuId()
    {
        var act = () => new LoggerConfiguration().WriteTo.Dlt(appId: "OK", ecuId: "ÉCU1").CreateLogger();
        act.Should().Throw<ArgumentException>().WithParameterName("ecuId");
    }

    [Fact]
    public void DltTcp_throws_on_empty_host()
    {
        var act = () => new LoggerConfiguration().WriteTo.DltTcp(appId: "OK", host: "").CreateLogger();
        act.Should().Throw<ArgumentException>().WithParameterName("host");
    }

    [Fact]
    public void DltFile_throws_on_negative_size_limit()
    {
        var act = () => new LoggerConfiguration().WriteTo.DltFile(path: "x.dlt", appId: "OK", fileSizeLimitBytes: -1).CreateLogger();
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("fileSizeLimitBytes");
    }
}
