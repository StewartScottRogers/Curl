using System.Text;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Text;

/// <summary>
/// Answers a DICT connection the way proto2_servers.py's <c>Dict</c> class does (port 18400): the same banner,
/// the same replies to DEFINE, MATCH and QUIT (matched case-insensitively by prefix), <c>500 unknown</c> for
/// anything else, and each command line logged.
/// </summary>
public sealed class DictResponder(ProtocolWireLog log)
{
    /// <summary>Serves one connection until QUIT or the client hangs up.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new ProtocolLineReader(connection);
        var writer = new ProtocolLineWriter(connection);
        await writer.SendAsync("220 dict ready <auth.mime> <1.1@x>\r\n", cancellationToken);
        while (await reader.ReadLineAsync(cancellationToken) is { } raw)
        {
            var line = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(raw));
            log.Add(line);
            var upper = line.ToUpperInvariant();
            if (upper.StartsWith("QUIT", StringComparison.Ordinal))
            {
                await writer.SendAsync("221 bye\r\n", cancellationToken);
                return;
            }

            await writer.SendAsync(Reply(upper), cancellationToken);
        }
    }

    private static string Reply(string upperLine) => upperLine switch
    {
        _ when upperLine.StartsWith("DEFINE", StringComparison.Ordinal) =>
            "150 1 definitions retrieved\r\n151 \"word\" web1913 \"Webster\"\r\nthe definition\r\n.\r\n250 ok\r\n",
        _ when upperLine.StartsWith("MATCH", StringComparison.Ordinal) => "152 1 matches found\r\nweb1913 \"word\"\r\n.\r\n250 ok\r\n",
        _ => "500 unknown\r\n",
    };
}
