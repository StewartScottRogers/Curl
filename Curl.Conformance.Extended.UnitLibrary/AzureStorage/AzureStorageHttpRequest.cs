namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>One HTTP/1.1 request curl sent an Azure Storage reference endpoint, as the endpoint read it (RFC 9112).</summary>
/// <param name="Method">The request method.</param>
/// <param name="Path">The request target's path, percent-decoded.</param>
/// <param name="Query">The request target's query parameters, percent-decoded; the last of a repeated name wins.</param>
/// <param name="Headers">The header fields in the order curl sent them.</param>
/// <param name="Body">The body the <c>Content-Length</c> field framed; empty without one.</param>
public sealed record AzureStorageHttpRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    byte[] Body)
{
    /// <summary>Gives the value of the first header field named <paramref name="name" />, compared without case.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null" /> when curl sent no such field.</returns>
    public string? Header(string name) =>
        Headers.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Gives the query parameter named <paramref name="name" />.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The value, or <see langword="null" /> when the query has no such parameter.</returns>
    public string? QueryValue(string name) => Query.GetValueOrDefault(name);
}
