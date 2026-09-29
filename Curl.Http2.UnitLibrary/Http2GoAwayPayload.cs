namespace Curl.Http2;

/// <summary>
/// A GOAWAY frame's payload (RFC 9113 section 6.8).
/// </summary>
/// <param name="LastStreamId">The highest stream the sender may have processed.</param>
/// <param name="ErrorCode">Why the connection is closing.</param>
/// <param name="DebugData">Opaque diagnostic bytes, often text.</param>
public sealed record Http2GoAwayPayload(int LastStreamId, Http2ErrorCode ErrorCode, ReadOnlyMemory<byte> DebugData);
