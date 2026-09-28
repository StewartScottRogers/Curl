using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// The command a POP3 transfer sends after logging in, and whether its answer carries a body
/// to write, chosen as curl 8.21.0's <c>pop3_perform_command</c> chooses them (BL-549, BL-550).
/// </summary>
/// <param name="Line">The command line, without its CRLF.</param>
/// <param name="WritesBody">
/// Whether the answer is read as a multi-line body and written to the output; when
/// <see langword="false" />, only its status line is read and nothing is written.
/// </param>
/// <remarks>
/// Measured with <c>Record-CurlExchange.ps1 -Pop3</c> (BL-550 Notes):
/// <list type="bullet">
/// <item>The command is <c>-X</c>'s, percent-decoded like the message id (<c>TOP%201 0</c> is
/// <c>TOP 1 0</c>; a control character is exit 3); without one, <c>LIST</c> when the URL names
/// no message or <c>-l</c> is given, else <c>RETR</c>. A message id is appended after a
/// space, even to <c>-X</c>'s command (<c>-X "DELE " …/1</c> sends <c>DELE  1</c>).</item>
/// <item>Whether the answer has a body depends on the command alone, without the id:
/// <c>APOP</c>, <c>AUTH</c>, <c>DELE</c>, <c>NOOP</c>, <c>PASS</c>, <c>QUIT</c>, <c>RSET</c>,
/// <c>STAT</c>, <c>STLS</c>, <c>USER</c> and <c>UTF8</c> never have one; <c>CAPA</c>,
/// <c>MSG</c>, <c>RETR</c>, <c>TOP</c> and <c>XTND</c> always do; <c>LIST</c> and <c>UIDL</c> do
/// alone and not with arguments (<c>-X "UIDL 1"</c> is one line, <c>-X UIDL …/1</c> waits
/// for a body). Names match case-insensitively and must end the command or be followed by a
/// space; any other command (<c>DELEX</c>, <c>FOO</c>) is taken to have a body.</item>
/// <item><c>-l</c> on a URL naming a message writes no body whatever the command
/// (<c>-l -X RETR …/1</c> writes nothing).</item>
/// <item><c>-I</c> changes nothing: <c>RETR</c> still writes the message.</item>
/// </list>
/// </remarks>
internal sealed record Pop3Command(string Line, bool WritesBody)
{
    private const string ListCommand = "LIST";

    private const string RetrieveCommand = "RETR";

    /// <summary>
    /// Each command curl 8.21.0 knows: whether its answer has a body when it is sent alone,
    /// and when it is followed by arguments.
    /// </summary>
    private static readonly (string Name, bool Alone, bool WithArguments)[] KnownCommands =
    [
        ("APOP", false, false),
        ("AUTH", false, false),
        ("CAPA", true, true),
        ("DELE", false, false),
        ("LIST", true, false),
        ("MSG", true, true),
        ("NOOP", false, false),
        ("PASS", false, false),
        ("QUIT", false, false),
        ("RETR", true, true),
        ("RSET", false, false),
        ("STAT", false, false),
        ("STLS", false, false),
        ("TOP", true, true),
        ("UIDL", true, false),
        ("USER", false, false),
        ("UTF8", false, false),
        ("XTND", true, true),
    ];

    /// <summary>
    /// Chooses the command for <paramref name="context" />'s URL, <c>-X</c> and <c>-l</c>.
    /// </summary>
    /// <param name="context">The transfer whose URL, mail options and list-only flag decide.</param>
    /// <returns>
    /// The command, or <see langword="null" /> when the message id or the <c>-X</c> command
    /// decodes to a control character, which curl refuses with exit 3.
    /// </returns>
    public static Pop3Command? Choose(ITransferContext context)
    {
        if (Pop3MessageId.Read(context.Url) is not { } messageId
            || Pop3MessageId.Decode(context.Mail?.CustomCommand ?? string.Empty) is not { } custom)
        {
            return null;
        }

        return messageId.Length == 0
            ? ForMaildrop(custom)
            : ForMessage(messageId, custom, context.ListOnly);
    }

    /// <summary>
    /// Chooses the command for a URL naming no message: <c>-X</c>'s, else <c>LIST</c>.
    /// </summary>
    private static Pop3Command ForMaildrop(string custom)
    {
        string command = custom.Length > 0 ? custom : ListCommand;
        return new Pop3Command(command, HasBody(command));
    }

    /// <summary>
    /// Chooses the command for a URL naming <paramref name="messageId" />: <c>-X</c>'s, else
    /// <c>LIST</c> under <c>-l</c> and <c>RETR</c> without it, with the id appended.
    /// </summary>
    private static Pop3Command ForMessage(string messageId, string custom, bool listOnly)
    {
        string command = custom.Length > 0 ? custom : listOnly ? ListCommand : RetrieveCommand;
        return new Pop3Command(command + " " + messageId, !listOnly && HasBody(command));
    }

    /// <summary>
    /// Whether the answer to <paramref name="command" /> has a body, by the table curl's
    /// <c>pop3_is_multiline</c> reads.
    /// </summary>
    private static bool HasBody(string command)
    {
        foreach ((string name, bool alone, bool withArguments) in KnownCommands)
        {
            if (!command.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (command.Length == name.Length)
            {
                return alone;
            }

            if (command[name.Length] == ' ')
            {
                return withArguments;
            }
        }

        return true;
    }
}
