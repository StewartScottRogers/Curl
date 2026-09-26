using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Presents an <see cref="IConnection" /> as the <see cref="Stream" /> an
/// <see cref="System.Net.Security.SslStream" /> runs over. Only the asynchronous members
/// carry bytes, because <see cref="IConnection" /> has no synchronous ones. Disposing the
/// stream leaves the connection open: its owner disposes it.
/// </summary>
/// <param name="connection">The plaintext connection to read from and write to.</param>
internal sealed class ConnectionStream(IConnection connection) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <summary>Not supported: a connection has no length.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Length => throw new NotSupportedException();

    /// <summary>Not supported: a connection has no position.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        connection.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        connection.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        connection.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        connection.FlushAsync(cancellationToken).AsTask();

    /// <summary>Not supported: the connection has no synchronous read.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Not supported: the connection has no synchronous write.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Not supported: the connection has no synchronous flush.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Flush() => throw new NotSupportedException();

    /// <summary>Not supported: a connection cannot seek.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>Not supported: a connection has no length.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();
}
