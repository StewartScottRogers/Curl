using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// A stream the server opened that HTTP/3 itself does not use, offered to the caller of an
/// <see cref="Http3ClientSession" /> for an extension such as WebTransport: a unidirectional
/// stream of an unknown type (RFC 9114 section 6.2, WebTransport's <c>0x54</c>), or a
/// bidirectional stream (WebTransport's <c>0x41</c> signal).
/// </summary>
/// <param name="Stream">The stream, positioned after <paramref name="Type" />.</param>
/// <param name="IsBidirectional">Whether the stream is bidirectional rather than unidirectional.</param>
/// <param name="Type">The unidirectional stream's type, or the first variable-length integer of the bidirectional stream.</param>
public sealed record Http3UnknownPeerStream(IMultiplexedStream Stream, bool IsBidirectional, long Type);
