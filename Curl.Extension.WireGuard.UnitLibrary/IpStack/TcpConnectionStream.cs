namespace Curl.Extension.WireGuard.IpStack;

/// <summary>A <see cref="TcpConnection" /> as a <see cref="Stream" /> that reads and writes only asynchronously.</summary>
/// <param name="connection">The connection; disposing the stream closes it.</param>
public sealed class TcpConnectionStream(TcpConnection connection) : Stream
{
    private const string SynchronousIoRefused = "A user-space TCP connection reads and writes only asynchronously.";

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

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
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        connection.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(connection.WriteAsync(buffer, cancellationToken));

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        connection.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Does nothing: every write is sent before it completes.</summary>
    public override void Flush()
    {
    }

    /// <summary>Refused: the stream reads only asynchronously.</summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <returns>Never returns.</returns>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(SynchronousIoRefused);

    /// <summary>Refused: the stream writes only asynchronously.</summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException(SynchronousIoRefused);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>Closes the connection with FIN, then disposes the stream.</summary>
    /// <returns>A task that completes when the connection is closed.</returns>
    public override async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Aborts the connection without FIN when disposed synchronously.</summary>
    /// <param name="disposing">Whether the stream is being disposed rather than finalised.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connection.Abort();
        }

        base.Dispose(disposing);
    }
}
