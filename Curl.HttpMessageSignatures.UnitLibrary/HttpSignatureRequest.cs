namespace Curl.HttpMessageSignatures;

/// <summary>
/// The parts of a request curl 8.22.0 signs: the values of the four derived components and the
/// header lines the user gave with <c>-H</c>, the only headers a header component is taken from.
/// </summary>
/// <param name="Method">The request method, such as <c>GET</c>.</param>
/// <param name="Authority">The <c>@authority</c> value: the <c>Host</c> header's value when one is
/// sent, else the host name with <c>:port</c> when the port is not the scheme's default.</param>
/// <param name="Path">The URL path; an empty one is signed as <c>/</c>.</param>
/// <param name="Query">The URL query without its <c>?</c>, or <see langword="null" /> when the URL has none.</param>
/// <param name="HeaderLines">The <c>-H</c> lines as given, such as <c>Content-Type: text/plain</c>.</param>
public sealed record HttpSignatureRequest(
    string Method,
    string Authority,
    string Path,
    string? Query,
    IReadOnlyList<string> HeaderLines)
{
    /// <summary>The <c>@path</c> value: <see cref="Path" />, or <c>/</c> when it is empty.</summary>
    public string SignedPath => Path.Length == 0 ? "/" : Path;

    /// <summary>The <c>@query</c> value: <c>?</c> followed by <see cref="Query" />, or just <c>?</c> (RFC 9421 section 2.2.7).</summary>
    public string SignedQuery => "?" + Query;

    /// <summary>
    /// Whether a header line sets <paramref name="name" />: it starts with the name in any case,
    /// followed by <c>:</c> or <c>;</c>, as curl's <c>Curl_checkheaders</c> matches.
    /// </summary>
    /// <param name="name">The header field name.</param>
    /// <returns><see langword="true" /> when a line names the header.</returns>
    public bool HasHeader(string name) => HeaderLines.Any(line => Names(line, name));

    /// <summary>
    /// Returns the value of header component <paramref name="name" />: every line naming it,
    /// each value after its first colon with spaces and tabs trimmed, joined by <c>", "</c>
    /// (RFC 9421 section 2.1), as upstream's <c>resolve_component</c> does.
    /// </summary>
    /// <param name="name">The lower-case header field name.</param>
    /// <returns>The combined value.</returns>
    /// <exception cref="HttpSignatureException">No line with a colon names the header.</exception>
    public string HeaderValue(string name)
    {
        var values = HeaderLines
            .Where(line => Names(line, name) && line.Contains(':'))
            .Select(line => line[(line.IndexOf(':') + 1)..].Trim([' ', '\t']))
            .ToList();
        return values.Count == 0
            ? throw new HttpSignatureException($"httpsig: header '{name}' not found in request")
            : string.Join(", ", values);
    }

    private static bool Names(string line, string name) =>
        line.Length > name.Length
        && line.StartsWith(name, StringComparison.OrdinalIgnoreCase)
        && line[name.Length] is ':' or ';';
}
