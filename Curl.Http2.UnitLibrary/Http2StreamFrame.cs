namespace Curl.Http2;

/// <summary>
/// What <see cref="Http2Connection.ReadStreamFrameAsync" /> hands the caller: a stream's
/// body bytes, a whole header block with every CONTINUATION fragment joined, or, on a
/// connection with push enabled, a PUSH_PROMISE's whole header block.
/// </summary>
/// <param name="Type"><see cref="Http2FrameType.Data" />, <see cref="Http2FrameType.Headers" /> or <see cref="Http2FrameType.PushPromise" />.</param>
/// <param name="StreamId">The stream it belongs to; for a PUSH_PROMISE, the client stream it came on.</param>
/// <param name="Content">The body bytes with padding removed, or the complete HPACK header block.</param>
/// <param name="IsEndStream">Whether the peer ended the stream with it.</param>
public sealed record Http2StreamFrame(Http2FrameType Type, int StreamId, ReadOnlyMemory<byte> Content, bool IsEndStream)
{
    /// <summary>Gets the stream a PUSH_PROMISE reserved for the push, or 0 for any other frame.</summary>
    public int PromisedStreamId { get; init; }
}
