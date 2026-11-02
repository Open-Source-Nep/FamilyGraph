using System.Buffers;
using System.IO.Compression;
using System.IO.Pipelines;

namespace Server.Export;

/// <summary>
/// A write-only stream over the response pipe for the export zip. <see cref="ZipArchive"/> still writes
/// a few small records (an entry's data descriptor) synchronously; here a synchronous write only fills
/// the pipe's buffer, which the next asynchronous write or flush sends, so no request thread blocks on I/O.
/// </summary>
internal sealed class ResponsePipeStream(PipeWriter pipe) : Stream
{
    /// <summary>Sends the buffer once this much is waiting.</summary>
    private const int FlushThreshold = 64 * 1024;

    private long _unflushed;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        pipe.Write(buffer);
        _unflushed += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        if (_unflushed > FlushThreshold)
        {
            await FlushAsync(cancellationToken);
        }
    }

    /// <summary>Nothing to do synchronously; <see cref="FlushAsync(CancellationToken)"/> sends the buffer.</summary>
    public override void Flush()
    {
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        _unflushed = 0;
        await pipe.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
