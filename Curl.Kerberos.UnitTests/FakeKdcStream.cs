using System.Buffers.Binary;

namespace Curl.Kerberos;

/// <summary>
/// The TCP side of <see cref="FakeKdc" />: collects the length-prefixed request written to it,
/// and on the first read answers with the length-prefixed reply (RFC 4120 section 7.2.2).
/// </summary>
internal sealed class FakeKdcStream(Func<byte[], byte[]> answer, uint? replyLengthPrefix) : Stream
{
    private readonly MemoryStream written = new();
    private MemoryStream? reply;

    public bool WasDisposed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        reply ??= Answer();
        return reply.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => written.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    private MemoryStream Answer()
    {
        byte[] framed = written.ToArray();
        int length = BinaryPrimitives.ReadInt32BigEndian(framed);
        Assert.AreEqual(framed.Length - 4, length, "The request's length prefix must count the bytes after it.");
        byte[] answered = answer(framed[4..]);
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, replyLengthPrefix ?? (uint)answered.Length);
        return new MemoryStream([.. prefix, .. answered]);
    }
}
