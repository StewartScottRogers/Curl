namespace Curl.Extension.OAuth2.Flows;

/// <summary>The query parameters of the redirect the browser made to curl's loopback redirect URI (RFC 6749 §4.1.2).</summary>
/// <param name="Parameters">The decoded parameters; the first of a repeated name is kept.</param>
internal sealed record OAuth2Redirect(IReadOnlyDictionary<string, string> Parameters)
{
    /// <summary>Reads the request line of the redirect, such as <c>GET /?code=x&amp;state=y HTTP/1.1</c>.</summary>
    /// <returns>The redirect, or <see langword="null"/> when the line is not an origin-form GET request.</returns>
    public static OAuth2Redirect? ParseRequestLine(string requestLine)
    {
        var parts = requestLine.Split(' ');
        if (parts.Length != 3 || parts[0] != "GET" || !parts[1].StartsWith('/'))
        {
            return null;
        }

        var queryStart = parts[1].IndexOf('?', StringComparison.Ordinal);
        return new OAuth2Redirect(ParseQuery(queryStart < 0 ? string.Empty : parts[1][(queryStart + 1)..]));
    }

    /// <summary>Gets the parameter called <paramref name="name"/>, or <see langword="null"/>.</summary>
    public string? Find(string name) => Parameters.GetValueOrDefault(name);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal);
        foreach (var field in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = field.IndexOf('=', StringComparison.Ordinal);
            var name = separator < 0 ? field : field[..separator];
            var value = separator < 0 ? string.Empty : field[(separator + 1)..];
            parameters.TryAdd(Decode(name), Decode(value));
        }

        return parameters;
    }

    // application/x-www-form-urlencoded (RFC 6749 Appendix B): '+' is a space, then percent-decoding.
    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
