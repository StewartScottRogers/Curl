using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The SMTP side of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one
/// connection: the <c>220</c> curl banner on connect (or <c>&lt;servercmd&gt;</c>'s
/// <c>REPLY welcome</c>), a <c>REPLY</c> line's text for a command it names, else ftpserver.pl's
/// answer to <c>EHLO</c> (with the <c>CAPA</c> and <c>AUTH</c> lines), <c>HELO</c>, <c>MAIL</c>,
/// <c>RCPT</c>, <c>DATA</c>, <c>RSET</c>, <c>VRFY</c>, <c>EXPN</c>, <c>NOOP</c>, <c>HELP</c> and
/// <c>QUIT</c>, and <c>500 &lt;command&gt; is not dealt with!</c> for any other, <c>AUTH</c>
/// included: ftpserver.pl answers an SMTP authentication exchange only through <c>REPLY</c> lines.
/// A line is a command when it is three or four letters, optionally followed by white space and an
/// argument, a lone <c>*</c>, or up to 512 base64 characters (an authentication response, which is
/// its own command name); any other line is answered <c>500 Unrecognized command</c> and the
/// connection closes. After <c>DATA</c>'s <c>354</c>, lines up to the lone <c>.</c> are the message.
/// </summary>
internal sealed class SmtpResponder : ILineProtocolResponder
{
    private static readonly byte[] Banner = Encoding.Latin1.GetBytes(
        "220-        _   _ ____  _\r\n" +
        "220-    ___| | | |  _ \\| |\r\n" +
        "220-   / __| | | | |_) | |\r\n" +
        "220-  | (__| |_| |  _ {| |___\r\n" +
        "220    \\___|\\___/|_| \\_\\_____|\r\n");

    private readonly LineProtocolServerCommands serverCommands;

    private readonly IReadOnlyDictionary<string, byte[]> replyParts;

    private readonly List<string> receivedCommandLines = [];

    private readonly StringBuilder upload = new();

    private readonly Dictionary<string, Func<string?, string>> defaultAnswers;

    private bool receivingMessage;

    private string serverType = string.Empty;

    private string? client;

    /// <summary>Creates the responder for one connection of a case.</summary>
    /// <param name="serverCommands">The case's <c>&lt;servercmd&gt;</c> <c>REPLY</c>, <c>CAPA</c> and <c>AUTH</c> lines.</param>
    /// <param name="replyParts">The case's <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data2</c> and so on), which <c>VRFY</c> and <c>EXPN</c> send.</param>
    public SmtpResponder(LineProtocolServerCommands serverCommands, IReadOnlyDictionary<string, byte[]> replyParts)
    {
        ArgumentNullException.ThrowIfNull(serverCommands);
        ArgumentNullException.ThrowIfNull(replyParts);
        this.serverCommands = serverCommands;
        this.replyParts = replyParts;
        defaultAnswers = CreateDefaultAnswers();
        Greeting = serverCommands.TryFindReply("welcome", out byte[] welcome) ? welcome : Banner;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Greeting { get; }

    /// <summary>
    /// Gets every command line answered so far, each with its CRLF, in the order and form
    /// ftpserver.pl writes them to the protocol log <c>&lt;verify&gt;&lt;protocol&gt;</c> compares;
    /// the lines of a <c>DATA</c> message are not among them.
    /// </summary>
    public IReadOnlyList<string> ReceivedCommandLines => receivedCommandLines;

    /// <summary>
    /// Gets the last <c>DATA</c> message as ftpserver.pl stores it for <c>&lt;verify&gt;&lt;upload&gt;</c>:
    /// every line as sent, dots not unstuffed, each with a CRLF, through the terminating <c>.</c> line.
    /// </summary>
    public ReadOnlyMemory<byte> UploadedMessage => Encoding.Latin1.GetBytes(upload.ToString());

    private bool SupportsSmtpUtf8 => serverCommands.Capabilities.Contains("SMTPUTF8");

    /// <inheritdoc/>
    public LineProtocolReply Answer(string commandLine)
    {
        if (receivingMessage)
        {
            return ReceiveMessageLine(commandLine);
        }

        receivedCommandLines.Add(commandLine + "\r\n");
        if (!TrySplitCommand(commandLine, out string command, out string? argument))
        {
            return Reply("500 Unrecognized command\r\n", closesConnection: true);
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

    /// <summary>Splits a line as ftpserver.pl's main loop does for SMTP.</summary>
    private static bool TrySplitCommand(string commandLine, out string command, out string? argument)
    {
        int letters = CountLeadingLetters(commandLine);
        if (letters is 3 or 4 && EndsAfterLetters(commandLine, letters))
        {
            command = commandLine[..letters];
            argument = letters < commandLine.Length ? commandLine[(letters + 1)..] : null;
            return true;
        }

        command = commandLine;
        argument = null;
        return commandLine == "*" || IsBase64Line(commandLine);
    }

    /// <summary>Counts the ASCII letters a line starts with, stopping at five.</summary>
    private static int CountLeadingLetters(string commandLine)
    {
        int letters = 0;
        while (letters < commandLine.Length && letters < 5 && char.IsAsciiLetter(commandLine[letters]))
        {
            letters++;
        }

        return letters;
    }

    private static bool EndsAfterLetters(string commandLine, int letters) =>
        letters == commandLine.Length || char.IsWhiteSpace(commandLine[letters]);

    /// <summary>Matches <c>^[A-Z0-9+\/]{0,512}={0,2}$</c>, letters in either case.</summary>
    private static bool IsBase64Line(string line)
    {
        string body = line.TrimEnd('=');
        return line.Length - body.Length <= 2 && body.Length <= 512 &&
            body.All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '/');
    }

    /// <summary>Matches <c>[a-zA-Z0-9._%+-]+</c>, with bytes 0x80 to 0xff too under <c>SMTPUTF8</c>.</summary>
    private static bool IsLocalPart(string text, bool smtpUtf8) =>
        text.Length > 0 && text.All(character => IsAddressCharacter(character, "._%+-", smtpUtf8));

    /// <summary>Matches <c>local@(label.)+tld</c>, the tld two to four ASCII letters.</summary>
    private static bool IsAddress(string text, bool smtpUtf8)
    {
        int at = text.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 && IsLocalPart(text[..at], smtpUtf8) && IsDomain(text[(at + 1)..], smtpUtf8);
    }

    private static bool IsDomain(string text, bool smtpUtf8)
    {
        string[] labels = text.Split('.');
        return labels.Length >= 2 && IsTopLevelDomain(labels[^1]) &&
            labels[..^1].All(label => IsDomainLabel(label, smtpUtf8));
    }

    private static bool IsTopLevelDomain(string label) => label.Length is >= 2 and <= 4 && label.All(char.IsAsciiLetter);

    private static bool IsDomainLabel(string label, bool smtpUtf8) =>
        label.Length > 0 && label.All(character => IsAddressCharacter(character, "-", smtpUtf8));

    private static bool IsAddressCharacter(char character, string punctuation, bool smtpUtf8) =>
        char.IsAsciiLetterOrDigit(character) || punctuation.Contains(character, StringComparison.Ordinal) ||
        (smtpUtf8 && character is >= '\x80' and <= '\xff');

    private string AnswerByDefault(string command, string? argument) =>
        defaultAnswers.TryGetValue(command, out Func<string?, string>? answer)
            ? answer(argument)
            : $"500 {command} is not dealt with!\r\n";

    /// <summary>Maps each command ftpserver.pl answers for SMTP to the method that builds its answer.</summary>
    private Dictionary<string, Func<string?, string>> CreateDefaultAnswers() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["EHLO"] = argument => Hello(argument, "ESMTP"),
        ["HELO"] = argument => Hello(argument, "SMTP"),
        ["MAIL"] = Mail,
        ["RCPT"] = Recipient,
        ["DATA"] = Data,
        ["NOOP"] = argument => IsPerlTrue(argument) ? "501 Unrecognized parameter\r\n" : "250 OK\r\n",
        ["RSET"] = argument => IsPerlTrue(argument) ? "501 Unrecognized parameter\r\n" : "250 Resetting\r\n",
        ["HELP"] = _ => Help(),
        ["VRFY"] = Verify,
        ["EXPN"] = argument => IsPerlTrue(argument) ? ClientReplyData() : "501 Unrecognized parameter\r\n",
        ["QUIT"] = _ => $"221 curl {serverType} server signing off\r\n",
    };

    /// <summary>Answers <c>EHLO</c> or <c>HELO</c>; <c>EHLO</c> lists the capabilities and an <c>AUTH</c> line of the mechanisms.</summary>
    private string Hello(string? argument, string type)
    {
        client = IsPerlTrue(argument) ? argument : "[127.0.0.1]";
        serverType = type;
        List<string> lines = [$"{type} pingpong test server Hello {client}"];
        if (type == "ESMTP")
        {
            lines.AddRange(serverCommands.Capabilities);
            if (serverCommands.AuthenticationMechanisms.Count > 0)
            {
                lines.Add("AUTH " + string.Join(' ', serverCommands.AuthenticationMechanisms));
            }
        }

        StringBuilder reply = new();
        for (int index = 0; index < lines.Count; index++)
        {
            reply.Append(index < lines.Count - 1 ? "250-" : "250 ").Append(lines[index]).Append("\r\n");
        }

        return reply.ToString();
    }

    /// <summary>Answers <c>MAIL</c>: a <c>FROM:</c> parameter is needed, and a <c>SIZE=</c> over a <c>CAPA</c>'s <c>SIZE</c> is refused.</summary>
    private string Mail(string? argument)
    {
        if (!IsPerlTrue(argument))
        {
            return "501 Unrecognized parameter\r\n";
        }

        (string from, long size) = ReadMailParameters(argument!);
        if (from.Length == 0)
        {
            return "501 Invalid address\r\n";
        }

        return size > MaximumMessageSize() ? "552 Message size too large\r\n" : "250 Sender OK\r\n";
    }

    /// <summary>Reads the last <c>FROM:</c> address and the last numeric <c>SIZE=</c> of <c>MAIL</c>'s parameters; the others are ignored.</summary>
    private static (string From, long Size) ReadMailParameters(string argument)
    {
        string from = string.Empty;
        long size = 0;
        foreach (string element in argument.Split(' '))
        {
            if (element.StartsWith("FROM:", StringComparison.Ordinal))
            {
                from = element[5..];
            }
            else if (element.StartsWith("SIZE=", StringComparison.Ordinal) && long.TryParse(element[5..], out long parsed))
            {
                size = parsed;
            }
        }

        return (from, size);
    }

    /// <summary>The first <c>SIZE &lt;n&gt;</c> capability's limit; unlimited when there is none.</summary>
    private long MaximumMessageSize()
    {
        string? capability = serverCommands.Capabilities.FirstOrDefault(item => item.StartsWith("SIZE ", StringComparison.Ordinal));
        return capability is not null && long.TryParse(capability[5..], out long limit) ? limit : long.MaxValue;
    }

    /// <summary>Answers <c>RCPT</c>: <c>TO:</c> and an address in angle brackets.</summary>
    private string Recipient(string? argument)
    {
        if (argument is null || !argument.StartsWith("TO:", StringComparison.Ordinal))
        {
            return "501 Unrecognized parameter\r\n";
        }

        return IsBracketedAddress(argument[3..], SupportsSmtpUtf8) ? "250 Recipient OK\r\n" : "501 Invalid address\r\n";
    }

    private static bool IsBracketedAddress(string text, bool smtpUtf8) =>
        text.Length > 2 && text[0] == '<' && text[^1] == '>' && IsAddress(text[1..^1], smtpUtf8);

    /// <summary>Answers <c>DATA</c>; with no argument and a numeric (or no) client name, starts receiving the message.</summary>
    private string Data(string? argument)
    {
        if (IsPerlTrue(argument))
        {
            return "501 Unrecognized parameter\r\n";
        }

        if (client is not null && !client.All(char.IsAsciiDigit))
        {
            return "501 Invalid arguments\r\n";
        }

        receivingMessage = true;
        upload.Clear();
        return "354 Show me the mail\r\n";
    }

    private LineProtocolReply ReceiveMessageLine(string line)
    {
        upload.Append(line).Append("\r\n");
        if (line != ".")
        {
            return Reply(string.Empty, closesConnection: false);
        }

        receivingMessage = false;
        return Reply("250 OK, data received!\r\n", closesConnection: false);
    }

    private string Help()
    {
        if (client == "verifiedserver")
        {
            return $"214 WE ROOLZ: {Environment.ProcessId}\r\n";
        }

        string authentication = serverCommands.AuthenticationMechanisms.Count > 0 ? " AUTH" : string.Empty;
        return "214-This server supports the following commands:\r\n" +
            $"214 HELO EHLO RCPT DATA RSET MAIL VRFY EXPN QUIT HELP{authentication}\r\n";
    }

    /// <summary>Answers <c>VRFY</c>: a user name or address, answered from the reply data or as an address at example.com.</summary>
    private string Verify(string? argument)
    {
        string userName = (argument ?? string.Empty).Split(' ', 2)[0];
        if (userName.Length == 0)
        {
            return "501 Unrecognized parameter\r\n";
        }

        bool smtpUtf8 = SupportsSmtpUtf8;
        if (!IsUserNameOrAddress(userName, smtpUtf8))
        {
            return "501 Invalid address\r\n";
        }

        string replyData = ClientReplyData();
        if (replyData.Length > 0)
        {
            return replyData;
        }

        return IsAddress(userName, smtpUtf8: false) ? $"250 <{userName}>\r\n" : $"250 <{userName}@example.com>\r\n";
    }

    private static bool IsUserNameOrAddress(string text, bool smtpUtf8) => IsLocalPart(text, smtpUtf8) || IsAddress(text, smtpUtf8);

    /// <summary>
    /// The reply data ftpserver.pl's <c>getreplydata</c> picks for the client name: its number with
    /// any leading non-digits removed, <c>&lt;dataN&gt;</c> for a number over 10000 whose last four
    /// digits are N, else (or when that part is empty) <c>&lt;data&gt;</c>.
    /// </summary>
    private string ClientReplyData() => LineProtocolReplyData.Select(replyParts, client);
}
