namespace Curl.Http2;

/// <summary>
/// A HEADERS frame's payload with its padding removed (RFC 9113 section 6.2).
/// </summary>
/// <param name="Fragment">The header block fragment.</param>
/// <param name="Priority">The priority fields, when the PRIORITY flag was set.</param>
public sealed record Http2HeadersPayload(ReadOnlyMemory<byte> Fragment, Http2Priority? Priority);
