namespace Curl.Networking;

/// <summary>
/// One <c>--proxy-header</c> value as curl 8.21.0 puts it on the CONNECT request: the name
/// it overrides, and the line it sends, if any (BL-347 Notes, ADR-0077).
/// </summary>
/// <remarks>
/// The name ends at the first colon or semicolon. <c>Name: value</c> sends <c>Name: </c>
/// and the value with the spaces and tabs before it removed; <c>Name:</c>, with nothing but
/// spaces and tabs after the colon, sends nothing. <c>Name;</c>, with the semicolon last,
/// sends <c>Name: </c> with an empty value; anything after that semicolon sends nothing. A
/// value with neither separator, or an empty name, sends nothing and overrides nothing.
/// Every other form overrides curl's own CONNECT header of that name, sent or not.
/// </remarks>
internal readonly struct HttpProxyTunnelHeader
{
    private HttpProxyTunnelHeader(string? name, string? sentLine)
    {
        Name = name;
        SentLine = sentLine;
    }

    /// <summary>
    /// Gets the header name, or <see langword="null" /> when the value names no header.
    /// </summary>
    internal string? Name { get; }

    /// <summary>
    /// Gets the header line to send, without its line ending, or <see langword="null" />
    /// when the value sends nothing.
    /// </summary>
    internal string? SentLine { get; }

    /// <summary>
    /// Reads one <c>--proxy-header</c> value.
    /// </summary>
    /// <param name="entry">The value, verbatim.</param>
    /// <returns>The header.</returns>
    internal static HttpProxyTunnelHeader Parse(string entry)
    {
        int separator = entry.IndexOfAny([':', ';']);
        if (separator <= 0)
        {
            return default;
        }

        string name = entry[..separator];
        string rest = entry[(separator + 1)..];
        string? value = entry[separator] == ':' ? NonEmptyOrNull(rest.TrimStart(' ', '\t')) : rest.Length == 0 ? string.Empty : null;
        return new HttpProxyTunnelHeader(name, value is null ? null : $"{name}: {value}");
    }

    /// <summary>
    /// Tells whether this value names the header <paramref name="name" />, compared without
    /// regard to case, so that it overrides curl's own CONNECT header of that name.
    /// </summary>
    /// <param name="name">A header name, such as <c>Host</c>.</param>
    /// <returns><see langword="true" /> when the value's name is <paramref name="name" />.</returns>
    internal bool Names(string name) => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

    private static string? NonEmptyOrNull(string value) => value.Length == 0 ? null : value;
}
