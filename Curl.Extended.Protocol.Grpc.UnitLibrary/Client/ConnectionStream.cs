using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>
/// An <see cref="IConnection" /> as the asynchronous read-write <see cref="Stream" /> an HTTP/2 connection runs on;
/// each write is flushed at once, so a TLS connection sends it without waiting.
/// </summary>
/// <param name="connection">The connection; the caller owns it.</param>
internal sealed class ConnectionStream(IConnection connection) : Stream
{
    /// <summary>Gets whether a write failed with an <see cref="IOException" />, which makes the failure a send failure rather than a receive failure.</summary>
    public bool WriteFailed { get; private set; }

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
        try
        {
            await connection.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            WriteFailed = true;
            throw;
        }
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
