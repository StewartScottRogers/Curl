namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// A read-only window over a seekable upload from an offset to its end: one request's content, read from the upload
/// only as the transfer reads it, so nothing of it is held in memory. Disposing it leaves the upload open.
/// </summary>
internal sealed class UploadRemainderStream : Stream
{
    private readonly Stream upload;
    private readonly long start;

    /// <summary>Initializes a new instance of the <see cref="UploadRemainderStream" /> class and moves <paramref name="upload" /> to <paramref name="start" />.</summary>
    /// <param name="upload">The whole upload, seekable.</param>
    /// <param name="start">Where in <paramref name="upload" /> the window starts, at most its length.</param>
    public UploadRemainderStream(Stream upload, long start)
    {
        this.upload = upload;
        this.start = start;
        upload.Position = start;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => upload.Length - start;

    /// <inheritdoc />
    public override long Position
    {
        get => upload.Position - start;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            upload.Position = start + value;
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => upload.Read(buffer, offset, count);

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => upload.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        upload.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        upload.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = offset + origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => Position,
            _ => Length,
        };
        return Position;
    }

    /// <summary>Does nothing: the window is never written.</summary>
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
