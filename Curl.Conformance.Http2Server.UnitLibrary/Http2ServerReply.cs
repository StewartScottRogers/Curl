using Curl.Http2;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// The reply the h2c server sends on one stream: its header block (<c>:status</c> first) and body.
/// </summary>
/// <param name="Headers">The HTTP/2 header fields, <c>:status</c> first.</param>
/// <param name="Body">The body bytes.</param>
internal sealed record Http2ServerReply(IReadOnlyList<HeaderField> Headers, byte[] Body);
