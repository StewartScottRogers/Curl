using System.Text;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Mail;

/// <summary>
/// Answers an SMTP connection the way proto_servers.py's <c>Smtp</c> class does (port 18210): the same replies,
/// the AUTH PLAIN and LOGIN continuations, and the same wire log lines (every command line and continuation
/// answer, plus <c>[data b'...']</c> after a DATA body). A client that hangs up in the middle of an AUTH
/// continuation ends the connection.
/// </summary>
public sealed class SmtpResponder(ProtocolWireLog log)
{
    private static readonly Dictionary<string, string> FixedReplies = new(StringComparer.Ordinal)
    {
        ["EHLO"] = "250-mock\r\n250-SIZE 100000\r\n250-8BITMIME\r\n250 AUTH PLAIN LOGIN\r\n",
        ["HELO"] = "250 mock\r\n",
        ["MAIL"] = "250 ok\r\n",
        ["VRFY"] = "250 user ok <u@x>\r\n",
        ["EXPN"] = "250-a@x\r\n250 b@x\r\n",
        ["RSET"] = "250 ok\r\n",
        ["NOOP"] = "250 ok\r\n",
    };

    /// <summary>Serves one connection until QUIT or the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        await new Session(connection, log, cancellationToken).RunAsync();
    }

    private sealed class Session(Stream connection, ProtocolWireLog log, CancellationToken cancellationToken)
    {
        private readonly ProtocolLineReader _reader = new(connection);
        private readonly ProtocolLineWriter _writer = new(connection);
        private readonly MemoryStream _body = new();
        private bool _inData;

        public async Task RunAsync()
        {
            await SendAsync("220 mock smtp\r\n");
            while (await _reader.ReadLineAsync(cancellationToken) is { } line)
            {
                var keepGoing = _inData ? await HandleBodyLineAsync(line) : await HandleCommandAsync(line);
                if (!keepGoing)
                {
                    return;
                }
            }
        }

        private static string ReplyTo(string command, string line) => command switch
        {
            "RCPT" => line.Contains("bad@", StringComparison.Ordinal) ? "550 no such user\r\n" : "250 ok\r\n",
            "DATA" => "354 go ahead\r\n",
            _ => FixedReplies.GetValueOrDefault(command, "500 unknown\r\n"),
        };

        private Task SendAsync(string reply) => _writer.SendAsync(reply, cancellationToken);

        private async Task<bool> HandleBodyLineAsync(string line)
        {
            if (line != ".")
            {
                _body.Write(Encoding.Latin1.GetBytes(line));
                _body.WriteByte((byte)'\n');
                return true;
            }

            _inData = false;
            log.Add($"[data {PythonLiteral.FormatBytes(_body.ToArray())}]");
            _body.SetLength(0);
            await SendAsync("250 queued\r\n");
            return true;
        }

        private async Task<bool> HandleCommandAsync(string line)
        {
            log.Add(line);
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var command = (space < 0 ? line : line[..space]).ToUpperInvariant();
            if (command == "AUTH")
            {
                return await AuthenticateAsync(line);
            }

            if (command == "QUIT")
            {
                await SendAsync("221 bye\r\n");
                return false;
            }

            await SendAsync(ReplyTo(command, line));
            _inData = command == "DATA";
            return true;
        }

        private async Task<bool> AuthenticateAsync(string line)
        {
            var plain = line.Contains("PLAIN", StringComparison.OrdinalIgnoreCase);
            if (plain && line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 2)
            {
                await SendAsync("235 ok\r\n");
                return true;
            }

            var answered = plain
                ? await ContinueAsync("334 \r\n")
                : await ContinueAsync("334 VXNlcm5hbWU6\r\n") && await ContinueAsync("334 UGFzc3dvcmQ6\r\n");
            if (answered)
            {
                await SendAsync("235 ok\r\n");
            }

            return answered;
        }

        private async Task<bool> ContinueAsync(string prompt)
        {
            await SendAsync(prompt);
            var answer = await _reader.ReadLineAsync(cancellationToken);
            if (answer is not null)
            {
                log.Add(answer);
            }

            return answer is not null;
        }
    }
}
