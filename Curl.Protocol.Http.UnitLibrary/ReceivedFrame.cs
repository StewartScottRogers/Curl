using Curl.Http2;

namespace Curl.Protocol.Http;

/// <summary>
/// What an <see cref="Http2Session" /> hands one of its streams (BL-717): a DATA frame or a
/// completed header block with its decoded fields, or the peer's reset of the stream.
/// </summary>
/// <param name="Frame">The frame, or <see langword="null" /> for a reset.</param>
/// <param name="Fields">The decoded header block of a HEADERS frame, else <see langword="null" />.</param>
/// <param name="Reset">The peer's reset of the stream, or <see langword="null" /> for a frame.</param>
internal sealed record ReceivedFrame(Http2StreamFrame? Frame, IReadOnlyList<HeaderField>? Fields, Http2StreamResetException? Reset);
