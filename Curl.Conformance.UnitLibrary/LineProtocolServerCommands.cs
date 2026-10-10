using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The <c>REPLY</c>, <c>CAPA</c> and <c>AUTH</c> lines of a case's <c>&lt;servercmd&gt;</c>, as
/// upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) reads them:
/// <c>REPLY &lt;command&gt; &lt;text&gt;</c> replaces the server's answer to that command with the
/// text and a CRLF (command names are matched without regard to case; a later line for the same
/// command wins); <c>CAPA</c> lists the capabilities the server announces, split on spaces outside
/// double quotes with the quotes removed; <c>AUTH</c> lists its authentication mechanisms, split on
/// spaces. A later <c>CAPA</c> or <c>AUTH</c> line replaces an earlier one. Other lines are left for
/// the protocol stand-ins to read.
/// </summary>
internal sealed class LineProtocolServerCommands
{
    private readonly Dictionary<string, byte[]> replies;

    private LineProtocolServerCommands(Dictionary<string, byte[]> replies, string[] capabilities, string[] authenticationMechanisms)
    {
        this.replies = replies;
        Capabilities = capabilities;
        AuthenticationMechanisms = authenticationMechanisms;
    }

    /// <summary>Gets the capabilities a <c>CAPA</c> line lists; empty when there is none.</summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>Gets the authentication mechanisms an <c>AUTH</c> line lists; empty when there is none.</summary>
    public IReadOnlyList<string> AuthenticationMechanisms { get; }

    /// <summary>Reads the <c>REPLY</c>, <c>CAPA</c> and <c>AUTH</c> lines of a <c>&lt;servercmd&gt;</c> body.</summary>
    /// <param name="serverCommands">The part's body; empty when the case has none.</param>
    /// <returns>The replies, by command name, and the capabilities and mechanisms.</returns>
    public static LineProtocolServerCommands Read(ReadOnlySpan<byte> serverCommands)
    {
        Dictionary<string, byte[]> replies = new(StringComparer.OrdinalIgnoreCase);
        string[] capabilities = [];
        string[] authenticationMechanisms = [];
        foreach (string rawLine in Encoding.Latin1.GetString(serverCommands).Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Split(' ', 3) is ["REPLY", var command, var text])
            {
                replies[command] = Encoding.Latin1.GetBytes(text + "\r\n");
            }
            else if (TryFindArgument(line, "CAPA ", out string capabilityList))
            {
                capabilities = SplitOutsideQuotes(capabilityList);
            }
            else if (TryFindArgument(line, "AUTH ", out string mechanismList))
            {
                authenticationMechanisms = mechanismList.Split(' ');
            }
        }

        return new LineProtocolServerCommands(replies, capabilities, authenticationMechanisms);
    }

    /// <summary>Finds the reply <c>&lt;servercmd&gt;</c> gives for a command.</summary>
    /// <param name="command">The command name, such as <c>PASV</c>.</param>
    /// <param name="reply">The reply bytes, with their CRLF, when one is given.</param>
    /// <returns>Whether <c>&lt;servercmd&gt;</c> gives a reply for the command.</returns>
    public bool TryFindReply(string command, out byte[] reply) => replies.TryGetValue(command, out reply!);

    /// <summary>Matches ftpserver.pl's unanchored <c>/KEYWORD (.*)/</c>: the text after the keyword's first appearance.</summary>
    private static bool TryFindArgument(string line, string keyword, out string argument)
    {
        int at = line.IndexOf(keyword, StringComparison.Ordinal);
        argument = at < 0 ? string.Empty : line[(at + keyword.Length)..];
        return at >= 0;
    }

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
