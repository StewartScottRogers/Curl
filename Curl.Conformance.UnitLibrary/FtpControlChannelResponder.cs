using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The FTP control channel of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one
/// connection: the <c>220</c> curl banner on connect (or <c>&lt;servercmd&gt;</c>'s
/// <c>REPLY welcome</c>), a <c>REPLY</c> line's text for a command it names, else ftpserver.pl's
/// display text for the command (<c>USER</c>, <c>PASS</c>, <c>TYPE</c>, <c>CWD</c>, <c>SYST</c>,
/// <c>QUIT</c> and the rest), <c>PWD</c>'s current directory as <c>CWD</c> moved it, and
/// <c>500 &lt;command&gt; is not dealt with!</c> for any other command. A line that is not three or
/// four letters, optionally followed by white space and an argument, is answered
/// <c>500 Unrecognized command</c> and the connection closes. Commands that need a data connection
/// or the case's data are carried out by the <see cref="FtpTransferCommands"/> it is given, after
/// their display text as ftpserver.pl does; without them, and for the active-mode and upload
/// commands (<c>PORT</c>, <c>EPRT</c>, <c>STOR</c>, <c>APPE</c>, BL-1907), they get a <c>REPLY</c>
/// line, their display text, or the not-dealt-with answer.
/// </summary>
internal sealed class FtpControlChannelResponder : ILineProtocolResponder
{
    private static readonly Dictionary<string, string> DisplayTexts = new(StringComparer.Ordinal)
    {
        ["USER"] = "331 We are happy you popped in!",
        ["PASS"] = "230 Welcome you silly person",
        ["PORT"] = "200 You said PORT - I say FINE",
        ["TYPE"] = "200 I modify TYPE as you wanted",
        ["LIST"] = "150 here comes a directory",
        ["NLST"] = "150 here comes a directory",
        ["CWD"] = "250 CWD command successful.",
        ["SYST"] = "215 UNIX Type: L8",
        ["QUIT"] = "221 bye bye baby",
        ["MKD"] = "257 Created your requested directory",
        ["REST"] = "350 Yeah yeah we set it there for you",
        ["DELE"] = "200 OK OK OK whatever you say",
        ["RNFR"] = "350 Received your order. Please provide more",
        ["RNTO"] = "250 Ok, thanks. File renaming completed.",
        ["NOOP"] = "200 Yes, I'm very good at doing nothing.",
        ["PBSZ"] = "500 PBSZ not implemented",
        ["PROT"] = "500 PROT not implemented",
    };

    private static readonly byte[] Banner = Encoding.Latin1.GetBytes(
        "220-        _   _ ____  _\r\n" +
        "220-    ___| | | |  _ \\| |\r\n" +
        "220-   / __| | | | |_) | |\r\n" +
        "220-  | (__| |_| |  _ {| |___\r\n" +
        "220    \\___|\\___/|_| \\_\\_____|\r\n");

    private readonly LineProtocolServerCommands serverCommands;

    private readonly FtpTransferCommands? transferCommands;

    private readonly List<string> receivedCommandLines = [];

    private string targetDirectory = "/";

    /// <summary>Creates the responder for one connection of a case.</summary>
    /// <param name="serverCommands">The case's <c>&lt;servercmd&gt;</c> <c>REPLY</c> lines.</param>
    public FtpControlChannelResponder(LineProtocolServerCommands serverCommands)
    {
        ArgumentNullException.ThrowIfNull(serverCommands);
        this.serverCommands = serverCommands;
        Greeting = serverCommands.TryFindReply("welcome", out byte[] welcome) ? welcome : Banner;
    }

    /// <summary>Creates the responder for one connection of a case, carrying out its passive-mode transfers.</summary>
    /// <param name="serverCommands">The case's <c>&lt;servercmd&gt;</c> <c>REPLY</c> lines.</param>
    /// <param name="transferCommands">The connection's <c>PASV</c>, <c>EPSV</c>, <c>RETR</c>, <c>LIST</c>, <c>NLST</c>, <c>SIZE</c>, <c>MDTM</c> and <c>REST</c>.</param>
    public FtpControlChannelResponder(LineProtocolServerCommands serverCommands, FtpTransferCommands transferCommands)
        : this(serverCommands)
    {
        this.transferCommands = transferCommands;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Greeting { get; }

    /// <summary>
    /// Gets every command line answered so far, each with its CRLF, in the order and form
    /// ftpserver.pl writes them to the protocol log <c>&lt;verify&gt;&lt;protocol&gt;</c> compares.
    /// </summary>
    public IReadOnlyList<string> ReceivedCommandLines => receivedCommandLines;

    /// <inheritdoc/>
    public LineProtocolReply Answer(string commandLine)
    {
        receivedCommandLines.Add(commandLine + "\r\n");
        if (!TrySplitCommand(commandLine, out string command, out string argument))
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

    /// <summary>Splits a line the way ftpserver.pl's <c>^([A-Z]{3,4})(\s(.*))?$</c> does, letters in either case.</summary>
    private static bool TrySplitCommand(string commandLine, out string command, out string argument)
    {
        int letters = commandLine.Take(5).TakeWhile(char.IsAsciiLetter).Count();
        bool endsAfterLetters = letters == commandLine.Length || char.IsWhiteSpace(commandLine[letters]);
        command = commandLine[..letters];
        argument = letters < commandLine.Length ? commandLine[(letters + 1)..] : string.Empty;
        return letters is 3 or 4 && endsAfterLetters;
    }

    private string AnswerByDefault(string command, string argument)
    {
        // Display text is looked up by the command as written, the handler by its upper case,
        // so a lower-case "cwd" moves the directory and gets no reply at all.
        string? displayText = DisplayTexts.TryGetValue(command, out string? text) ? text + "\r\n" : null;
        switch (command.ToUpperInvariant())
        {
            case "PWD":
                return $"257 \"{CurrentDirectory()}\" is current directory\r\n";
            case "CWD":
                SwitchDirectory(argument);
                return displayText ?? string.Empty;
            default:
                return AnswerTransferOrNotDealtWith(command, argument, displayText);
        }
    }

    private string AnswerTransferOrNotDealtWith(string command, string argument, string? displayText) =>
        transferCommands is not null && transferCommands.TryAnswer(command.ToUpperInvariant(), argument, out string answer)
            ? (displayText ?? string.Empty) + answer
            : displayText ?? $"500 {command} is not dealt with!\r\n";

    private string CurrentDirectory() => targetDirectory == "/" ? "/" : targetDirectory.TrimEnd('/');

    /// <summary>Moves the current directory the way ftpserver.pl's <c>switch_directory</c> does.</summary>
    private void SwitchDirectory(string target)
    {
        if (target == "/")
        {
            targetDirectory = "/";
            return;
        }

        // Perl's split drops trailing empty fields, so "a/" is one segment and "//" none.
        string trimmed = target.TrimEnd('/');
        if (trimmed.Length == 0 || target.StartsWith("test-", StringComparison.Ordinal))
        {
            return;
        }

        foreach (string segment in trimmed.Split('/'))
        {
            targetDirectory = DirectoryAfter(segment);
        }
    }

    private string DirectoryAfter(string segment) => segment switch
    {
        "" => "/",
        ".." => ParentDirectory(targetDirectory),
        _ => targetDirectory + segment + "/",
    };

    /// <summary>Strips the last segment as ftpserver.pl's <c>s/[[:alnum:]]+\/$//</c> does: only an alphanumeric one.</summary>
    private static string ParentDirectory(string directory)
    {
        if (directory == "/")
        {
            return directory;
        }

        int end = directory.Length - 1;
        int start = end;
        // The directory always starts with "/", which stops the walk.
        while (char.IsAsciiLetterOrDigit(directory[start - 1]))
        {
            start--;
        }

        return start < end ? directory[..start] : directory;
    }
}
