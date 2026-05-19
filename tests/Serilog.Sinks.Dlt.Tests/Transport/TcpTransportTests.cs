using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog.Sinks.Dlt.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Transport;

public class TcpTransportTests
{
    [Fact]
    public async Task Connect_then_write_delivers_bytes()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        await using var transport = new TcpTransport("127.0.0.1", port);
        await transport.ConnectAsync(CancellationToken.None);
        await transport.WriteAsync(Encoding.ASCII.GetBytes("ping"), CancellationToken.None);

        using var server = await acceptTask;
        var buf = new byte[4];
        await server.GetStream().ReadExactlyAsync(buf, 0, 4);
        Encoding.ASCII.GetString(buf).Should().Be("ping");

        listener.Stop();
    }

    [Fact]
    public async Task Connect_to_dead_port_throws_DltTransportException()
    {
        await using var transport = new TcpTransport("127.0.0.1", 1);
        Func<Task> act = async () => await transport.ConnectAsync(CancellationToken.None);
        await act.Should().ThrowAsync<DltTransportException>();
    }
}
