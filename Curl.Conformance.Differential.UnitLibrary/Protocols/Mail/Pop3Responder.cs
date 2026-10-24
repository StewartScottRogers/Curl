using System.Globalization;
using System.Text;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Mail;

/// <summary>
/// Answers a POP3 connection the way proto_servers.py's <c>Pop3</c> class does (port 18220): the same replies
/// over <see cref="MailMessages"/> and a wire log of every command line. A message number the Python class
/// would crash on (not a number, past the end of a Python list, or missing) ends the connection, as the
/// crashed handler did.
/// </summary>
public sealed class Pop3Responder(ProtocolWireLog log)
{
    private static readonly Dictionary<string, string> FixedReplies = new(StringComparer.Ordinal)
    {
        ["CAPA"] = "+OK\r\nTOP\r\nUIDL\r\nUSER\r\nSASL PLAIN\r\n.\r\n",
        ["USER"] = "+OK\r\n",
        ["PASS"] = "+OK logged in\r\n",
        ["DELE"] = "+OK deleted\r\n",
        ["NOOP"] = "+OK\r\n",
        ["QUIT"] = "+OK bye\r\n",
    };

    private static readonly Dictionary<string, Func<string[], byte[]?>> ComputedReplies = new(StringComparer.Ordinal)
    {
        ["STAT"] = _ => Stat(),
        ["LIST"] = List,
        ["UIDL"] = _ => Uidl(),
        ["RETR"] = Retrieve,
        ["TOP"] = Top,
    };

    /// <summary>Serves one connection until QUIT, a crash-equivalent command, or the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new ProtocolLineReader(connection);
        var writer = new ProtocolLineWriter(connection);
        await writer.SendAsync("+OK mock pop3\r\n", cancellationToken);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            log.Add(line);
            var parts = line.Split(' ');
            var command = parts[0].ToUpperInvariant();
            var reply = ReplyTo(command, parts[1..]);
            if (reply is null)
            {
                return;
            }

            await writer.SendAsync(reply, cancellationToken);
            if (command == "QUIT")
            {
                return;
            }
        }
    }

    private static byte[]? ReplyTo(string command, string[] arguments) =>
        ComputedReplies.TryGetValue(command, out var computed)
            ? computed(arguments)
            : Text(FixedReplies.GetValueOrDefault(command, "-ERR unknown\r\n"));

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Stat() => Text(string.Create(CultureInfo.InvariantCulture, $"+OK {MailMessages.All.Count} {MailMessages.All.Sum(message => message.Length)}\r\n"));

    private static byte[] Uidl() => Text("+OK\r\n"
        + string.Concat(MailMessages.All.Select((_, index) => string.Create(CultureInfo.InvariantCulture, $"{index + 1} uid{index + 1}\r\n")))
        + ".\r\n");

    private static byte[]? List(string[] arguments) => arguments.Length > 0 ? ListOne(arguments[0]) : ListAll();

    private static byte[]? ListOne(string number) =>
        MailMessages.TryParseNumber(number, out var n) && MailMessages.TryGetMessage(n - 1, out var message)
            ? Text(string.Create(CultureInfo.InvariantCulture, $"+OK {number} {message.Length}\r\n"))
            : null;

    private static byte[] ListAll() => Text("+OK\r\n"
        + string.Concat(MailMessages.All.Select((message, index) => string.Create(CultureInfo.InvariantCulture, $"{index + 1} {message.Length}\r\n")))
        + ".\r\n");

    private static byte[]? Retrieve(string[] arguments)
    {
        if (arguments.Length == 0 || !MailMessages.TryParseNumber(arguments[0], out var number))
        {
            return null;
        }

        var index = number - 1;
        if (index >= MailMessages.All.Count)
        {
            return Text("-ERR no such message\r\n");
        }

        return MailMessages.TryGetMessage(index, out var message) ? [.. "+OK\r\n"u8, .. message, .. ".\r\n"u8] : null;
    }

    private static byte[]? Top(string[] arguments)
    {
        if (arguments.Length == 0 || !MailMessages.TryParseNumber(arguments[0], out var number) || !MailMessages.TryGetMessage(number - 1, out var message))
        {
            return null;
        }

        var headers = message[..message.AsSpan().IndexOf("\r\n\r\n"u8)];
        return [.. "+OK\r\n"u8, .. headers, .. "\r\n.\r\n"u8];
    }
}
