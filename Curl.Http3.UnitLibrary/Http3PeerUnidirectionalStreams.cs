namespace Curl.Http3;

/// <summary>
/// Sorts the unidirectional streams a server opens (RFC 9114 section 6.2) by the type they
/// start with, as a client that never sends <c>MAX_PUSH_ID</c>: one control stream, one QPACK
/// encoder stream and one QPACK decoder stream, no push streams, and unknown and grease
/// types ignored, or left to an extension that reads them.
/// </summary>
public sealed class Http3PeerUnidirectionalStreams
{
    private readonly HashSet<Http3UnidirectionalStreamType> accepted = [];

    /// <summary>
    /// Reads a new stream's type and records it.
    /// </summary>
    /// <param name="stream">The new unidirectional stream; left positioned after its type.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// <see cref="Http3UnidirectionalStreamType.Control" />,
    /// <see cref="Http3UnidirectionalStreamType.QpackEncoder" /> or
    /// <see cref="Http3UnidirectionalStreamType.QpackDecoder" />; or <see langword="null" />
    /// when the type is unknown or reserved for greasing, or the stream ended before its type,
    /// and the caller stops reading it (with <see cref="Http3ErrorCode.StreamCreationError" />).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <see langword="null" />.</exception>
    /// <exception cref="Http3Exception">
    /// A second control, QPACK encoder or QPACK decoder stream (<see cref="Http3ErrorCode.StreamCreationError" />),
    /// or a push stream (<see cref="Http3ErrorCode.IdError" />).
    /// </exception>
    public async ValueTask<Http3UnidirectionalStreamType?> AcceptAsync(Stream stream, CancellationToken cancellationToken) =>
        await ReadTypeAsync(stream, cancellationToken).ConfigureAwait(false) is { } type and <= (long)Http3UnidirectionalStreamType.QpackDecoder
            ? (Http3UnidirectionalStreamType)type
            : null;

    /// <summary>
    /// Reads a new stream's type and records it, as <see cref="AcceptAsync" /> does, but gives
    /// every type as its number, so a caller can hand a stream of a type HTTP/3 does not use
    /// to an extension that does (WebTransport's <c>0x54</c>).
    /// </summary>
    /// <param name="stream">The new unidirectional stream; left positioned after its type.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stream's type, or <see langword="null" /> when the stream ended before it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <see langword="null" />.</exception>
    /// <exception cref="Http3Exception">
    /// A second control, QPACK encoder or QPACK decoder stream (<see cref="Http3ErrorCode.StreamCreationError" />),
    /// or a push stream (<see cref="Http3ErrorCode.IdError" />).
    /// </exception>
    public async ValueTask<long?> ReadTypeAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long? type;
        try
        {
            type = await Http3StreamReading.ReadVariableLengthIntegerAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        switch (type)
        {
            case 0x00 or 0x02 or 0x03:
                Record((Http3UnidirectionalStreamType)type);
                break;
            case 0x01:
                throw new Http3Exception(Http3ErrorCode.IdError, "the server opened a push stream although no MAX_PUSH_ID was sent");
        }

        return type;
    }

    private void Record(Http3UnidirectionalStreamType type)
    {
        if (!accepted.Add(type))
        {
            throw new Http3Exception(Http3ErrorCode.StreamCreationError, $"the server opened a second {type} stream");
        }
    }
}
