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

    [Fact]
    public async Task Rotation_rolls_when_size_limit_exceeded()
    {
        var path = Path("roll.dlt");
        await using var transport = new FileTransport(path, fileSizeLimitBytes: 10, retainedFileCountLimit: 3);

        await transport.WriteAsync(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, CancellationToken.None);
        File.Exists(Path("roll.dlt.0")).Should().BeFalse();

        await transport.WriteAsync(new byte[] { 9, 10, 11, 12, 13 }, CancellationToken.None);
        File.Exists(Path("roll.dlt.0")).Should().BeTrue("first batch should be in roll.dlt.0");
        File.ReadAllBytes(Path("roll.dlt.0")).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        File.ReadAllBytes(path).Should().Equal(9, 10, 11, 12, 13);
    }

    [Fact]
    public async Task Rotation_retains_only_N_files()
    {
        var path = Path("retain.dlt");
        await using var transport = new FileTransport(path, fileSizeLimitBytes: 4, retainedFileCountLimit: 2);

        for (var i = 0; i < 5; i++)
            await transport.WriteAsync(new byte[] { (byte)i, (byte)i, (byte)i, (byte)i, (byte)i }, CancellationToken.None);

        File.Exists(path).Should().BeTrue();
        File.Exists(Path("retain.dlt.0")).Should().BeTrue();
        File.Exists(Path("retain.dlt.1")).Should().BeTrue();
        File.Exists(Path("retain.dlt.2")).Should().BeFalse("retainedFileCountLimit=2 → at most retain.dlt.0..1");
    }
}
