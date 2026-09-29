using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Presents an <see cref="IConnection" /> as the <see cref="Stream" /> the HTTP/2 frame layer
/// reads and writes (<see cref="Curl.Http2.Http2Connection" />): reads and writes pass
/// straight through, asynchronously only. Disposing it leaves the connection open; the
/// handler owns and disposes that.
/// </summary>
/// <param name="connection">The connection.</param>
internal sealed class HttpConnectionStream(IConnection connection) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a connection has no length.</exception>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a connection has no position.</exception>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        connection.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        connection.FlushAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the connection is read asynchronously only.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the connection is written asynchronously only.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the connection is flushed asynchronously only.</exception>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a connection cannot seek.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: a connection has no length.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();
}
