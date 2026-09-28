using System.Globalization;

namespace Curl.Protocol.Imap;

/// <summary>
/// Which untagged responses curl 8.21.0 writes to the output while a <c>LIST</c>, a
/// <c>SEARCH</c> or a <c>-X</c> command waits, and where one of them announces a literal,
/// as measured with <c>Record-CurlExchange.ps1 -Imap</c> and read in its <c>lib/imap.c</c>
/// (BL-556).
/// </summary>
internal static class ImapListedResponses
{
    /// <summary>
    /// The <c>-X</c> commands, matched by their first word in any case, whose every untagged
    /// response is written, whatever its name.
    /// </summary>
    private static readonly HashSet<string> CommandsWritingEveryResponse =
        new(["SELECT", "EXAMINE", "SEARCH", "EXPUNGE", "LSUB", "UID", "GETQUOTAROOT", "NOOP"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the test for an untagged <c>LIST</c> response, the only one a listing writes.</summary>
    public static Func<string, bool> ListWanted { get; } = static line => ImapSession.IsUntaggedResponse(line, "LIST");

    /// <summary>Gets the test for an untagged <c>SEARCH</c> response, the only one a search writes.</summary>
    public static Func<string, bool> SearchWanted { get; } = static line => ImapSession.IsUntaggedResponse(line, "SEARCH");

    /// <summary>
    /// The test for the untagged responses written while <paramref name="customCommand" />
    /// waits: those named by its first word; every one for the commands curl lists
    /// (<c>SELECT</c>, <c>EXAMINE</c>, <c>SEARCH</c>, <c>EXPUNGE</c>, <c>LSUB</c>, <c>UID</c>,
    /// <c>GETQUOTAROOT</c>, <c>NOOP</c>); and <c>FETCH</c> as well for <c>STORE</c>.
    /// </summary>
    /// <param name="customCommand">The decoded <c>-X</c> command.</param>
    /// <returns>Whether an untagged line is written.</returns>
    public static Func<string, bool> CustomWanted(string customCommand)
    {
        string name = customCommand.Split(' ', 2)[0];
        if (CommandsWritingEveryResponse.Contains(name))
        {
            return static _ => true;
        }

        bool isStore = name.Equals("STORE", StringComparison.OrdinalIgnoreCase);
        return line => ImapSession.IsUntaggedResponse(line, name)
            || (isStore && ImapSession.IsUntaggedResponse(line, "FETCH"));
    }

    /// <summary>
    /// The size of the literal <paramref name="line" /> announces: the first <c>{</c> outside
    /// a quoted string (where <c>\</c> escapes the next character), then digits and <c>}</c>.
    /// <see langword="null" /> when there is no such brace or it is not followed by digits and
    /// <c>}</c>; no later brace is looked at.
    /// </summary>
    /// <param name="line">The untagged line, without its LF.</param>
    /// <returns>The literal's size in bytes, or <see langword="null" />.</returns>
    public static long? LiteralSizeOf(string line)
    {
        int open = FirstBraceOutsideQuotes(line);
        if (open < 0)
        {
            return null;
        }

        ReadOnlySpan<char> afterBrace = line.AsSpan(open + 1);
        int digits = afterBrace.IndexOfAnyExceptInRange('0', '9');
        return digits > 0
            && afterBrace[digits] == '}'
            && long.TryParse(afterBrace[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out long size)
                ? size
                : null;
    }

    /// <summary>Where the first <c>{</c> outside a quoted string is, or -1 when there is none.</summary>
    private static int FirstBraceOutsideQuotes(string line)
    {
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char next = line[index];
            if (next == '{' && !quoted)
            {
                return index;
            }

            quoted ^= next == '"';
            index += quoted && next == '\\' ? 1 : 0;
        }

        return -1;
    }
}
