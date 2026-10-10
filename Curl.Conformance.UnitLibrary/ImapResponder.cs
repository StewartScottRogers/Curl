using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The IMAP side of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one
/// connection: the <c>* OK</c> curl banner on connect (or <c>&lt;servercmd&gt;</c>'s
/// <c>REPLY welcome</c>), a <c>REPLY</c> line's text, untagged, for a command it names, else
/// ftpserver.pl's tagged answer to <c>CAPABILITY</c>, <c>LOGIN</c>, <c>SELECT</c>, <c>EXAMINE</c>,
/// <c>FETCH</c>, <c>UID</c>, <c>LIST</c>, <c>LSUB</c>, <c>STATUS</c>, <c>SEARCH</c>, <c>STORE</c>,
/// <c>COPY</c>, <c>CREATE</c>, <c>DELETE</c>, <c>RENAME</c>, <c>NOOP</c>, <c>CHECK</c>,
/// <c>CLOSE</c>, <c>EXPUNGE</c>, <c>IDLE</c>, <c>LOGOUT</c> and <c>APPEND</c>, and
/// <c>&lt;tag&gt; BAD &lt;command&gt; is not dealt with!</c> for any other, <c>AUTHENTICATE</c>
/// included: ftpserver.pl answers an IMAP authentication exchange only through <c>REPLY</c> lines.
/// A line is a tag, a space and a command, optionally followed by a space and arguments; a lone
/// <c>*</c> or a line of base64 characters is a command of its own under the last tag (an
/// authentication response); any other line is answered <c>&lt;line&gt; BAD Command</c> and the
/// connection closes. After <c>APPEND</c>'s <c>+</c> continuation, lines carry its <c>{n}</c>
/// literal until n bytes and then a CRLF have arrived.
/// </summary>
internal sealed class ImapResponder : ILineProtocolResponder
{
    private const string NoState = " BAD Command received in Invalid state\r\n";

    private const string BadArgument = " BAD Command Argument\r\n";

    private static readonly byte[] Banner = Encoding.Latin1.GetBytes(
        "        _   _ ____  _\r\n" +
        "    ___| | | |  _ \\| |\r\n" +
        "   / __| | | | |_) | |\r\n" +
        "  | (__| |_| |  _ {| |___\r\n" +
        "   \\___|\\___/|_| \\_\\_____|\r\n" +
        "* OK curl IMAP server ready to serve\r\n");

    private static readonly Dictionary<string, Func<ImapResponder, string, string>> Commands = new()
    {
        ["APPEND"] = (responder, arguments) => responder.Append(arguments),
        ["CAPABILITY"] = (responder, _) => responder.Capability(),
        ["CHECK"] = (responder, _) => responder.WhenSelected(() => responder.tag + " OK CHECK completed\r\n"),
        ["CLOSE"] = (responder, _) => responder.WhenSelected(responder.Close),
        ["COPY"] = (responder, arguments) => responder.TaggedUnlessEmpty(SplitParameters(arguments, 2), " OK COPY completed\r\n"),
        ["CREATE"] = (responder, arguments) => responder.TaggedUnlessEmpty([Unquote(arguments)], " OK CREATE completed\r\n"),
        ["DELETE"] = (responder, arguments) => responder.TaggedUnlessEmpty([Unquote(arguments)], " OK DELETE completed\r\n"),
        ["EXAMINE"] = (responder, arguments) => responder.ReplyDataFor(Unquote(arguments), " OK [READ-ONLY] EXAMINE completed\r\n"),
        ["EXPUNGE"] = (responder, _) => responder.WhenSelected(responder.Expunge),
        ["FETCH"] = (responder, arguments) => responder.WhenSelected(() => responder.Fetch(arguments)),
        ["IDLE"] = (_, _) => "+ entering idle mode\r\n",
        ["LIST"] = (responder, arguments) => responder.List(SplitParameters(arguments, 2)[0]),
        ["LOGIN"] = (responder, arguments) => responder.TaggedUnlessEmpty([SplitParameters(arguments, 2)[0]], " OK LOGIN completed\r\n"),
        ["LOGOUT"] = (responder, _) => "* BYE curl IMAP server signing off\r\n" + responder.tag + " OK LOGOUT completed\r\n",
        ["LSUB"] = (responder, arguments) => responder.ReplyDataFor(SplitParameters(arguments, 2)[0], " OK LSUB Completed\r\n"),
        ["NOOP"] = (responder, arguments) => responder.Noop(arguments),
        ["RENAME"] = (responder, arguments) => responder.TaggedUnlessEmpty(SplitParameters(arguments, 2), " OK RENAME completed\r\n"),
        ["SEARCH"] = (responder, arguments) => responder.WhenSelected(() => responder.Search(Unquote(arguments))),
        ["SELECT"] = (responder, arguments) => responder.Select(Unquote(arguments)),
        ["STATUS"] = (responder, arguments) => responder.ReplyDataFor(SplitParameters(arguments, 2)[0], " OK STATUS completed\r\n"),
        ["STORE"] = (responder, arguments) => responder.WhenSelected(() => responder.Store(arguments)),
        ["UID"] = (responder, arguments) => responder.WhenSelected(() => responder.Uid(arguments)),
    };

    private readonly LineProtocolServerCommands serverCommands;

    private readonly IReadOnlyDictionary<string, byte[]> replyParts;

    private readonly List<string> receivedCommandLines = [];

    private readonly List<string> deleted = [];

    private readonly StringBuilder upload = new();

    private string tag = string.Empty;

    private string selected = string.Empty;

    private long literalBytesLeft = -1;

    /// <summary>Creates the responder for one connection of a case.</summary>
    /// <param name="serverCommands">The case's <c>&lt;servercmd&gt;</c> <c>REPLY</c>, <c>CAPA</c>, <c>AUTH</c> and <c>POSTFETCH</c> lines.</param>
    /// <param name="replyParts">The case's <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data2</c> and so on), which <c>FETCH</c>, <c>LIST</c> and the other mailbox commands send.</param>
    public ImapResponder(LineProtocolServerCommands serverCommands, IReadOnlyDictionary<string, byte[]> replyParts)
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
    /// ftpserver.pl writes them to the protocol log <c>&lt;verify&gt;&lt;protocol&gt;</c> compares;
    /// the lines carrying an <c>APPEND</c> literal are not among them.
    /// </summary>
    public IReadOnlyList<string> ReceivedCommandLines => receivedCommandLines;

    /// <summary>
    /// Gets the last <c>APPEND</c> literal as ftpserver.pl stores it for <c>&lt;verify&gt;&lt;upload&gt;</c>:
    /// its first n bytes, without the CRLF that ends the command.
    /// </summary>
    public ReadOnlyMemory<byte> UploadedMessage => Encoding.Latin1.GetBytes(upload.ToString());

    /// <inheritdoc/>
    public LineProtocolReply Answer(string commandLine)
    {
        if (literalBytesLeft >= 0)
        {
            return Reply(ReceiveLiteralLine(commandLine), closesConnection: false);
        }

        receivedCommandLines.Add(commandLine + "\r\n");
        if (!TrySplitCommand(commandLine, out string command, out string arguments))
        {
            return Reply(commandLine + " BAD Command\r\n", closesConnection: true);
        }

        if (serverCommands.TryFindReply(command, out byte[] customReply))
        {
            return new LineProtocolReply(customReply, false);
        }

        string text = Commands.TryGetValue(command.ToUpperInvariant(), out Func<ImapResponder, string, string>? answer)
            ? answer(this, arguments)
            : $"{tag} BAD {command} is not dealt with!\r\n";
        return Reply(text, closesConnection: false);
    }

    private static LineProtocolReply Reply(string text, bool closesConnection) =>
        new(Encoding.Latin1.GetBytes(text), closesConnection);

    /// <summary>ftpserver.pl's <c>fix_imap_params</c>: removes one pair of enclosing double quotes.</summary>
    private static string Unquote(string parameter) =>
        parameter.Length >= 2 && parameter[0] == '"' && parameter[^1] == '"' ? parameter[1..^1] : parameter;

    /// <summary>Perl's <c>split(/ /, $args, count)</c> padded to <paramref name="count"/> with empty values, each unquoted.</summary>
    private static string[] SplitParameters(string arguments, int count)
    {
        string[] parameters = new string[count];
        string[] parts = arguments.Length == 0 ? [] : arguments.Split(' ', count);
        for (int index = 0; index < count; index++)
        {
            parameters[index] = index < parts.Length ? Unquote(parts[index]) : string.Empty;
        }

        return parameters;
    }

    /// <summary>Matches <c>^[A-Z0-9+\/]*={0,2}$</c>, letters in either case.</summary>
    private static bool IsBase64Line(string line)
    {
        string body = line.TrimEnd('=');
        return line.Length - body.Length <= 2 &&
            body.All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '/');
    }

    /// <summary>Splits a line as ftpserver.pl's main loop does for IMAP, keeping the tag for the reply.</summary>
    private bool TrySplitCommand(string commandLine, out string command, out string arguments)
    {
        string[] fields = commandLine.Split(' ', 3);
        arguments = string.Empty;
        if (fields.Length >= 2 && fields[0].Length > 0 && fields[1].Length > 0)
        {
            tag = fields[0];
            command = fields[1];
            arguments = fields.Length == 3 ? fields[2] : string.Empty;
            return true;
        }

        command = commandLine;
        return commandLine == "*" || IsBase64Line(commandLine);
    }

    private string WhenSelected(Func<string> answer) => selected.Length == 0 ? tag + NoState : answer();

    private string TaggedUnlessEmpty(string[] parameters, string completed) =>
        tag + (parameters.Any(parameter => parameter.Length == 0) ? BadArgument : completed);

    private string ReplyData(string name) => LineProtocolReplyData.Select(replyParts, name);

    private string ReplyDataFor(string name, string completed) =>
        name.Length == 0 ? tag + BadArgument : ReplyData(name) + tag + completed;

    private string Capability()
    {
        if (serverCommands.Capabilities.Count == 0 && serverCommands.AuthenticationMechanisms.Count == 0)
        {
            return tag + " BAD Command\r\n";
        }

        string capabilities = string.Concat(serverCommands.Capabilities.Select(capability => " " + capability));
        string mechanisms = string.Concat(serverCommands.AuthenticationMechanisms.Select(mechanism => " AUTH=" + mechanism));
        return $"* CAPABILITY IMAP4{capabilities}{mechanisms} pingpong test server\r\n{tag} OK CAPABILITY completed\r\n";
    }

    private string Select(string mailbox)
    {
        if (mailbox.Length == 0)
        {
            return tag + BadArgument;
        }

        selected = mailbox;
        return "* 172 EXISTS\r\n" +
            "* 1 RECENT\r\n" +
            "* OK [UNSEEN 12] Message 12 is first unseen\r\n" +
            "* OK [UIDVALIDITY 3857529045] UIDs valid\r\n" +
            "* OK [UIDNEXT 4392] Predicted next UID\r\n" +
            "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n" +
            "* OK [PERMANENTFLAGS (\\Deleted \\Seen \\*)] Limited\r\n" +
            tag + " OK [READ-WRITE] SELECT completed\r\n";
    }

    /// <summary>Answers <c>FETCH</c>: the reply data as a literal whose size is empty, as Perl's undefined sum prints, when there is none.</summary>
    private string Fetch(string arguments)
    {
        string[] parameters = SplitParameters(arguments, 2);
        string data = selected == "verifiedserver" ? $"WE ROOLZ: {Environment.ProcessId}\r\n" : ReplyData(selected);
        string size = data.Length > 0 ? data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        return $"* {parameters[0]} FETCH ({parameters[1]} {{{size}}}\r\n{data}{serverCommands.PostFetch})\r\n{tag} OK FETCH completed\r\n";
    }

    private string List(string reference) => reference == "verifiedserver"
        ? $"* LIST () \"/\" \"WE ROOLZ: {Environment.ProcessId}\"\r\n{tag} OK LIST Completed\r\n"
        : ReplyDataFor(reference, " OK LIST Completed\r\n");

    private string Search(string what) => what.Length == 0 ? tag + BadArgument : ReplyData(selected) + tag + " OK SEARCH completed\r\n";

    private string Store(string arguments)
    {
        string[] parameters = arguments.Split(' ', 3);
        string uid = Unquote(parameters[0]);
        if (uid.Length == 0 || parameters.Length < 3 || parameters[1] != "+Flags" || parameters[2].Length == 0)
        {
            return tag + BadArgument;
        }

        if (parameters[2] == "\\Deleted")
        {
            deleted.Add(uid);
        }

        return $"* {uid} FETCH (FLAGS (\\Seen {parameters[2]}))\r\n{tag} OK STORE completed\r\n";
    }

    private string Close()
    {
        if (deleted.Count == 0)
        {
            return tag + BadArgument;
        }

        deleted.Clear();
        return tag + " OK CLOSE completed\r\n";
    }

    private string Expunge()
    {
        string report = deleted.Count == 0 ? "* 172 EXISTS\r\n" : string.Concat(deleted.Select(uid => $"* {uid} EXPUNGE\r\n"));
        deleted.Clear();
        return report + tag + " OK EXPUNGE completed\r\n";
    }

    /// <summary>
    /// Answers <c>UID</c>, which ftpserver.pl does not split: <c>UID FETCH</c> runs <c>FETCH</c> on
    /// the whole argument (so <c>FETCH</c> is its message number), and only a bare <c>COPY</c>,
    /// <c>STORE</c> or <c>SEARCH</c> is answered with the reply data.
    /// </summary>
    private string Uid(string arguments)
    {
        string command = Unquote(arguments);
        if (command.StartsWith("FETCH", StringComparison.Ordinal))
        {
            return Fetch(arguments);
        }

        return command is "COPY" or "STORE" or "SEARCH"
            ? $"{ReplyData(selected)}{tag} OK {command} completed\r\n"
            : tag + BadArgument;
    }

    /// <summary>Answers <c>NOOP</c>; an argument Perl counts true (not empty and not <c>"0"</c>) is refused.</summary>
    private string Noop(string arguments) => arguments.Length > 0 && arguments != "0"
        ? tag + BadArgument
        : "* 22 EXPUNGE\r\n* 23 EXISTS\r\n* 3 RECENT\r\n* 14 FETCH (FLAGS (\\Seen \\Deleted))\r\n" + tag + " OK NOOP completed\r\n";

    /// <summary>
    /// Answers <c>APPEND &lt;mailbox&gt; ... {n}</c> with the <c>+</c> continuation and starts
    /// receiving its n-byte literal; anything else is refused.
    /// </summary>
    private string Append(string arguments)
    {
        int space = arguments.IndexOf(' ', StringComparison.Ordinal);
        if (space <= 0 || Unquote(arguments[..space]).Length == 0 || !TryReadLiteralSize(arguments, space, out long size))
        {
            literalBytesLeft = -1;
            return tag + BadArgument;
        }

        literalBytesLeft = size;
        upload.Clear();
        return "+ Ready for literal data\r\n";
    }

    /// <summary>Reads the <c>{n}</c> that ends <c>APPEND</c>'s arguments after the mailbox ending at <paramref name="space"/>, and only when no other <c>{</c> sits between them.</summary>
    private static bool TryReadLiteralSize(string arguments, int space, out long size)
    {
        int open = arguments.LastIndexOf('{');
        if (open <= space || !arguments.EndsWith('}'))
        {
            size = 0;
            return false;
        }

        return long.TryParse(arguments.AsSpan(open + 1, arguments.Length - open - 2), System.Globalization.NumberStyles.None, null, out size) &&
            !arguments.AsSpan(space + 1, open - space - 1).Contains('{');
    }

    /// <summary>
    /// Takes the literal's bytes from one line and its CRLF; once n bytes have arrived, a line whose
    /// remainder is exactly its CRLF completes the <c>APPEND</c>, as ftpserver.pl reads it.
    /// </summary>
    private string ReceiveLiteralLine(string line)
    {
        string received = line + "\r\n";
        int taken = (int)Math.Min(literalBytesLeft, received.Length);
        upload.Append(received, 0, taken);
        literalBytesLeft -= taken;
        if (literalBytesLeft > 0 || received.Length - taken != 2)
        {
            return string.Empty;
        }

        literalBytesLeft = -1;
        return tag + " OK APPEND completed\r\n";
    }
}
