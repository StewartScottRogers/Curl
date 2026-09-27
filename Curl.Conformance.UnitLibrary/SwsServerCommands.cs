using System.Text;

namespace Curl.Conformance;

/// <summary>
/// A test case's <c>&lt;reply&gt;&lt;servercmd&gt;</c> read the way <c>sws_parse_servercmd</c> in
/// upstream's <c>tests/server/sws.c</c> at <c>curl-8_21_0</c> reads it: one command per line,
/// recognised by its prefix, unknown lines ignored.
/// </summary>
internal sealed class SwsServerCommands
{
    private static readonly string[] UnsupportedPrefixes = ["idle", "stream", "connection-monitor", "upgrade"];

    // sscanf patterns such as "delay: %d": the name, a colon, blanks, then a number.
    private static readonly string[] UnsupportedNumberedCommands = ["delay", "writedelay"];

    private SwsServerCommands(bool closesAfterEveryReply, bool requiresAuthorization, bool ignoresExpectedBody, int skippedBodyBytes, IReadOnlyList<string> unsupportedCommands)
    {
        ClosesAfterEveryReply = closesAfterEveryReply;
        RequiresAuthorization = requiresAuthorization;
        IgnoresExpectedBody = ignoresExpectedBody;
        SkippedBodyBytes = skippedBodyBytes;
        UnsupportedCommands = unsupportedCommands;
    }

    /// <summary>Whether <c>swsclose</c> is given, so the server closes the connection after every reply.</summary>
    public bool ClosesAfterEveryReply { get; }

    /// <summary>
    /// Whether <c>auth_required</c> is given, so a request with no <c>Authorization:</c> in it
    /// ends at its headers and its body is not waited for.
    /// </summary>
    public bool RequiresAuthorization { get; }

    /// <summary>
    /// Whether <c>no-expect</c> is given, so a request with an <c>Expect: 100-continue</c> header
    /// ends at its headers whatever its <c>Content-Length</c> says.
    /// </summary>
    public bool IgnoresExpectedBody { get; }

    /// <summary>
    /// The number <c>skip: N</c> gives, the last one when several do, else 0: a request body is
    /// read N bytes shorter than its <c>Content-Length</c> says. A number too large for an
    /// <see cref="int"/> reads as 0.
    /// </summary>
    public int SkippedBodyBytes { get; }

    /// <summary>The name of every command sws knows that this emulation does not carry out, in file order.</summary>
    public IReadOnlyList<string> UnsupportedCommands { get; }

    /// <summary>Reads the commands out of a <c>&lt;servercmd&gt;</c> body.</summary>
    /// <param name="body">The body, after the test file's expansion.</param>
    /// <returns>The commands the body gives.</returns>
    public static SwsServerCommands Read(ReadOnlySpan<byte> body)
    {
        bool closesAfterEveryReply = false;
        bool requiresAuthorization = false;
        bool ignoresExpectedBody = false;
        int skippedBodyBytes = 0;
        List<string> unsupportedCommands = [];
        foreach (string line in Encoding.Latin1.GetString(body).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            closesAfterEveryReply |= line.StartsWith("swsclose", StringComparison.Ordinal);
            requiresAuthorization |= line.StartsWith("auth_required", StringComparison.Ordinal);
            ignoresExpectedBody |= line.StartsWith("no-expect", StringComparison.Ordinal);
            skippedBodyBytes = NumberedCommandValue(line, "skip") ?? skippedBodyBytes;
            if (UnsupportedCommandName(line) is { } name)
            {
                unsupportedCommands.Add(name);
            }
        }

        return new SwsServerCommands(closesAfterEveryReply, requiresAuthorization, ignoresExpectedBody, skippedBodyBytes, unsupportedCommands);
    }

    private static string? UnsupportedCommandName(string line) =>
        UnsupportedPrefixes.FirstOrDefault(prefix => line.StartsWith(prefix, StringComparison.Ordinal))
        ?? UnsupportedNumberedCommands.FirstOrDefault(name => NumberedCommandValue(line, name) is not null);

    // The number after "name:" and any blanks, or null when the line is not that command.
    private static int? NumberedCommandValue(string line, string name) =>
        line.StartsWith(name + ":", StringComparison.Ordinal) ? LeadingInteger(line[(name.Length + 1)..].TrimStart()) : null;

    // What sscanf's %d accepts at the start: an optional sign, then digits.
    private static int? LeadingInteger(string text)
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
}
