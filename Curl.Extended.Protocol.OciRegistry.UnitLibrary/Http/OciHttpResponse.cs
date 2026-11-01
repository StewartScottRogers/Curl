namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>The final response from a registry or its token endpoint, whatever its status.</summary>
/// <param name="StatusCode">The three-digit status code; a 401 carries the <c>WWW-Authenticate</c> challenge.</param>
/// <param name="ReasonPhrase">The reason phrase, possibly empty.</param>
/// <param name="Headers">The header fields, in the order received, values trimmed of surrounding whitespace.</param>
/// <param name="Body">The whole body; empty for a response to <c>HEAD</c>, a 204 or a 304.</param>
public sealed record OciHttpResponse(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    byte[] Body)
{
    /// <summary>Finds the first header field named <paramref name="name"/>, compared case-insensitively.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/> when the response has none.</returns>
    public string? FindHeader(string name) => OciHttpResponseHead.FindHeader(Headers, name);
}
