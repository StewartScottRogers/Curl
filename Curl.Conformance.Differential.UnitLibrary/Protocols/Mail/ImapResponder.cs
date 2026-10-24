using System.Globalization;
using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Mail;

/// <summary>
/// Answers an IMAP connection the way proto_servers.py's <c>Imap</c> class does (port 18230): the same tagged
/// replies, the AUTHENTICATE continuation, FETCH over <see cref="MailMessages"/>, and the same wire log lines
/// (every command line and continuation answer, plus <c>[append b'...']</c> after an APPEND literal). A FETCH or
/// APPEND the Python class would crash on (no message number, no literal size, a bad number) ends the connection.
/// </summary>
public sealed class ImapResponder(ProtocolWireLog log)
{
    private static readonly Dictionary<string, Func<string, string>> TaggedReplies = new(StringComparer.Ordinal)
    {
        ["CAPABILITY"] = tag => $"* CAPABILITY IMAP4rev1 AUTH=PLAIN UIDPLUS\r\n{tag} OK done\r\n",
        ["LOGIN"] = tag => $"{tag} OK logged in\r\n",
        ["LIST"] = tag => $"* LIST (\\HasNoChildren) \"/\" \"INBOX\"\r\n* LIST (\\HasNoChildren) \"/\" \"Sent\"\r\n{tag} OK done\r\n",
        ["SELECT"] = Selected,
        ["EXAMINE"] = Selected,
        ["UID"] = tag => $"{tag} OK uid done\r\n",
        ["SEARCH"] = tag => $"* SEARCH 1 2\r\n{tag} OK done\r\n",
        ["STORE"] = tag => $"{tag} OK stored\r\n",
        ["EXPUNGE"] = tag => $"{tag} OK expunged\r\n",
        ["STATUS"] = tag => $"* STATUS INBOX (MESSAGES 2)\r\n{tag} OK done\r\n",
        ["NOOP"] = tag => $"{tag} OK noop\r\n",
    };

    /// <summary>Serves one connection until LOGOUT, a crash-equivalent command, or the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        await new Session(connection, log, cancellationToken).RunAsync();
    }

    private static string Selected(string tag) => $"* 2 EXISTS\r\n* 0 RECENT\r\n* OK [UIDVALIDITY 7] ok\r\n{tag} OK [READ-WRITE] selected\r\n";

    private sealed class Session(Stream connection, ProtocolWireLog log, CancellationToken cancellationToken)
    {
        private readonly DifferentialHttpLineReader _reader = new(connection);
        private readonly ProtocolLineWriter _writer = new(connection);

        public async Task RunAsync()
        {
            await SendAsync("* OK mock imap\r\n");
            while (await ReadLineAsync() is { } line)
            {
                log.Add(line);
                var parts = line.Split(' ', 3);
                var command = parts.Length > 1 ? parts[1].ToUpperInvariant() : string.Empty;
                if (!await HandleAsync(command, parts[0], line, parts))
                {
                    return;
                }
            }
        }

        private Task SendAsync(string reply) => _writer.SendAsync(reply, cancellationToken);

        private async Task<string?> ReadLineAsync()
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            return line.Length == 0 ? null : Encoding.Latin1.GetString(line).TrimEnd('\r', '\n');
        }

        private async Task<bool> HandleAsync(string command, string tag, string line, string[] parts)
        {
            switch (command)
            {
                case "AUTHENTICATE":
                    return await AuthenticateAsync(tag);
                case "FETCH":
                    return await FetchAsync(tag, parts);
                case "APPEND":
                    return await AppendAsync(tag, line);
                case "LOGOUT":
                    await SendAsync($"* BYE\r\n{tag} OK bye\r\n");
                    return false;
                default:
                    await SendAsync(TaggedReplies.TryGetValue(command, out var reply) ? reply(tag) : $"{tag} BAD unknown\r\n");
                    return true;
            }
        }

        private async Task<bool> AuthenticateAsync(string tag)
        {
            await SendAsync("+ \r\n");
            var answer = await ReadLineAsync();
            if (answer is null)
            {
                return false;
            }

            log.Add(answer);
            await SendAsync($"{tag} OK auth\r\n");
            return true;
        }

        private async Task<bool> FetchAsync(string tag, string[] parts)
        {
            var numbers = parts.Length < 3 ? [] : parts[2].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (numbers.Length == 0 || !MailMessages.TryParseNumber(numbers[0], out var number) || !MailMessages.TryGetMessage(number - 1, out var message))
            {
                return false;
            }

            var head = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"* {number} FETCH (BODY[] {{{message.Length}}}\r\n"));
            await _writer.SendAsync([.. head, .. message, .. ")\r\n"u8, .. Encoding.UTF8.GetBytes($"{tag} OK done\r\n")], cancellationToken);
            return true;
        }

        private async Task<bool> AppendAsync(string tag, string line)
        {
            var brace = line.LastIndexOf('{');
            if (brace < 0 || !MailMessages.TryParseNumber(line[(brace + 1)..].TrimEnd('+', '}'), out var size))
            {
                return false;
            }

            await SendAsync("+ go\r\n");
            var literal = await _reader.ReadAsync(size, cancellationToken);
            await _reader.ReadLineAsync(cancellationToken);
            log.Add($"[append {PythonLiteral.FormatBytes(literal)}]");
            await SendAsync($"{tag} OK appended\r\n");
            return true;
        }
    }
}
