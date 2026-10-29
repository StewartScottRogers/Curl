namespace Curl.Extension.OAuth2.Http;

/// <summary>The response from a token or device authorization endpoint, whatever its status.</summary>
/// <param name="StatusCode">The three-digit status code; a 400 or 401 carries the error response (RFC 6749 §5.2).</param>
/// <param name="ReasonPhrase">The reason phrase, possibly empty.</param>
/// <param name="Headers">The header fields, in the order received, values trimmed of surrounding whitespace.</param>
/// <param name="Body">The whole body, decoded as UTF-8 (RFC 8259 §8.1), for the <c>Wire</c> parsers.</param>
public sealed record OAuth2HttpResponse(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Body)
{
    /// <summary>Finds the first header field named <paramref name="name"/>, compared case-insensitively.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/> when the response has none.</returns>
    public string? FindHeader(string name) => OAuth2HttpResponseHead.FindHeader(Headers, name);
}
