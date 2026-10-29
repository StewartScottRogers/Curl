namespace Curl.Extension.SignalR.Transport;

/// <summary>The status code and header fields of an HTTP/1.1 response (RFC 9112 section 4 and 5).</summary>
/// <param name="StatusCode">The three-digit status code.</param>
/// <param name="Headers">
/// Each field's value by its name, compared without case; a field sent more than once has its values joined by
/// <c>", "</c> (RFC 9110 section 5.3).
/// </param>
internal sealed record HttpResponseHead(int StatusCode, IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>Whether the field <paramref name="name"/> holds <paramref name="token"/> in its comma-separated list, without case.</summary>
    public bool HeaderHasToken(string name, string token) =>
        Headers.TryGetValue(name, out string? value)
        && value.Split(',', StringSplitOptions.TrimEntries).Contains(token, StringComparer.OrdinalIgnoreCase);
}
