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

        if (_bytesWritten + frame.Length > _fileSizeLimitBytes)
            await RollAsync().ConfigureAwait(false);

        await _stream!.WriteAsync(frame, ct).ConfigureAwait(false);
        _bytesWritten += frame.Length;
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask RollAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        var oldest = $"{_path}.{_retainedFileCountLimit - 1}";
        if (File.Exists(oldest)) File.Delete(oldest);

        for (var i = _retainedFileCountLimit - 2; i >= 0; i--)
        {
            var src = $"{_path}.{i}";
            var dst = $"{_path}.{i + 1}";
            if (File.Exists(src)) File.Move(src, dst, overwrite: true);
        }

        if (File.Exists(_path)) File.Move(_path, $"{_path}.0", overwrite: true);

        _bytesWritten = 0;
        Open();
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
