namespace Curl.Protocol.Pop3;

/// <summary>
/// Finds the APOP timestamp (RFC 1939 section 7) in a POP3 greeting, the way curl 8.21.0's
/// <c>pop3_state_servergreet_resp</c> does: the timestamp runs from the greeting's first
/// <c>&lt;</c> to the first <c>&gt;</c> after it, and it must contain <c>@</c>. Text after
/// the <c>&gt;</c> is ignored.
/// </summary>
/// <remarks>
/// Kept for <c>APOP</c> (BL-548), which measures it against real curl; BL-1664 measured the
/// greetings with text after the timestamp.
/// </remarks>
internal static class Pop3ApopTimestamp
{
    /// <summary>
    /// Reads the timestamp from <paramref name="greeting" />.
    /// </summary>
    /// <param name="greeting">The greeting line without its line end.</param>
    /// <returns>The timestamp with its angle brackets, or <see langword="null" /> when there is none.</returns>
    public static string? Read(string greeting)
    {
        int start = greeting.IndexOf('<', StringComparison.Ordinal);
        int end = start < 0 ? -1 : greeting.IndexOf('>', start);
        if (end < 0)
        {
            return null;
        }

        string timestamp = greeting[start..(end + 1)];
        return timestamp.Contains('@', StringComparison.Ordinal) ? timestamp : null;
    }
}
