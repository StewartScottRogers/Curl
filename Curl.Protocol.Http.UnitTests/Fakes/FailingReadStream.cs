namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// A read-only stream that returns its content at most <c>readSize</c> bytes per read and
/// then throws, as a request body file whose disk fails partway does.
/// </summary>
/// <param name="content">The bytes read before the failure.</param>
/// <param name="readSize">The most bytes one read returns; 1 or more.</param>
/// <param name="failure">What the first read past <paramref name="content" /> throws.</param>
public sealed class FailingReadStream(byte[] content, int readSize, Exception failure) : Stream
{
    private int offset;

    /// <summary>
    /// Gets the size of every read asked for, in order.
    /// </summary>
    public List<int> RequestedReads { get; } = [];

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
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        RequestedReads.Add(buffer.Length);
        if (offset == content.Length)
        {
            throw failure;
        }

        int length = Math.Min(Math.Min(readSize, buffer.Length), content.Length - offset);
        content.AsSpan(offset, length).CopyTo(buffer);
        offset += length;
        return length;
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
