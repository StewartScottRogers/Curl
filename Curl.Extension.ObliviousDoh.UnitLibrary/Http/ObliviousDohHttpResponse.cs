namespace Curl.Extension.ObliviousDoh.Http;

/// <summary>An HTTP response the proxy or the target sent: its status code and its decoded body.</summary>
/// <param name="StatusCode">The status code.</param>
/// <param name="Body">The body, with any chunked transfer coding removed.</param>
public sealed record ObliviousDohHttpResponse(int StatusCode, byte[] Body);
