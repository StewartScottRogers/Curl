namespace Curl.Extended.Protocol.AzureStorage.Http;

/// <summary>The response to one Azure Storage HTTP/1.1 request.</summary>
/// <param name="StatusCode">The three-digit status code.</param>
/// <param name="ReasonPhrase">The reason phrase, possibly empty.</param>
/// <param name="Headers">The header fields, in the order received, values trimmed of surrounding whitespace.</param>
/// <param name="Body">The body, read from the connection as it is consumed; it ends where the message's framing says (RFC 9112 §6.3).</param>
/// <param name="ConnectionPersists">
/// Whether the connection can carry another request once <paramref name="Body"/> has been read to its end: the
/// response is HTTP/1.1, has no <c>Connection: close</c>, and its body is not delimited by the close (RFC 9112 §9.3).
/// </param>
public sealed record AzureStorageHttpResponse(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    Stream Body,
    bool ConnectionPersists)
{
    /// <summary>Finds the first header field named <paramref name="name"/>, compared case-insensitively.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/> when the response has none.</returns>
    public string? FindHeader(string name) => AzureStorageHttpResponseHead.FindHeader(Headers, name);
}
