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
    public async Task Reader_drains_bytes_the_peer_sends()
    {
        Skip.IfNot(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Unix socket only");

        var path = NewSocketPath();
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        var acceptTask = listener.AcceptAsync();

        await using var transport = new UnixSocketTransport(path);
        await transport.ConnectAsync(CancellationToken.None);
        using var server = await acceptTask;

        // Send a chunk from the listener side. With no reader on the transport,
        // these bytes would sit in our kernel receive buffer; with the reader
        // they get drained.
        var payload = new byte[16 * 1024];
        new Random(42).NextBytes(payload);
        await server.SendAsync(payload, SocketFlags.None);

        // Give the reader a moment to drain.
        await Task.Delay(150);

        // Indirect check: server's send completed and the connection is still healthy.
        // (If the reader weren't draining, repeated sends would eventually fill our
        // socket receive buffer; but the more direct check is that we can keep
        // writing in both directions without errors.)
        await transport.WriteAsync(new byte[] { 0x42 }, CancellationToken.None);
        var roundTripBuf = new byte[1];
        var n = await server.ReceiveAsync(roundTripBuf, SocketFlags.None);
        n.Should().Be(1);
        roundTripBuf[0].Should().Be(0x42);

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
