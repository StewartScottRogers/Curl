namespace Curl.Original.Protocol.Ws.Fakes;

/// <summary>
/// A writable <see cref="Stream" /> that, as a write callback calling <c>curl_ws_send</c> does, answers every write by
/// writing <see cref="Pong" /> to the connection <see cref="WsPerformConnection" /> shows at that moment, and records
/// whether it showed one.
/// </summary>
/// <param name="performConnection">The connection the transfer shows while its frames flow.</param>
public sealed class PongingOutputStream(WsPerformConnection performConnection) : Stream
{
    /// <summary>The raw PONG lib2301's write callback sends back: <c>8a 00</c>.</summary>
    public static readonly byte[] Pong = [0x8a, 0x00];

    /// <summary>Gets, for each write, whether a connection was shown when it came.</summary>
    public List<bool> ConnectionShownAtWrites { get; } = [];

    /// <inheritdoc />
    public override bool CanRead => false;

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
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ConnectionShownAtWrites.Add(performConnection.Connection is not null);
        if (performConnection.Connection is { } connection)
        {
            await connection.WriteAsync(Pong, cancellationToken).ConfigureAwait(false);
        }
    }
}
