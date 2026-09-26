namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// A read-only stream over fixed bytes that cannot seek and hands out at most
/// <c>chunkSize</c> bytes per read, the way a pipe such as <c>curl -T -</c> reading
/// standard input does.
/// </summary>
/// <param name="content">The bytes to hand out.</param>
/// <param name="chunkSize">The most bytes one read returns.</param>
public sealed class NonSeekableReadStream(byte[] content, int chunkSize = int.MaxValue) : Stream
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
    public override int Read(byte[] buffer, int offset, int count)
    {
        var length = Math.Min(Math.Min(count, chunkSize), content.Length - position);
        content.AsSpan(position, length).CopyTo(buffer.AsSpan(offset));
        position += length;
        return length;
    }

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
