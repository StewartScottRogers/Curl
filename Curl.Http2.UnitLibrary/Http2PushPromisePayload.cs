namespace Curl.Http2;

/// <summary>
/// A PUSH_PROMISE frame's payload with its padding removed (RFC 9113 section 6.6).
/// </summary>
/// <param name="PromisedStreamId">The stream the server reserves for the push.</param>
/// <param name="Fragment">The header block fragment of the promised request.</param>
public sealed record Http2PushPromisePayload(int PromisedStreamId, ReadOnlyMemory<byte> Fragment);
