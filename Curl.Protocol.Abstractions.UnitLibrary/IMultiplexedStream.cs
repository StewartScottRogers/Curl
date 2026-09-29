namespace Curl.Protocol.Abstractions;

/// <summary>
/// One stream of an <see cref="IMultiplexedConnection" />: a byte stream with its own end
/// and its own reset, which HTTP/3 (<c>Curl.Http3</c>) reads and writes frames on
/// (ADR-0144).
/// </summary>
/// <remarks>
/// <c>Curl.Quic</c> implements it with stream flow control; a unit test supplies an
/// in-memory one. The code that opened or accepted the stream disposes it.
/// </remarks>
public interface IMultiplexedStream : IAsyncDisposable
{
    /// <summary>
    /// Gets the stream's QUIC stream ID, the number in curl's <c>[HTTP/3] [&lt;id&gt;]</c>
    /// lines.
    /// </summary>
    long StreamId { get; }

    /// <summary>
    /// Reads up to <paramref name="buffer" /> bytes the peer sent on this stream.
    /// </summary>
    /// <param name="buffer">The destination for the bytes read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read, or zero once the peer has ended the stream (FIN).</returns>
    /// <exception cref="MultiplexedStreamResetException">The peer reset the stream.</exception>
    /// <exception cref="MultiplexedConnectionFailedException">The connection was lost.</exception>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="buffer" /> on this stream, ending it when
    /// <paramref name="endStream" /> is <see langword="true" />.
    /// </summary>
    /// <param name="buffer">The bytes to send; may be empty to only end the stream.</param>
    /// <param name="endStream"><see langword="true" /> to send FIN after the bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes have been accepted by the stream.</returns>
    /// <exception cref="MultiplexedConnectionFailedException">The connection was lost.</exception>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken);

    /// <summary>
    /// Abandons the stream in both directions, sending <c>RESET_STREAM</c> and
    /// <c>STOP_SENDING</c> with <paramref name="applicationErrorCode" />.
    /// </summary>
    /// <param name="applicationErrorCode">
    /// The application's error code, such as HTTP/3's <c>H3_REQUEST_CANCELLED</c> (<c>0x10c</c>).
    /// </param>
    void Abort(long applicationErrorCode);
}
