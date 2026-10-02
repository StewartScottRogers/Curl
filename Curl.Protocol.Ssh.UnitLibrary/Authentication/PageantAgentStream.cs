using System.Buffers.Binary;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// A connection to Pageant as a stream, so <see cref="SshAgentClient" /> speaks to it as to
/// a pipe: each whole request frame written is exchanged at once, and the answer frame is
/// what the next reads return. A failed exchange leaves nothing to read, so the client's
/// read ends early and the request fails.
/// </summary>
/// <param name="transact">Exchanges one request frame for one answer frame, or no bytes.</param>
internal sealed class PageantAgentStream(Func<byte[], byte[]> transact) : Stream
{
    private const int LengthSize = 4;

    private readonly MemoryStream written = new();

    private MemoryStream toRead = new();

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => toRead.Read(buffer, offset, count);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        written.Write(buffer, offset, count);
        byte[] bytes = written.ToArray();
        if (bytes.Length >= LengthSize && bytes.Length - LengthSize >= BinaryPrimitives.ReadUInt32BigEndian(bytes))
        {
            written.SetLength(0);
            toRead = new MemoryStream(transact(bytes));
        }
    }
}
