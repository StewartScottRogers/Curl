namespace Curl.Http3;

/// <summary>
/// Reads the server's QPACK encoder and decoder streams (RFC 9204 section 4.2) into this
/// endpoint's <see cref="QpackDecoder" /> and <see cref="QpackEncoder" />. Either stream
/// closing is a connection error.
/// </summary>
public static class Http3PeerQpackStreams
{
    /// <summary>
    /// Reads what has arrived on the server's encoder stream and applies it to
    /// <paramref name="decoder" />.
    /// </summary>
    /// <param name="stream">The server's encoder stream, after its stream type.</param>
    /// <param name="decoder">This endpoint's decoder.</param>
    /// <param name="buffer">Where the bytes are read into; not empty.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many bytes were read and applied.</returns>
    /// <exception cref="Http3Exception">The stream ended (<see cref="Http3ErrorCode.ClosedCriticalStream" />).</exception>
    /// <exception cref="QpackException">An instruction is invalid (<see cref="QpackErrorCode.EncoderStreamError" />).</exception>
    public static async ValueTask<int> ReadEncoderStreamAsync(Stream stream, QpackDecoder decoder, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        var count = await ReadCriticalStreamAsync(stream, buffer, "QPACK encoder", cancellationToken).ConfigureAwait(false);
        decoder.ReadEncoderStream(buffer.Span[..count]);
        return count;
    }

    /// <summary>
    /// Reads what has arrived on the server's decoder stream and applies it to
    /// <paramref name="encoder" />.
    /// </summary>
    /// <param name="stream">The server's decoder stream, after its stream type.</param>
    /// <param name="encoder">This endpoint's encoder.</param>
    /// <param name="buffer">Where the bytes are read into; not empty.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many bytes were read and applied.</returns>
    /// <exception cref="Http3Exception">The stream ended (<see cref="Http3ErrorCode.ClosedCriticalStream" />).</exception>
    /// <exception cref="QpackException">An instruction is invalid (<see cref="QpackErrorCode.DecoderStreamError" />).</exception>
    public static async ValueTask<int> ReadDecoderStreamAsync(Stream stream, QpackEncoder encoder, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        var count = await ReadCriticalStreamAsync(stream, buffer, "QPACK decoder", cancellationToken).ConfigureAwait(false);
        encoder.ReadDecoderStream(buffer.Span[..count]);
        return count;
    }

    private static async ValueTask<int> ReadCriticalStreamAsync(Stream stream, Memory<byte> buffer, string streamName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfZero(buffer.Length, nameof(buffer));
        var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (count == 0)
        {
            throw new Http3Exception(Http3ErrorCode.ClosedCriticalStream, $"the server closed its {streamName} stream");
        }

        return count;
    }
}
