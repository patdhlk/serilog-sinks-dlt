using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog;
using Xunit;

namespace Serilog.Sinks.Dlt.IntegrationTests;

[Trait("Category", "RealDaemon")]
public class RealDaemonTests
{
    // dlt-daemon must be built with WITH_DLT_UNIX_SOCKET_IPC=ON so /tmp/dlt is
    // a real Unix domain socket (Ubuntu's apt build uses FIFO IPC and won't
    // work). The docker/Dockerfile in this repo builds the daemon from source
    // with the right flag.
    private static readonly string SocketPath = Environment.GetEnvironmentVariable("DLT_SOCKET_PATH") ?? "/tmp/dlt";
    private static readonly string TraceDir   = Environment.GetEnvironmentVariable("DLT_TRACE_DIR") ?? "/var/log/dlt";
    private static readonly bool   Enabled    = Environment.GetEnvironmentVariable("DLT_DAEMON_AVAILABLE") == "1";

    [SkippableFact]
    public async Task Information_event_lands_in_offline_trace()
    {
        Skip.IfNot(Enabled, "DLT_DAEMON_AVAILABLE not set");

        var apid = $"T{Random.Shared.Next(100, 999)}";
        var marker = $"marker-{Guid.NewGuid():N}";

        using (var log = new LoggerConfiguration()
            .WriteTo.Dlt(appId: apid, socketPath: SocketPath)
            .CreateLogger())
        {
            log.Information("integration {marker}", marker);
            await Task.Delay(500);
        }

        var psi = new ProcessStartInfo("bash", $"-c \"dlt-convert -a {Path.Combine(TraceDir, "*.dlt")}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();

        output.Should().Contain(apid).And.Contain(marker);
    }
}
