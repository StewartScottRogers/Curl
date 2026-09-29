namespace Curl.Http2;

/// <summary>
/// What <see cref="Http2Connection.ReadStreamFrameAsync" /> hands the caller: a stream's
/// body bytes, or a whole header block with every CONTINUATION fragment joined.
/// </summary>
/// <param name="Type"><see cref="Http2FrameType.Data" /> or <see cref="Http2FrameType.Headers" />.</param>
/// <param name="StreamId">The stream it belongs to.</param>
/// <param name="Content">The body bytes with padding removed, or the complete HPACK header block.</param>
/// <param name="IsEndStream">Whether the peer ended the stream with it.</param>
public sealed record Http2StreamFrame(Http2FrameType Type, int StreamId, ReadOnlyMemory<byte> Content, bool IsEndStream);
