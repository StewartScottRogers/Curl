using System.Security.Cryptography;
using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The POP3 side of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one
/// connection: the curl banner and <c>+OK</c> line on connect (or <c>&lt;servercmd&gt;</c>'s
/// <c>REPLY welcome</c>), a <c>REPLY</c> line's text for a command it names, else ftpserver.pl's
/// answer to <c>CAPA</c>, <c>APOP</c>, <c>AUTH</c>, <c>USER</c>, <c>PASS</c>, <c>RETR</c>,
/// <c>LIST</c>, <c>DELE</c>, <c>STAT</c>, <c>NOOP</c>, <c>UIDL</c>, <c>TOP</c>, <c>RSET</c> and
/// <c>QUIT</c>, and <c>-ERR &lt;command&gt; is not dealt with!</c> for any other. A line is a
/// command when it is three or four letters, optionally followed by white space and an argument,
/// a lone <c>*</c>, or base64 characters (an authentication response, which is its own command
/// name); any other line is answered <c>-ERR Unrecognized command</c> and the connection closes.
/// <c>RETR</c>, <c>LIST</c> and <c>TOP</c> send the <c>&lt;reply&gt;</c> data as it is written,
/// without dot-stuffing, and end with a lone <c>.</c> line.
/// </summary>
internal sealed class Pop3Responder : ILineProtocolResponder
{
    private const string Timestamp = "<1972.987654321@curl>";

    private const string Password = "secret";

    private static readonly byte[] Banner = Encoding.Latin1.GetBytes(
        "        _   _ ____  _\r\n" +
        "    ___| | | |  _ \\| |\r\n" +
        "   / __| | | | |_) | |\r\n" +
        "  | (__| |_| |  _ {| |___\r\n" +
        "   \\___|\\___/|_| \\_\\_____|\r\n" +
        "+OK curl POP3 server ready to serve \r\n");

    private readonly LineProtocolServerCommands serverCommands;

    private readonly IReadOnlyDictionary<string, byte[]> replyParts;

    private readonly List<string> receivedCommandLines = [];

    private readonly List<string> deletedMessages = [];

    /// <summary>Creates the responder for one connection of a case.</summary>
    /// <param name="serverCommands">The case's <c>&lt;servercmd&gt;</c> <c>REPLY</c>, <c>CAPA</c> and <c>AUTH</c> lines.</param>
    /// <param name="replyParts">The case's <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data2</c> and so on), which <c>RETR</c>, <c>LIST</c> and <c>TOP</c> send.</param>
    public Pop3Responder(LineProtocolServerCommands serverCommands, IReadOnlyDictionary<string, byte[]> replyParts)
    {
        ArgumentNullException.ThrowIfNull(serverCommands);
        ArgumentNullException.ThrowIfNull(replyParts);
        this.serverCommands = serverCommands;
        this.replyParts = replyParts;
        Greeting = serverCommands.TryFindReply("welcome", out byte[] welcome) ? welcome : Banner;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Greeting { get; }

    /// <summary>
    /// Gets every command line answered so far, each with its CRLF, in the order and form
    /// ftpserver.pl writes them to the protocol log <c>&lt;verify&gt;&lt;protocol&gt;</c> compares.
    /// </summary>
    public IReadOnlyList<string> ReceivedCommandLines => receivedCommandLines;

    /// <summary>Gets the message numbers <c>DELE</c> marked since the last <c>RSET</c> or <c>QUIT</c>.</summary>
    public IReadOnlyList<string> DeletedMessages => deletedMessages;

    /// <inheritdoc/>
    public LineProtocolReply Answer(string commandLine)
    {
        receivedCommandLines.Add(commandLine + "\r\n");
        if (!TrySplitCommand(commandLine, out string command, out string? argument))
        {
            return Reply("-ERR Unrecognized command\r\n", closesConnection: true);
        }

        if (serverCommands.TryFindReply(command, out byte[] customReply))
        {
            return new LineProtocolReply(customReply, false);
        }

        return Reply(AnswerByDefault(command, argument), closesConnection: false);
    }

    private static LineProtocolReply Reply(string text, bool closesConnection) =>
        new(Encoding.Latin1.GetBytes(text), closesConnection);

    /// <summary>Whether Perl counts a value true: defined, not empty and not <c>"0"</c>.</summary>
    private static bool IsPerlTrue(string? value) => !string.IsNullOrEmpty(value) && value != "0";

    /// <summary>Splits a line as ftpserver.pl's main loop does for POP3.</summary>
    private static bool TrySplitCommand(string commandLine, out string command, out string? argument)
    {
        int letters = 0;
        while (letters < commandLine.Length && letters < 5 && char.IsAsciiLetter(commandLine[letters]))
        {
            letters++;
        }

        bool endsAfterLetters = letters == commandLine.Length || char.IsWhiteSpace(commandLine[letters]);
        if (letters is 3 or 4 && endsAfterLetters)
        {
            command = commandLine[..letters];
            argument = letters < commandLine.Length ? commandLine[(letters + 1)..] : null;
            return true;
        }

        command = commandLine;
        argument = null;
        return commandLine == "*" || IsBase64Line(commandLine);
    }

    /// <summary>Matches <c>^[A-Z0-9+\/]*={0,2}$</c>, letters in either case.</summary>
    private static bool IsBase64Line(string line)
    {
        string body = line.TrimEnd('=');
        return line.Length - body.Length <= 2 &&
            body.All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '/');
    }

    /// <summary>A multi-line reply: the status line, each line as given, and the lone <c>.</c> line.</summary>
    private static string Listing(string statusLine, IEnumerable<string> lines) =>
        statusLine + string.Concat(lines) + ".\r\n";

    /// <summary>Splits <c>split(/ /, $args, 2)</c> into two values, either empty when missing.</summary>
    private static (string First, string Second) SplitPair(string? argument)
    {
        string[] parts = (argument ?? string.Empty).Split(' ', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    private string AnswerByDefault(string command, string? argument) => command.ToUpperInvariant() switch
    {
        "CAPA" => Capabilities(),
        "APOP" => AuthenticatedPop(argument),
        "AUTH" => AuthenticationMechanisms(),
        "USER" => IsPerlTrue(argument) ? "+OK\r\n" : "-ERR Protocol error\r\n",
        "PASS" => "+OK Login successful\r\n",
        "RETR" => Listing("+OK Mail transfer starts\r\n", [Retrieve(argument)]),
        "LIST" => Listing("+OK Listing starts\r\n", [ReplyData("data")]),
        "DELE" => Delete(argument),
        "STAT" => IsPerlTrue(argument) ? "-ERR Protocol error\r\n" : "+OK 3 4294967800\r\n",
        "NOOP" => IsPerlTrue(argument) ? "-ERR Protocol error\r\n" : "+OK\r\n",
        "UIDL" => UniqueIdentifiers(),
        "TOP" => Top(argument),
        "RSET" => Reset(argument),
        "QUIT" => Quit(),
        _ => $"-ERR {command} is not dealt with!\r\n",
    };

    private bool HasCapability(string capability) => serverCommands.Capabilities.Contains(capability);

    /// <summary>
    /// Answers <c>CAPA</c>: each capability but <c>APOP</c> and a <c>SASL</c> line of the mechanisms,
    /// each followed by two CRLFs as ftpserver.pl sends them, then its implementation line.
    /// </summary>
    private string Capabilities()
    {
        List<string> lines = [.. serverCommands.Capabilities.Where(capability => capability != "APOP")];
        if (serverCommands.AuthenticationMechanisms.Count > 0)
        {
            lines.Add("SASL " + string.Join(' ', serverCommands.AuthenticationMechanisms));
        }

        if (lines.Count == 0)
        {
            return "-ERR Unrecognized command\r\n";
        }

        return Listing(
            "+OK List of capabilities follows\r\n",
            [.. lines.Select(line => line + "\r\n\r\n"), "IMPLEMENTATION POP3 pingpong test server\r\n"]);
    }

    /// <summary>Answers <c>APOP</c>: the digest must be the MD5 of the timestamp and ftpserver.pl's password.</summary>
    private string AuthenticatedPop(string? argument)
    {
        if (!HasCapability("APOP"))
        {
            return "-ERR Unrecognized command\r\n";
        }

        (string user, string secret) = SplitPair(argument);
        if (user.Length == 0 || secret.Length == 0)
        {
            return "-ERR Protocol error\r\n";
        }

#pragma warning disable CA5351 // APOP is defined on MD5; this is the test server's check, not a protection.
        string digest = Convert.ToHexStringLower(MD5.HashData(Encoding.Latin1.GetBytes(Timestamp + Password)));
#pragma warning restore CA5351
        return secret == digest ? "+OK Login successful\r\n" : "-ERR Login failure\r\n";
    }

    private string AuthenticationMechanisms()
    {
        IReadOnlyList<string> mechanisms = serverCommands.AuthenticationMechanisms;
        return mechanisms.Count == 0
            ? "-ERR Unrecognized command\r\n"
            : Listing("+OK List of supported mechanisms follows\r\n", mechanisms.Select(mechanism => mechanism + "\r\n"));
    }

    /// <summary>The message <c>RETR</c> sends: the proof line for <c>verifiedserver</c>, else the reply data for the message number.</summary>
    private string Retrieve(string? messageNumber) =>
        messageNumber == "verifiedserver"
            ? $"WE ROOLZ: {Environment.ProcessId}\r\n"
            : LineProtocolReplyData.Select(replyParts, messageNumber);

    private string ReplyData(string partName) =>
        replyParts.TryGetValue(partName, out byte[]? part) ? Encoding.Latin1.GetString(part) : string.Empty;

    private string Delete(string? messageNumber)
    {
        if (!IsPerlTrue(messageNumber))
        {
            return "-ERR Protocol error\r\n";
        }

        deletedMessages.Add(messageNumber!);
        return "+OK\r\n";
    }

    /// <summary>Answers <c>UIDL</c> with ftpserver.pl's built-in list, when <c>CAPA</c> names it.</summary>
    private string UniqueIdentifiers() =>
        HasCapability("UIDL")
            ? Listing("+OK Listing starts\r\n", ["1 1\r\n", "2 2\r\n", "3 4\r\n"])
            : "-ERR Unrecognized command\r\n";

    /// <summary>Answers <c>TOP</c>, when <c>CAPA</c> names it: the whole reply data for the message number, whatever the line count.</summary>
    private string Top(string? argument)
    {
        if (!HasCapability("TOP"))
        {
            return "-ERR Unrecognized command\r\n";
        }

        (string messageNumber, string lineCount) = SplitPair(argument);
        return messageNumber.Length == 0 || lineCount.Length == 0
            ? "-ERR Protocol error\r\n"
            : Listing("+OK Mail transfer starts\r\n", [LineProtocolReplyData.Select(replyParts, messageNumber)]);
    }

    private string Reset(string? argument)
    {
        if (IsPerlTrue(argument))
        {
            return "-ERR Protocol error\r\n";
        }

        deletedMessages.Clear();
        return "+OK\r\n";
    }

    private string Quit()
    {
        deletedMessages.Clear();
        return "+OK curl POP3 server signing off\r\n";
    }
}
