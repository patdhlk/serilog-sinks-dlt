using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Dlt.Transport;

internal sealed class FileTransport : IDltTransport
{
    private readonly string _path;
    private readonly long _fileSizeLimitBytes;
    private readonly int _retainedFileCountLimit;
    private FileStream? _stream;
    private long _bytesWritten;
    private bool _disposed;

    public FileTransport(string path, long fileSizeLimitBytes, int retainedFileCountLimit)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path must be non-empty", nameof(path));
        if (fileSizeLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(fileSizeLimitBytes));
        if (retainedFileCountLimit < 1) throw new ArgumentOutOfRangeException(nameof(retainedFileCountLimit));
        _path = path;
        _fileSizeLimitBytes = fileSizeLimitBytes;
        _retainedFileCountLimit = retainedFileCountLimit;
    }

    public bool IsConnected => _stream is not null;

    public ValueTask ConnectAsync(CancellationToken ct) => ValueTask.CompletedTask;

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (_disposed) throw new DltTransportException("FileTransport disposed");

        if (_stream is null) Open();
        await _stream!.WriteAsync(frame, ct).ConfigureAwait(false);
        _bytesWritten += frame.Length;
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private void Open()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 4096, useAsync: true);
        _bytesWritten = _stream.Length;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_stream is not null)
        {
            try { await _stream.DisposeAsync().ConfigureAwait(false); }
            catch { }
            _stream = null;
        }
    }
}
