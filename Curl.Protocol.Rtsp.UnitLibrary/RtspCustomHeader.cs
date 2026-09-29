namespace Curl.Protocol.Rtsp;

/// <summary>
/// One <c>-H</c> entry as curl reads it: <c>Name: value</c> is sent as written,
/// <c>Name:</c> with no value removes curl's own header of that name and sends nothing, and
/// <c>Name;</c> sends <c>Name:</c> with an empty value.
/// </summary>
/// <remarks>
/// The same rules as the HTTP and WebSocket libraries' custom headers; a protocol library may
/// not reference another, so the RTSP library keeps its own copy (ADR-0169). Measured on curl
/// 8.21.0 over RTSP (BL-591): <c>-H 'X-Empty;'</c> sends <c>X-Empty:</c>, <c>-H 'X-Gone:'</c>
/// sends nothing, and <c>-H 'User-Agent:'</c> removes curl's <c>User-Agent</c>.
/// </remarks>
internal readonly struct RtspCustomHeader
{
    private const string WhiteSpace = " \t\r\n\v\f";

    private readonly string entry;

    private readonly int separatorIndex;

    private RtspCustomHeader(string entry, int separatorIndex, string? sentLine)
    {
        this.entry = entry;
        this.separatorIndex = separatorIndex;
        SentLine = sentLine;
    }

    /// <summary>Gets the line sent for the entry, or <see langword="null" /> when it sends nothing.</summary>
    internal string? SentLine { get; }

    /// <summary>Reads one <c>-H</c> entry.</summary>
    /// <param name="entry">The entry as given to <c>-H</c>.</param>
    /// <returns>The entry, with the line it sends.</returns>
    internal static RtspCustomHeader Parse(string entry)
    {
        int colon = entry.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            bool hasValue = !entry.AsSpan(colon + 1).TrimStart(WhiteSpace).IsEmpty;
            return new RtspCustomHeader(entry, colon, colon > 0 && hasValue ? entry : null);
        }

        int semicolon = entry.IndexOf(';', StringComparison.Ordinal);
        bool sendsEmpty = semicolon > 0 && semicolon == entry.Length - 1;
        return new RtspCustomHeader(entry, semicolon, sendsEmpty ? string.Concat(entry.AsSpan(0, semicolon), ":") : null);
    }

    /// <summary>Determines whether the entry names the header <paramref name="name" />, in any case.</summary>
    /// <param name="name">The header name.</param>
    /// <returns><see langword="true" /> when the entry's name is <paramref name="name" />.</returns>
    internal bool Names(string name) =>
        separatorIndex == name.Length && entry.StartsWith(name, StringComparison.OrdinalIgnoreCase);
}
