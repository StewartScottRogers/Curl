using System.Globalization;
using System.Text;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Ftp;

/// <summary>
/// Answers an FTP control connection the way proto_servers.py's <c>Ftp</c> class does (port 18200): the same
/// replies, the same <see cref="FtpFiles"/>, the same LIST/NLST listings, passive data connections, REST,
/// and the same wire log lines (every command line, plus <c>[uploaded N bytes: ...]</c> after STOR and APPE).
/// A command the Python class would crash on (a REST with no number, a transfer with no PASV or EPSV first)
/// ends the connection, as the crashed handler did.
/// </summary>
public sealed class FtpResponder(ProtocolWireLog log, IPassiveDataListenerOpener dataListeners)
{
    private const int UploadLogBytes = 40;

    private static readonly Dictionary<string, string> FixedReplies = new(StringComparer.Ordinal)
    {
        ["USER"] = "331 pass please\r\n",
        ["PWD"] = "257 \"/\" is cwd\r\n",
        ["SYST"] = "215 UNIX Type: L8\r\n",
        ["FEAT"] = "211-Features:\r\n SIZE\r\n MDTM\r\n REST STREAM\r\n EPSV\r\n211 End\r\n",
        ["OPTS"] = "200 ok\r\n",
        ["DELE"] = "250 ok\r\n",
        ["RMD"] = "250 ok\r\n",
        ["MKD"] = "250 ok\r\n",
        ["RNTO"] = "250 ok\r\n",
        ["RNFR"] = "350 ready\r\n",
        ["SITE"] = "200 ok\r\n",
        ["NOOP"] = "200 ok\r\n",
    };

    /// <summary>Serves one control connection until QUIT, a crash-equivalent command, or the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        using var session = new Session(connection, log, dataListeners, cancellationToken);
        await session.RunAsync();
    }

    private sealed class Session(Stream connection, ProtocolWireLog log, IPassiveDataListenerOpener dataListeners, CancellationToken cancellationToken) : IDisposable
    {
        private readonly ProtocolLineReader _reader = new(connection);
        private readonly ProtocolLineWriter _writer = new(connection);
        private string _directory = "/";
        private long _restart;
        private IPassiveDataListener? _passive;

        public void Dispose() => _passive?.Dispose();

        public async Task RunAsync()
        {
            await SendAsync("220 mock ftp\r\n");
            while (await _reader.ReadLineAsync(cancellationToken) is { } line)
            {
                log.Add(line);
                var space = line.IndexOf(' ', StringComparison.Ordinal);
                var command = (space < 0 ? line : line[..space]).ToUpperInvariant();
                var argument = space < 0 ? string.Empty : line[(space + 1)..];
                if (!await HandleAsync(command, argument))
                {
                    return;
                }
            }
        }

        private static byte[] Listing(bool detailed) => Encoding.ASCII.GetBytes(string.Concat(FtpFiles.ListedNames.Select(name => detailed
            ? string.Create(CultureInfo.InvariantCulture, $"-rw-r--r--   1 u g {FtpFiles.ListedSize(name)} Oct 21 07:28 {name}\r\n")
            : name + "\r\n")));

        private async Task<bool> HandleAsync(string command, string argument)
        {
            switch (command)
            {
                case "REST":
                    if (!long.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out _restart))
                    {
                        return false;
                    }

                    await SendAsync("350 restarting\r\n");
                    return true;
                case "PASV" or "EPSV":
                    await OpenPassiveAsync(command == "PASV");
                    return true;
                case "LIST" or "NLST":
                    return await SendDataAsync("150 here comes\r\n", Listing(command == "LIST"));
                case "RETR":
                    return await RetrieveAsync(argument);
                case "STOR" or "APPE":
                    return await StoreAsync();
                case "QUIT":
                    await SendAsync("221 bye\r\n");
                    return false;
                default:
                    await SendAsync(ReplyTo(command, argument));
                    return true;
            }
        }

        private async Task OpenPassiveAsync(bool classic)
        {
            _passive?.Dispose();
            _passive = dataListeners.Open();
            var port = _passive.Port;
            await SendAsync(classic
                ? string.Create(CultureInfo.InvariantCulture, $"227 Entering Passive Mode (127,0,0,1,{port >> 8},{port & 255})\r\n")
                : string.Create(CultureInfo.InvariantCulture, $"229 Entering Extended Passive Mode (|||{port}|)\r\n"));
        }

        private string ReplyTo(string command, string argument)
        {
            if (command != "CWD")
            {
                return PureReplyTo(command, argument);
            }

            _directory = argument;
            return "250 ok\r\n";
        }

        private static string PureReplyTo(string command, string argument)
        {
            switch (command)
            {
                case "SIZE":
                    return FtpFiles.TryGet(argument.StartsWith('/') ? argument : "/" + argument, out var sized)
                        ? string.Create(CultureInfo.InvariantCulture, $"213 {sized.Length}\r\n")
                        : "550 no such file\r\n";
                case "MDTM":
                    return FtpFiles.TryGet("/" + argument.TrimStart('/'), out _) ? "213 20151021072800\r\n" : "550 no\r\n";
                case "PASS":
                    return argument == "wrong" ? "530 login incorrect\r\n" : "230 logged in\r\n";
                case "TYPE":
                    return $"200 Type {argument}\r\n";
                default:
                    return FixedReplies.GetValueOrDefault(command, "502 not implemented\r\n");
            }
        }

        private async Task<bool> RetrieveAsync(string argument)
        {
            var path = argument.StartsWith('/') ? argument : _directory == "/" ? "/" + argument : _directory.TrimEnd('/') + "/" + argument;
            if (!FtpFiles.TryGet(path, out var content))
            {
                await SendAsync("550 no such file\r\n");
                return true;
            }

            var start = _restart < 0 ? Math.Max(0, content.Length + _restart) : Math.Min(_restart, content.Length);
            _restart = 0;
            return await SendDataAsync("150 opening\r\n", content[(int)start..]);
        }

        private async Task<bool> SendDataAsync(string preliminary, byte[] data)
        {
            await SendAsync(preliminary);
            if (_passive is null)
            {
                return false;
            }

            await using (var channel = await TakeDataConnectionAsync())
            {
                await channel.WriteAsync(data, cancellationToken);
            }

            await SendAsync("226 done\r\n");
            return true;
        }

        private async Task<bool> StoreAsync()
        {
            await SendAsync("150 ok send\r\n");
            if (_passive is null)
            {
                return false;
            }

            using var received = new MemoryStream();
            await using (var channel = await TakeDataConnectionAsync())
            {
                await channel.CopyToAsync(received, cancellationToken);
            }

            var bytes = received.ToArray();
            log.Add(string.Create(CultureInfo.InvariantCulture, $"[uploaded {bytes.Length} bytes: {PythonLiteral.FormatBytes(bytes.AsSpan(0, Math.Min(UploadLogBytes, bytes.Length)))}]"));
            await SendAsync("226 stored\r\n");
            return true;
        }

        private async Task<Stream> TakeDataConnectionAsync()
        {
            var listener = _passive!;
            _passive = null;
            using (listener)
            {
                return await listener.AcceptAsync(cancellationToken);
            }
        }

        private Task SendAsync(string text) => _writer.SendAsync(text, cancellationToken);
    }
}
