namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// A readable stream that cannot seek, so its length is not known before it is read, as
/// standard input's is under <c>-T -</c>.
/// </summary>
public sealed class UnseekableStream(byte[] content) : Stream
{
    private readonly MemoryStream inner = new(content);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
