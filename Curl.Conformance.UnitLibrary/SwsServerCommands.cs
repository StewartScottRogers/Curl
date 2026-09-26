using System.Text;

namespace Curl.Conformance;

/// <summary>
/// A test case's <c>&lt;reply&gt;&lt;servercmd&gt;</c> read the way <c>sws_parse_servercmd</c> in
/// upstream's <c>tests/server/sws.c</c> at <c>curl-8_21_0</c> reads it: one command per line,
/// recognised by its prefix, unknown lines ignored.
/// </summary>
internal sealed class SwsServerCommands
{
    private static readonly string[] UnsupportedPrefixes = ["auth_required", "idle", "stream", "connection-monitor", "upgrade", "no-expect"];

    // sscanf patterns such as "skip: %d": the name, a colon, blanks, then a number.
    private static readonly string[] UnsupportedNumberedCommands = ["skip", "delay", "writedelay"];

    private SwsServerCommands(bool closesAfterEveryReply, IReadOnlyList<string> unsupportedCommands)
    {
        ClosesAfterEveryReply = closesAfterEveryReply;
        UnsupportedCommands = unsupportedCommands;
    }

    /// <summary>Whether <c>swsclose</c> is given, so the server closes the connection after every reply.</summary>
    public bool ClosesAfterEveryReply { get; }

    /// <summary>The name of every command sws knows that this emulation does not carry out, in file order.</summary>
    public IReadOnlyList<string> UnsupportedCommands { get; }

    /// <summary>Reads the commands out of a <c>&lt;servercmd&gt;</c> body.</summary>
    /// <param name="body">The body, after the test file's expansion.</param>
    /// <returns>The commands the body gives.</returns>
    public static SwsServerCommands Read(ReadOnlySpan<byte> body)
    {
        bool closesAfterEveryReply = false;
        List<string> unsupportedCommands = [];
        foreach (string line in Encoding.Latin1.GetString(body).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            closesAfterEveryReply |= line.StartsWith("swsclose", StringComparison.Ordinal);
            if (UnsupportedCommandName(line) is { } name)
            {
                unsupportedCommands.Add(name);
            }
        }

        return new SwsServerCommands(closesAfterEveryReply, unsupportedCommands);
    }

    private static string? UnsupportedCommandName(string line) =>
        UnsupportedPrefixes.FirstOrDefault(prefix => line.StartsWith(prefix, StringComparison.Ordinal))
        ?? UnsupportedNumberedCommands.FirstOrDefault(name => IsNumberedCommand(line, name));

    private static bool IsNumberedCommand(string line, string name) =>
        line.StartsWith(name + ":", StringComparison.Ordinal) && StartsWithInteger(line[(name.Length + 1)..].TrimStart());

    // What sscanf's %d accepts at the start: an optional sign, then a digit.
    private static bool StartsWithInteger(string text)
    {
        string unsigned = text.StartsWith('+') || text.StartsWith('-') ? text[1..] : text;
        return unsigned.Length > 0 && char.IsAsciiDigit(unsigned[0]);
    }
}
