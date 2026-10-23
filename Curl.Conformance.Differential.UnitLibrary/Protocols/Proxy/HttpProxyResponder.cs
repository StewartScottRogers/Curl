using System.Globalization;
using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.Protocols.Proxy;

/// <summary>
/// Answers an HTTP proxy connection the way proxy_server.py's <c>P</c> handler does for requests that are
/// not SOCKS (port 18300): every request is logged as its raw head; with <paramref name="requiresAuthentication"/>
/// (the script's <c>needauth</c> argument) a request without <c>Proxy-Authorization</c> gets a 407 and the
/// connection waits for the next request; CONNECT answers <c>200 Connection established</c> and tunnels to the
/// target; any other request gets the fixed <c>proxy reply</c> with <c>X-Via: proxy</c>. A request line that
/// is not three words, or a CONNECT target without a numeric port, ends the connection as the crashed handler did.
/// </summary>
public sealed class HttpProxyResponder(ProtocolWireLog log, IProxyTunnelConnector connector, bool requiresAuthentication)
{
    private static readonly byte[] Challenge = "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"p\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray();
    private static readonly byte[] Established = "HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\n"u8.ToArray();
    private static readonly byte[] Forwarded = "HTTP/1.1 200 OK\r\nContent-Length: 11\r\nX-Via: proxy\r\n\r\nproxy reply"u8.ToArray();

    /// <summary>Serves one connection until a forwarded request, a tunnel, a crash-equivalent request, or the client hangs up.</summary>
    public Task RespondAsync(Stream connection, CancellationToken cancellationToken) =>
        RespondAsync(connection, new DifferentialHttpLineReader(connection), [], cancellationToken);

    /// <summary>
    /// Serves a connection whose first bytes, <paramref name="consumed"/>, the caller has already read from
    /// <paramref name="reader"/> (the byte <see cref="ProxyServerResponder"/> peeks to tell SOCKS from HTTP).
    /// </summary>
    public async Task RespondAsync(Stream connection, DifferentialHttpLineReader reader, byte[] consumed, CancellationToken cancellationToken)
    {
        var writer = new ProtocolLineWriter(connection);
        while (true)
        {
            var (requestLine, hasAuthorization) = await ReadRequestAsync(reader, consumed, cancellationToken);
            consumed = [];
            var words = requestLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 3)
            {
                return;
            }

            if (requiresAuthentication && !hasAuthorization)
            {
                await writer.SendAsync(Challenge, cancellationToken);
                continue;
            }

            if (words[0] == "CONNECT")
            {
                await TunnelAsync(connection, writer, words[1], cancellationToken);
            }
            else
            {
                await writer.SendAsync(Forwarded, cancellationToken);
            }

            return;
        }
    }

    private async Task<(string RequestLine, bool HasAuthorization)> ReadRequestAsync(DifferentialHttpLineReader reader, byte[] consumed, CancellationToken cancellationToken)
    {
        var head = new StringBuilder();
        var requestLine = Encoding.Latin1.GetString(await ReadRequestLineAsync(reader, consumed, cancellationToken));
        head.Append(requestLine);
        var hasAuthorization = false;
        while (true)
        {
            var header = Encoding.Latin1.GetString(await reader.ReadLineAsync(cancellationToken));
            head.Append(header);
            if (header is "\r\n" or "")
            {
                break;
            }

            hasAuthorization |= header.Split(':')[0].Trim().Equals("proxy-authorization", StringComparison.OrdinalIgnoreCase);
        }

        log.Add(head.ToString());
        return (requestLine, hasAuthorization);
    }

    private static async Task<byte[]> ReadRequestLineAsync(DifferentialHttpLineReader reader, byte[] consumed, CancellationToken cancellationToken) =>
        consumed is [.., (byte)'\n'] ? consumed : [.. consumed, .. await reader.ReadLineAsync(cancellationToken)];

    private async Task TunnelAsync(Stream connection, ProtocolLineWriter writer, string target, CancellationToken cancellationToken)
    {
        var colon = target.LastIndexOf(':');
        var host = colon < 0 ? string.Empty : target[..colon];
        if (!int.TryParse(target[(colon + 1)..], NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var port))
        {
            return;
        }

        await writer.SendAsync(Established, cancellationToken);
        await ProxyTunnel.RelayAsync(connection, connector, host, port, cancellationToken);
    }
}
