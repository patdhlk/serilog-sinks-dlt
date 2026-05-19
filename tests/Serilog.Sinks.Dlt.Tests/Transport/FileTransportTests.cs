using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Serilog.Sinks.Dlt.Transport;
using Xunit;

namespace Serilog.Sinks.Dlt.Tests.Transport;

public class FileTransportTests : IDisposable
{
    private readonly string _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dlt-file-{Guid.NewGuid():N}");

    public FileTransportTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Path(string name) => System.IO.Path.Combine(_dir, name);

    [Fact]
    public async Task WriteAsync_appends_bytes_to_file()
    {
        var path = Path("app.dlt");
        await using (var transport = new FileTransport(path, fileSizeLimitBytes: 1_000_000, retainedFileCountLimit: 5))
        {
            await transport.WriteAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
            await transport.WriteAsync(new byte[] { 4, 5 }, CancellationToken.None);
        }
        File.ReadAllBytes(path).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task WriteAsync_creates_parent_directory_if_missing()
    {
        var path = Path("nested/sub/app.dlt");
        await using var transport = new FileTransport(path, 1_000_000, 5);
        await transport.WriteAsync(new byte[] { 42 }, CancellationToken.None);
        File.Exists(path).Should().BeTrue();
    }
}
