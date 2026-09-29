using Curl.Protocol.Abstractions;

namespace Curl.Quic;

/// <summary>
/// One <see cref="QuicStream" /> offered through the multiplexed-stream contract HTTP/3 uses
/// (ADR-0144, BL-721). A read waits until bytes, the FIN or the peer's RESET_STREAM have
/// arrived; a write hands the bytes to the stream, which flow control lets out as the
/// server allows, and completes at once.
/// </summary>
internal sealed class QuicMultiplexedStream(QuicConnection connection, QuicStream stream) : IMultiplexedStream
{
    /// <inheritdoc />
    public long StreamId => (long)stream.Id;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        connection.WaitForAsync(() => TryRead(buffer.Span), cancellationToken);

    /// <inheritdoc />
    /// <exception cref="MultiplexedStreamResetException">The peer asked the client to stop sending on this stream.</exception>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connection.Change(() =>
        {
            if (stream.PeerStopSendingErrorCode is { } errorCode)
            {
                throw new MultiplexedStreamResetException((long)errorCode, $"The server asked QUIC stream {stream.Id} to stop sending, with application error {errorCode}.");
            }

            stream.Write(buffer.Span, endStream);
        });
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Abort(long applicationErrorCode) => connection.Change(() => stream.Abort((ulong)applicationErrorCode));

    /// <summary>Releases nothing: the stream ends only by FIN, by <see cref="Abort" /> or with its connection.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Bytes, or zero at the end; a reset throws; null while nothing has arrived.
    private int? TryRead(Span<byte> destination)
    {
        if (stream.PeerResetErrorCode is { } errorCode)
        {
            throw new MultiplexedStreamResetException((long)errorCode, $"The server reset QUIC stream {stream.Id} with application error {errorCode}.");
        }

        var read = stream.Read(destination);
        return read > 0 || stream.IsReadComplete || destination.IsEmpty ? read : null;
    }
}
