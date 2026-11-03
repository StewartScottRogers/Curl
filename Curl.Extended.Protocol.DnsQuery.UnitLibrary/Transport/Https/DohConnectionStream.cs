using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Https;

/// <summary>
/// Presents a TLS connection as the asynchronous read-write <see cref="Stream" /> the shared HTTP/2 layer runs on
/// (BL-4690): reads and writes go to the connection, each write flushed; the synchronous and seeking members are not supported.
/// </summary>
/// <param name="connection">The connection; the caller owns it.</param>
internal sealed class DohConnectionStream(IConnection connection) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        connection.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await connection.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => connection.FlushAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}
