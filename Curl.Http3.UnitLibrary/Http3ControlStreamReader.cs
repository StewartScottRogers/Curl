namespace Curl.Http3;

/// <summary>
/// Reads the server's control stream (RFC 9114 section 6.2.1) as a client that never sends
/// <c>MAX_PUSH_ID</c>, as curl's ngtcp2 build does not: <c>SETTINGS</c> first and once,
/// then <c>GOAWAY</c> frames; anything else on it is a connection error.
/// </summary>
public sealed class Http3ControlStreamReader
{
    /// <summary>
    /// The longest payload of a known frame type accepted on the control stream. Frames of
    /// unknown and grease types are skipped whatever their length.
    /// </summary>
    public const long MaximumFramePayloadLength = 64 * 1024;

    private readonly Http3FrameReader frames;

    /// <summary>
    /// Initializes a new instance of the <see cref="Http3ControlStreamReader" /> class.
    /// </summary>
    /// <param name="stream">The server's control stream, after its stream type, as <see cref="Http3PeerUnidirectionalStreams.AcceptAsync" /> leaves it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <see langword="null" />.</exception>
    public Http3ControlStreamReader(Stream stream)
    {
        frames = new Http3FrameReader(stream, MaximumFramePayloadLength);
    }

    /// <summary>Gets the server's <c>SETTINGS</c>, once they have been read.</summary>
    public Http3SettingsFrame? PeerSettings { get; private set; }

    /// <summary>
    /// Gets the stream ID of the latest <c>GOAWAY</c>: requests on this stream ID and above
    /// were not processed. <see langword="null" /> until one arrives.
    /// </summary>
    public long? GoawayStreamId { get; private set; }

    /// <summary>
    /// Reads and checks the next frame, recording <c>SETTINGS</c> in <see cref="PeerSettings" />
    /// and <c>GOAWAY</c> in <see cref="GoawayStreamId" />.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="Http3Exception">
    /// The stream ended (<see cref="Http3ErrorCode.ClosedCriticalStream" />); the first frame
    /// is not <c>SETTINGS</c> (<see cref="Http3ErrorCode.MissingSettings" />); a second
    /// <c>SETTINGS</c> or a <c>DATA</c>, <c>HEADERS</c>, <c>PUSH_PROMISE</c> or
    /// <c>MAX_PUSH_ID</c> frame arrived (<see cref="Http3ErrorCode.FrameUnexpected" />); a
    /// <c>GOAWAY</c> names a stream that is not client-initiated bidirectional, or a larger
    /// one than before, or a <c>CANCEL_PUSH</c> arrived although no push was allowed
    /// (<see cref="Http3ErrorCode.IdError" />); or <see cref="Http3FrameReader.ReadFrameAsync" />
    /// refused the frame.
    /// </exception>
    public async ValueTask<Http3Frame> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var frame = await ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
        if (PeerSettings is null)
        {
            PeerSettings = frame as Http3SettingsFrame
                ?? throw new Http3Exception(Http3ErrorCode.MissingSettings, $"the control stream starts with a {frame.Type} frame");
            return frame;
        }

        Accept(frame);
        return frame;
    }

    private static Http3Exception ClosedCriticalStream() =>
        new(Http3ErrorCode.ClosedCriticalStream, "the server closed its control stream");

    private async ValueTask<Http3Frame> ReadNextFrameAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await frames.ReadFrameAsync(cancellationToken).ConfigureAwait(false) ?? throw ClosedCriticalStream();
        }
        catch (Http3Exception) when (frames.HasReachedEndOfStream)
        {
            throw ClosedCriticalStream();
        }
    }

    private void Accept(Http3Frame frame)
    {
        switch (frame)
        {
            case Http3GoawayFrame goaway:
                AcceptGoaway(goaway.Id);
                break;
            case Http3CancelPushFrame cancelPush:
                throw new Http3Exception(
                    Http3ErrorCode.IdError,
                    $"CANCEL_PUSH names push ID {cancelPush.PushId} although no MAX_PUSH_ID was sent");
            default:
                throw new Http3Exception(Http3ErrorCode.FrameUnexpected, $"a {frame.Type} frame arrived on the control stream");
        }
    }

    private void AcceptGoaway(long streamId)
    {
        if (streamId % 4 != 0)
        {
            throw new Http3Exception(Http3ErrorCode.IdError, $"GOAWAY names stream {streamId}, which is not a client-initiated bidirectional stream");
        }

        if (streamId > GoawayStreamId)
        {
            throw new Http3Exception(Http3ErrorCode.IdError, $"GOAWAY names stream {streamId}, above the earlier GOAWAY's {GoawayStreamId}");
        }

        GoawayStreamId = streamId;
    }
}
