namespace Curl.Extension.S3.Http;

/// <summary>One HTTP/1.1 request to an S3 endpoint.</summary>
/// <param name="Method">The method, such as <c>GET</c> or <c>PUT</c>.</param>
/// <param name="Target">The request target in origin form, such as <c>/bucket/key?uploads</c>, already percent-encoded.</param>
/// <param name="Headers">The header fields to send, in order; they must include <c>Host</c>. <c>Content-Length</c> is added from <paramref name="BodyLength"/>.</param>
/// <param name="Body">The body to stream, or <see langword="null"/> for none.</param>
/// <param name="BodyLength">The number of bytes of <paramref name="Body"/> to send, as <c>Content-Length</c>; ignored without a body.</param>
public sealed record S3HttpRequest(
    string Method,
    string Target,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    Stream? Body = null,
    long BodyLength = 0);
