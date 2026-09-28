namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// A readable <see cref="Stream" /> that returns <paramref name="before" /> and then throws
/// <paramref name="failure" /> on the next read, as a <c>-T</c> file whose read fails part way.
/// </summary>
/// <param name="before">The bytes read before the failure.</param>
/// <param name="failure">Thrown by the read after <paramref name="before" /> is used up.</param>
public sealed class FailingUploadStream(byte[] before, IOException failure) : Stream
{
    private int position;

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
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (position == before.Length)
        {
            throw failure;
        }

        int length = Math.Min(buffer.Length, before.Length - position);
        before.AsSpan(position, length).CopyTo(buffer);
        position += length;
        return length;
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
