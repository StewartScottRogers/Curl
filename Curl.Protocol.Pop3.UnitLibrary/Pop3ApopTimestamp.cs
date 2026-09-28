namespace Curl.Protocol.Pop3;

/// <summary>
/// Finds the APOP timestamp (RFC 1939 section 7) in a POP3 greeting, the way curl 8.21.0's
/// <c>pop3_state_servergreet_resp</c> does: the greeting must end with <c>&gt;</c>, the
/// timestamp runs from its first <c>&lt;</c> to that end, and it must contain <c>@</c>.
/// </summary>
/// <remarks>
/// Kept for <c>APOP</c> (BL-548), which measures it against real curl.
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
        if (!greeting.EndsWith('>') || start < 0)
        {
            return null;
        }

        string timestamp = greeting[start..];
        return timestamp.Contains('@', StringComparison.Ordinal) ? timestamp : null;
    }
}
