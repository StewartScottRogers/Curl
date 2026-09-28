namespace Curl.Protocol.Ws;

/// <summary>
/// One <c>-H</c> entry as curl reads it: <c>Name: value</c> is sent as written,
/// <c>Name:</c> with no value removes curl's own header of that name and sends nothing, and
/// <c>Name;</c> sends <c>Name:</c> with an empty value.
/// </summary>
/// <remarks>
/// The same rules as the HTTP library's custom header; a protocol library may not reference
/// another, so the WebSocket library keeps its own copy (ADR-0128).
/// </remarks>
internal readonly struct WsCustomHeader
{
    private const string WhiteSpace = " \t\r\n\v\f";

    private readonly int separatorIndex;

    private WsCustomHeader(string entry, int separatorIndex, string? sentLine)
    {
        Entry = entry;
        this.separatorIndex = separatorIndex;
        SentLine = sentLine;
    }

    /// <summary>Gets the entry as given to <c>-H</c>.</summary>
    internal string Entry { get; }

    /// <summary>Gets the line sent for the entry, or <see langword="null" /> when it sends nothing.</summary>
    internal string? SentLine { get; }

    /// <summary>
    /// Gets the value of a <c>Name: value</c> entry, trimmed, or <see langword="null" /> for an
    /// entry that removes a header or sends an empty one.
    /// </summary>
    internal string? Value =>
        SentLine == Entry ? Entry.AsSpan(separatorIndex + 1).Trim(WhiteSpace).ToString() : null;

    /// <summary>Reads one <c>-H</c> entry.</summary>
    /// <param name="entry">The entry as given to <c>-H</c>.</param>
    /// <returns>The entry, with the line it sends.</returns>
    internal static WsCustomHeader Parse(string entry)
    {
        int colon = entry.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            bool hasValue = !entry.AsSpan(colon + 1).TrimStart(WhiteSpace).IsEmpty;
            return new WsCustomHeader(entry, colon, colon > 0 && hasValue ? entry : null);
        }

        int semicolon = entry.IndexOf(';', StringComparison.Ordinal);
        bool sendsEmpty = semicolon > 0 && semicolon == entry.Length - 1;
        return new WsCustomHeader(entry, semicolon, sendsEmpty ? string.Concat(entry.AsSpan(0, semicolon), ":") : null);
    }

    /// <summary>Determines whether the entry names the header <paramref name="name" />, in any case.</summary>
    /// <param name="name">The header name.</param>
    /// <returns><see langword="true" /> when the entry's name is <paramref name="name" />.</returns>
    internal bool Names(string name) =>
        separatorIndex == name.Length && Entry.StartsWith(name, StringComparison.OrdinalIgnoreCase);
}
