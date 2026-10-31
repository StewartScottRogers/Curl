using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Wire;

/// <summary>
/// A read-only, asynchronous-only <see cref="Stream"/> over an <see cref="IConnection"/>, so an <see cref="System.Xml.XmlReader"/>
/// can parse the server's XML stream as it arrives. It remembers whether the connection reached its end.
/// </summary>
internal sealed class ConnectionReadStream(IConnection connection, CancellationToken cancellationToken) : Stream
{
    /// <summary>Gets whether a read found the connection closed.</summary>
    internal bool ReachedEnd { get; private set; }

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
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken readCancellation = default)
    {
        int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        ReachedEnd |= read == 0;
        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken readCancellation) =>
        ReadAsync(buffer.AsMemory(offset, count), readCancellation).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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
