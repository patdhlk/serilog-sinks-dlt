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

    [Theory]
    [InlineData(nameof(LoggerSinkConfigurationExtensions.Dlt))]
    [InlineData(nameof(LoggerSinkConfigurationExtensions.DltTcp))]
    [InlineData(nameof(LoggerSinkConfigurationExtensions.DltFile))]
    public void Sink_methods_expose_useSourceContextAsCtid_as_last_optional_parameter(string method)
    {
        var p = typeof(LoggerSinkConfigurationExtensions).GetMethod(method)!.GetParameters();
        var last = p[^1];
        last.Name.Should().Be("useSourceContextAsCtid");
        last.ParameterType.Should().Be(typeof(bool));
        last.HasDefaultValue.Should().BeTrue();
        last.DefaultValue.Should().Be(false);
    }

    [Fact]
    public void DltFile_with_verbatim_ctid_writes_ctid_unchanged()
    {
        var dir = System.IO.Directory.CreateTempSubdirectory();
        try
        {
            var path = System.IO.Path.Combine(dir.FullName, "x.dlt");
            using (var log = new LoggerConfiguration()
                       .WriteTo.DltFile(path, appId: "TEST", useSourceContextAsCtid: true)
                       .CreateLogger())
            {
                log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "SENS").Information("hi");
            }
            var bytes = System.IO.File.ReadAllBytes(System.IO.Directory.GetFiles(dir.FullName, "*.dlt")[0]);
            System.Text.Encoding.ASCII.GetString(bytes).Should().Contain("TESTSENS");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
