namespace Curl.Protocol.Http;

/// <summary>
/// One <c>-H</c>/<c>--header</c> value as curl 8.21.0 reads it: the name it overrides, and
/// the line it sends, if any (BL-172 Notes).
/// </summary>
/// <remarks>
/// <c>Name: value</c> is sent verbatim. <c>Name:</c>, with nothing but white space after
/// the colon, sends nothing. <c>Name;</c>, with the semicolon last, sends <c>Name:</c>
/// with an empty value. Anything after that semicolon, a value with no colon or semicolon,
/// or an empty name sends nothing. Every form with a name, sent or not, overrides the
/// header of that name curl would otherwise generate.
/// </remarks>
internal readonly struct HttpCustomHeader
{
    private readonly int separatorIndex;

    private HttpCustomHeader(string entry, int separatorIndex, string? sentLine)
    {
        Entry = entry;
        this.separatorIndex = separatorIndex;
        SentLine = sentLine;
    }

    /// <summary>
    /// Gets the value exactly as given on the command line.
    /// </summary>
    internal string Entry { get; }

    /// <summary>
    /// Gets the header line to send, without its line ending, or <see langword="null" />
    /// when the value sends nothing.
    /// </summary>
    internal string? SentLine { get; }

    /// <summary>
    /// Reads one <c>-H</c> value.
    /// </summary>
    /// <param name="entry">The value, verbatim.</param>
    /// <returns>The header.</returns>
    internal static HttpCustomHeader Parse(string entry)
    {
        int colon = entry.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            bool hasValue = !entry.AsSpan(colon + 1).TrimStart(" \t\r\n\v\f").IsEmpty;
            return new HttpCustomHeader(entry, colon, colon > 0 && hasValue ? entry : null);
        }

        int semicolon = entry.IndexOf(';', StringComparison.Ordinal);
        bool sendsEmpty = semicolon > 0 && semicolon == entry.Length - 1;
        return new HttpCustomHeader(entry, semicolon, sendsEmpty ? string.Concat(entry.AsSpan(0, semicolon), ":") : null);
    }

    /// <summary>
    /// Tells whether this value names the header <paramref name="name" />, compared without
    /// regard to case, so that it overrides curl's own header of that name.
    /// </summary>
    /// <param name="name">A header name, such as <c>Accept</c>.</param>
    /// <returns><see langword="true" /> when the value's name is <paramref name="name" />.</returns>
    internal bool Names(string name) =>
        separatorIndex == name.Length && Entry.StartsWith(name, StringComparison.OrdinalIgnoreCase);
}
