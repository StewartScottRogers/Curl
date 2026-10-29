namespace Curl.HttpDatagram;

/// <summary>
/// Frames HTTP Datagrams carried in QUIC DATAGRAM frames on HTTP/3 (RFC 9297 section 2.1):
/// Quarter Stream ID (i), HTTP Datagram Payload (..).
/// </summary>
public static class Http3Datagram
{
    /// <summary>
    /// The largest legal quarter stream ID, 2^60 - 1.
    /// </summary>
    public const ulong MaximumQuarterStreamId = (1UL << 60) - 1;

    /// <summary>
    /// Encodes a datagram for the request stream <paramref name="streamId" />.
    /// </summary>
    /// <param name="streamId">The client-initiated bidirectional stream the datagram belongs to.</param>
    /// <param name="payload">The HTTP Datagram payload.</param>
    /// <returns>The QUIC DATAGRAM frame's payload.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The stream is not a client-initiated bidirectional stream.</exception>
    public static byte[] Encode(ulong streamId, ReadOnlySpan<byte> payload)
    {
        if (streamId % 4 != 0 || streamId > HttpDatagramVariableLengthInteger.MaximumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(streamId), streamId, "HTTP Datagrams belong to client-initiated bidirectional streams.");
        }

        var quarterStreamId = streamId / 4;
        var prefixLength = HttpDatagramVariableLengthInteger.GetEncodedLength(quarterStreamId);
        var bytes = new byte[prefixLength + payload.Length];
        HttpDatagramVariableLengthInteger.Write(quarterStreamId, bytes);
        payload.CopyTo(bytes.AsSpan(prefixLength));
        return bytes;
    }

    /// <summary>
    /// Decodes a QUIC DATAGRAM frame's payload into its request stream and HTTP Datagram payload.
    /// </summary>
    /// <param name="datagram">The QUIC DATAGRAM frame's payload.</param>
    /// <param name="streamId">The request stream the datagram belongs to: four times the quarter stream ID.</param>
    /// <returns>The HTTP Datagram payload.</returns>
    /// <exception cref="HttpDatagramException">
    /// H3_DATAGRAM_ERROR: the quarter stream ID is cut short or above 2^60 - 1.
    /// </exception>
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> datagram, out ulong streamId)
    {
        if (!HttpDatagramVariableLengthInteger.TryRead(datagram.Span, out var quarterStreamId, out var consumed))
        {
            throw new HttpDatagramException(HttpDatagramErrorCode.DatagramError, "the datagram is too short to hold a quarter stream ID");
        }

        if (quarterStreamId > MaximumQuarterStreamId)
        {
            throw new HttpDatagramException(HttpDatagramErrorCode.DatagramError, "the quarter stream ID is above 2^60 - 1");
        }

        streamId = quarterStreamId * 4;
        return datagram[consumed..];
    }
}
