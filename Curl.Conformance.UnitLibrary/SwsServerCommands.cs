using System.Text;

namespace Curl.Conformance;

/// <summary>
/// A test case's <c>&lt;reply&gt;&lt;servercmd&gt;</c> read the way <c>sws_parse_servercmd</c> in
/// upstream's <c>tests/server/sws.c</c> at <c>curl-8_21_0</c> reads it: one command per line,
/// recognised by its prefix, unknown lines ignored.
/// </summary>
internal sealed class SwsServerCommands
{
    private readonly List<string> unsupportedCommands = [];

    private SwsServerCommands()
    {
    }

    /// <summary>Whether <c>swsclose</c> is given, so the server closes the connection after every reply.</summary>
    public bool ClosesAfterEveryReply { get; private set; }

    /// <summary>
    /// Whether <c>auth_required</c> is given, so a request with no <c>Authorization:</c> in it
    /// ends at its headers and its body is not waited for.
    /// </summary>
    public bool RequiresAuthorization { get; private set; }

    /// <summary>
    /// Whether <c>no-expect</c> is given, so a request with an <c>Expect: 100-continue</c> header
    /// ends at its headers whatever its <c>Content-Length</c> says.
    /// </summary>
    public bool IgnoresExpectedBody { get; private set; }

    /// <summary>
    /// The number <c>skip: N</c> gives, the last one when several do, else 0: a request body is
    /// read N bytes shorter than its <c>Content-Length</c> says. A number too large for an
    /// <see cref="int"/> reads as 0.
    /// </summary>
    public int SkippedBodyBytes { get; private set; }

    /// <summary>How a request is answered: <c>idle</c> or <c>stream</c>, the last one given, else normally.</summary>
    public SwsReplyMode ReplyMode { get; private set; }

    /// <summary>
    /// Whether <c>connection-monitor</c> is given, so <c>[DISCONNECT]</c> and a line feed are
    /// recorded when a connection that carried a request closes.
    /// </summary>
    public bool MonitorsConnections { get; private set; }

    /// <summary>
    /// Whether <c>upgrade</c> is given, so a request with <c>Upgrade:</c> in it ends at its
    /// headers and switches the connection away from HTTP/1 after the reply.
    /// </summary>
    public bool AllowsUpgrade { get; private set; }

    /// <summary>
    /// The number <c>writedelay: N</c> gives, the last one when several do, else 0: the
    /// milliseconds sws waits after each write of up to 20 reply bytes.
    /// </summary>
    public int MillisecondsAfterEachWrite { get; private set; }

    /// <summary>The name of every command sws knows that this emulation does not carry out, in file order.</summary>
    public IReadOnlyList<string> UnsupportedCommands => unsupportedCommands;

    /// <summary>Reads the commands out of a <c>&lt;servercmd&gt;</c> body.</summary>
    /// <param name="body">The body, after the test file's expansion.</param>
    /// <returns>The commands the body gives.</returns>
    public static SwsServerCommands Read(ReadOnlySpan<byte> body)
    {
        SwsServerCommands commands = new();
        foreach (string line in Encoding.Latin1.GetString(body).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            commands.ReadFlag(line);
            commands.ReadNumber(line);
        }

        return commands;
    }

    /// <summary>What sscanf's <c>%d</c> accepts at the start of <paramref name="text"/>: an optional sign, then digits.</summary>
    /// <param name="text">The text, its leading blanks already skipped.</param>
    /// <returns>The number, 0 when it is too large for an <see cref="int"/>, or <see langword="null"/> when there are no digits.</returns>
    public static int? LeadingInteger(string text)
    {
        int signLength = text.StartsWith('+') || text.StartsWith('-') ? 1 : 0;
        int end = signLength;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        if (end == signLength)
        {
            return null;
        }

        _ = int.TryParse(text[..end], out int value);
        return value;
    }

    // The number after "name:" and any blanks, or null when the line is not that command.
    private static int? NumberedCommandValue(string line, string name) =>
        line.StartsWith(name + ":", StringComparison.Ordinal) ? LeadingInteger(line[(name.Length + 1)..].TrimStart()) : null;

    private static bool IsCommand(string line, string name) => line.StartsWith(name, StringComparison.Ordinal);

    private void ReadFlag(string line)
    {
        ClosesAfterEveryReply |= IsCommand(line, "swsclose");
        RequiresAuthorization |= IsCommand(line, "auth_required");
        IgnoresExpectedBody |= IsCommand(line, "no-expect");
        MonitorsConnections |= IsCommand(line, "connection-monitor");
        AllowsUpgrade |= IsCommand(line, "upgrade");
        ReplyMode = IsCommand(line, "idle") ? SwsReplyMode.Idle : IsCommand(line, "stream") ? SwsReplyMode.Stream : ReplyMode;
    }

    private void ReadNumber(string line)
    {
        SkippedBodyBytes = NumberedCommandValue(line, "skip") ?? SkippedBodyBytes;
        MillisecondsAfterEachWrite = NumberedCommandValue(line, "writedelay") ?? MillisecondsAfterEachWrite;

        // delay: N is kept unsupported; see this library's CLAUDE.md.
        if (NumberedCommandValue(line, "delay") is not null)
        {
            unsupportedCommands.Add("delay");
        }
    }
}
