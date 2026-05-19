using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog.Sinks.Dlt.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Transport;

[Trait("Category", "Unix")]
public class UnixSocketTransportTests
{
    private static string NewSocketPath() =>
        Path.Combine(Path.GetTempPath(), $"dlt-test-{Guid.NewGuid():N}.sock");

    [SkippableFact]
    public async Task Connect_then_write_delivers_bytes_to_listener()
    {
        Skip.IfNot(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Unix socket only");

        var path = NewSocketPath();
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);

        var acceptTask = listener.AcceptAsync();

        await using var transport = new UnixSocketTransport(path);
        await transport.ConnectAsync(CancellationToken.None);
        await transport.WriteAsync(Encoding.ASCII.GetBytes("hello"), CancellationToken.None);

        using var server = await acceptTask;
        var buf = new byte[5];
        var n = await server.ReceiveAsync(buf, SocketFlags.None);
        n.Should().Be(5);
        Encoding.ASCII.GetString(buf).Should().Be("hello");

        try { File.Delete(path); } catch { }
    }

    [SkippableFact]
    public async Task WriteAsync_throws_DltTransportException_when_listener_closed()
    {
        Skip.IfNot(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Unix socket only");

        var path = NewSocketPath();
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        var acceptTask = listener.AcceptAsync();

        await using var transport = new UnixSocketTransport(path);
        await transport.ConnectAsync(CancellationToken.None);

        var server = await acceptTask;
        server.Close();
        listener.Close();

        Func<Task> writeBurst = async () =>
        {
            for (var i = 0; i < 10; i++)
                await transport.WriteAsync(new byte[4096], CancellationToken.None);
        };
        await writeBurst.Should().ThrowAsync<DltTransportException>();

        try { File.Delete(path); } catch { }
    }
}
