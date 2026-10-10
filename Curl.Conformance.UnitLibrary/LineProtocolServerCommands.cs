using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The <c>REPLY</c>, <c>CAPA</c> and <c>AUTH</c> lines of a case's <c>&lt;servercmd&gt;</c>, as
/// upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) reads them:
/// <c>REPLY &lt;command&gt; &lt;text&gt;</c> replaces the server's answer to that command with the
/// text and a CRLF (command names are matched without regard to case; a later line for the same
/// command wins); <c>CAPA</c> lists the capabilities the server announces, split on spaces outside
/// double quotes with the quotes removed; <c>AUTH</c> lists its authentication mechanisms, split on
/// spaces; <c>POSTFETCH &lt;text&gt;</c> gives the text IMAP sends before the <c>)</c> that ends a
/// <c>FETCH</c> response. A later <c>CAPA</c>, <c>AUTH</c> or <c>POSTFETCH</c> line replaces an earlier one. Other lines are left for
/// the protocol stand-ins to read.
/// </summary>
internal sealed class LineProtocolServerCommands
{
    private readonly Dictionary<string, byte[]> replies;

    private LineProtocolServerCommands(Dictionary<string, byte[]> replies, string[] capabilities, string[] authenticationMechanisms, string postFetch)
    {
        this.replies = replies;
        Capabilities = capabilities;
        AuthenticationMechanisms = authenticationMechanisms;
        PostFetch = postFetch;
    }

    /// <summary>Gets the capabilities a <c>CAPA</c> line lists; empty when there is none.</summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>Gets the authentication mechanisms an <c>AUTH</c> line lists; empty when there is none.</summary>
    public IReadOnlyList<string> AuthenticationMechanisms { get; }

    /// <summary>Gets the text a <c>POSTFETCH</c> line gives; empty when there is none.</summary>
    public string PostFetch { get; }

    /// <summary>Reads the <c>REPLY</c>, <c>CAPA</c>, <c>AUTH</c> and <c>POSTFETCH</c> lines of a <c>&lt;servercmd&gt;</c> body.</summary>
    /// <param name="serverCommands">The part's body; empty when the case has none.</param>
    /// <returns>The replies, by command name, and the capabilities and mechanisms.</returns>
    public static LineProtocolServerCommands Read(ReadOnlySpan<byte> serverCommands)
    {
        List<string> lines = [.. Encoding.Latin1.GetString(serverCommands).Split('\n').Select(rawLine => rawLine.TrimEnd('\r'))];
        List<(string Keyword, string Argument)> keywordLines = ReadKeywordLines(lines);
        return new LineProtocolServerCommands(
            ReadReplies(lines),
            ReadCapabilities(keywordLines),
            LastArgument(keywordLines, "AUTH ")?.Split(' ') ?? [],
            LastArgument(keywordLines, "POSTFETCH ") ?? string.Empty);
    }

    private static Dictionary<string, byte[]> ReadReplies(List<string> lines)
    {
        Dictionary<string, byte[]> replies = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            if (TryReadReply(line, out string command, out byte[] reply))
            {
                replies[command] = reply;
            }
        }

        return replies;
    }

    private static List<(string Keyword, string Argument)> ReadKeywordLines(List<string> lines) =>
        [.. lines.Where(line => !TryReadReply(line, out _, out _)).Select(FindKeywordLine).OfType<(string, string)>()];

    private static string[] ReadCapabilities(List<(string Keyword, string Argument)> keywordLines) =>
        LastArgument(keywordLines, "CAPA ") is { } capabilityList ? SplitOutsideQuotes(capabilityList) : [];
    /// <summary>Finds the reply <c>&lt;servercmd&gt;</c> gives for a command.</summary>
    /// <param name="command">The command name, such as <c>PASV</c>.</param>
    /// <param name="reply">The reply bytes, with their CRLF, when one is given.</param>
    /// <returns>Whether <c>&lt;servercmd&gt;</c> gives a reply for the command.</returns>
    public bool TryFindReply(string command, out byte[] reply) => replies.TryGetValue(command, out reply!);

    /// <summary>Reads <c>REPLY &lt;command&gt; &lt;text&gt;</c>: the command and the text with its CRLF.</summary>
    private static bool TryReadReply(string line, out string command, out byte[] reply)
    {
        bool isReply = line.Split(' ', 3) is ["REPLY", _, _];
        string[] fields = line.Split(' ', 3);
        command = isReply ? fields[1] : string.Empty;
        reply = isReply ? Encoding.Latin1.GetBytes(PerlDoubleQuoted(fields[2]) + "\r\n") : [];
        return isReply;
    }

    /// <summary>
    /// Reads reply text as ftpserver.pl's <c>eval "qq{...}"</c> does: <c>\r</c>, <c>\n</c> and
    /// <c>\t</c> are control characters and a backslash before any other character stands for that
    /// character, so <c>\r\n</c> splits a reply into lines and <c>\@</c> is <c>@</c>; a lone
    /// backslash at the end stays.
    /// </summary>
    private static string PerlDoubleQuoted(string text)
    {
        StringBuilder value = new();
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '\\' && index + 1 < text.Length)
            {
                index++;
                character = PerlEscaped(text[index]);
            }

            value.Append(character);
        }

        return value.ToString();
    }

    /// <summary>The character a backslash before <paramref name="escaped"/> stands for in a Perl double-quoted string.</summary>
    private static char PerlEscaped(char escaped) =>
        escaped switch { 'r' => '\r', 'n' => '\n', 't' => '\t', _ => escaped };

    /// <summary>Finds the first of <c>POSTFETCH</c>, <c>CAPA</c> and <c>AUTH</c> a line holds, as the first matching branch of ftpserver.pl's chain does.</summary>
    private static (string Keyword, string Argument)? FindKeywordLine(string line)
    {
        foreach (string keyword in new[] { "POSTFETCH ", "CAPA ", "AUTH " })
        {
            int at = line.IndexOf(keyword, StringComparison.Ordinal);
            if (at >= 0)
            {
                return (keyword, line[(at + keyword.Length)..]);
            }
        }

        return null;
    }

    /// <summary>The argument of the last line for a keyword (a later line replaces an earlier one), or <see langword="null"/> when there is none.</summary>
    private static string? LastArgument(List<(string Keyword, string Argument)> keywordLines, string keyword) =>
        keywordLines.LastOrDefault(keywordLine => keywordLine.Keyword == keyword).Argument;
    /// <summary>Splits on spaces outside double quotes and strips a value's enclosing quotes, as for <c>CAPA "SIZE 32"</c>.</summary>
    private static string[] SplitOutsideQuotes(string list)
    {
        List<string> values = [];
        StringBuilder value = new();
        bool quoted = false;
        foreach (char character in list)
        {
            if (character == ' ' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
                continue;
            }

            quoted ^= character == '"';
            value.Append(character);
        }

        values.Add(value.ToString());
        return [.. values.Select(item => item.Length >= 2 && item[0] == '"' && item[^1] == '"' ? item[1..^1] : item)];
    }
}
