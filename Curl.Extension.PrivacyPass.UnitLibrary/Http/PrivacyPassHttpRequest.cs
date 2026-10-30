namespace Curl.Extension.PrivacyPass.Http;

/// <summary>One HTTP/1.1 request to an issuer.</summary>
/// <param name="Method">The method, such as <c>GET</c>, <c>HEAD</c>, <c>POST</c>, <c>PUT</c> or <c>PATCH</c>.</param>
/// <param name="Url">The absolute <c>http</c> or <c>https</c> URL; its path and query go on the request line, its host in <c>Host</c>.</param>
/// <param name="Headers">The fields to send after <c>Host</c>, in order, such as <c>Accept</c>, <c>Authorization</c> or <c>Content-Type</c>.</param>
/// <param name="Body">The body, sent with its <c>Content-Length</c>, or <see langword="null"/> for a request without one.</param>
public sealed record PrivacyPassHttpRequest(
    string Method,
    Uri Url,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    byte[]? Body);
