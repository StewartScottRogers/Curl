using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The <c>REPLY</c> lines of a case's <c>&lt;servercmd&gt;</c>, as upstream's
/// <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) reads them: <c>REPLY &lt;command&gt; &lt;text&gt;</c>
/// replaces the server's answer to that command with the text and a CRLF. Command names are
/// matched without regard to case; a later line for the same command wins. Other lines are left
/// for the protocol stand-ins to read.
/// </summary>
internal sealed class LineProtocolServerCommands
{
    private readonly Dictionary<string, byte[]> replies;

    private LineProtocolServerCommands(Dictionary<string, byte[]> replies) => this.replies = replies;

    /// <summary>Reads the <c>REPLY</c> lines of a <c>&lt;servercmd&gt;</c> body.</summary>
    /// <param name="serverCommands">The part's body; empty when the case has none.</param>
    /// <returns>The replies, by command name.</returns>
    public static LineProtocolServerCommands Read(ReadOnlySpan<byte> serverCommands)
    {
        Dictionary<string, byte[]> replies = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in Encoding.Latin1.GetString(serverCommands).Split('\n'))
        {
            if (line.TrimEnd('\r').Split(' ', 3) is ["REPLY", var command, var text])
            {
                replies[command] = Encoding.Latin1.GetBytes(text + "\r\n");
            }
        }

        return new LineProtocolServerCommands(replies);
    }

    /// <summary>Finds the reply <c>&lt;servercmd&gt;</c> gives for a command.</summary>
    /// <param name="command">The command name, such as <c>PASV</c>.</param>
    /// <param name="reply">The reply bytes, with their CRLF, when one is given.</param>
    /// <returns>Whether <c>&lt;servercmd&gt;</c> gives a reply for the command.</returns>
    public bool TryFindReply(string command, out byte[] reply) => replies.TryGetValue(command, out reply!);
}
