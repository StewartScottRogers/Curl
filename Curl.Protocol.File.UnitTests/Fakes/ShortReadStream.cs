namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A forward-only readable stream that never returns more than a fixed number of bytes
/// from one read, however many were asked for, and is still not at its end when it
/// stops short.
/// </summary>
/// <remarks>
/// Every other fake here is backed by a <see cref="MemoryStream" />, which fills the whole
/// buffer whenever it has the bytes, so none of them can show the handler a short read
/// that is not the end of the stream. A pipe, a socket or a character device does exactly
/// that, and only a read of zero means the end.
/// </remarks>
public sealed class ShortReadStream : Stream, IRecordingStream
{
    private readonly MemoryStream inner;
    private readonly int maximumReadLength;
    private readonly List<int> readLengths = [];

    /// <summary>
    /// Creates a stream that yields <paramref name="content" /> at most
    /// <paramref name="maximumReadLength" /> bytes at a time.
    /// </summary>
    /// <param name="content">The content to yield.</param>
    /// <param name="maximumReadLength">The most bytes any one read returns.</param>
    public ShortReadStream(byte[] content, int maximumReadLength)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumReadLength);

        inner = new MemoryStream(content);
        this.maximumReadLength = maximumReadLength;
    }

    /// <summary>
    /// Gets the number of bytes each asynchronous read returned, in order, including the
    /// final zero that marks the end.
    /// </summary>
    public IReadOnlyList<int> ReadLengths => readLengths;

    /// <inheritdoc />
    public bool WasDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) =>
        inner.Read(buffer[..Math.Min(buffer.Length, maximumReadLength)]);

    /// <inheritdoc />
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        int read = Read(buffer.Span);
        readLengths.Add(read);

        return ValueTask.FromResult(read);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;

        base.Dispose(disposing);
    }
}
