using System.Globalization;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens a tunnel through an HTTP proxy: writes the CONNECT request curl 8.21.0 writes and
/// reads the proxy's reply up to the end of its header block.
/// </summary>
/// <remarks>
/// The reply is read one byte at a time, so no byte after the header block is taken off the
/// connection: whatever follows belongs to the tunnel, such as the TLS handshake.
/// </remarks>
internal static class HttpProxyTunnel
{
    /// <summary>
    /// Builds the CONNECT request for <paramref name="host" /> and <paramref name="port" /> through <paramref name="proxy" />.
    /// </summary>
    /// <param name="host">The host the tunnel reaches: the URL's, or the one a <c>--connect-to</c> mapping gives (measured).</param>
    /// <param name="port">The port the tunnel reaches.</param>
    /// <param name="proxy">The proxy, whose kind picks HTTP/1.0 or HTTP/1.1.</param>
    /// <param name="options">The <c>User-Agent</c> and the <c>--proxy-header</c> values.</param>
    /// <param name="proxyAuthorization">
    /// The <c>Proxy-Authorization</c> value, such as <c>Basic dTpw</c>, or <see langword="null" /> to send none.
    /// </param>
    /// <returns>
    /// The request bytes: curl's own headers in its order, each left out when a
    /// <c>--proxy-header</c> value names it, then the proxy header lines, ending with the
    /// empty line (BL-347 Notes).
    /// </returns>
    public static byte[] BuildConnectRequest(string host, int port, ProxyEndpoint proxy, HttpProxyTunnelOptions options, string? proxyAuthorization)
    {
        var authority = FormatAuthority(host, port);
        var version = proxy.Kind == ProxyKind.Http10 ? "HTTP/1.0" : "HTTP/1.1";
        HttpProxyTunnelHeader[] proxyHeaders = [.. options.ProxyHeaders.Select(value => HttpProxyTunnelHeader.Parse(RequestText(value, options)))];
        var request = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"CONNECT {authority} {version}\r\n");

        AppendUnlessNamed(request, proxyHeaders, "Host", authority);
        AppendUnlessNamed(request, proxyHeaders, "Proxy-Authorization", proxyAuthorization);
        AppendUnlessNamed(request, proxyHeaders, "User-Agent", options.UserAgent is { } userAgent ? RequestText(userAgent, options) : null);
        AppendUnlessNamed(request, proxyHeaders, "Proxy-Connection", "Keep-Alive");
        foreach (var sentLine in proxyHeaders.Select(header => header.SentLine).OfType<string>())
        {
            request.Append(sentLine).Append("\r\n");
        }

        request.Append("\r\n");
        return Encoding.Latin1.GetBytes(request.ToString());
    }

    // Appends "name: value" unless value is null or a --proxy-header value names the header.
    private static void AppendUnlessNamed(StringBuilder request, HttpProxyTunnelHeader[] proxyHeaders, string name, string? value)
    {
        if (value is not null && !proxyHeaders.Any(header => header.Names(name)))
        {
            request.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }
    }

    // Command-line text as its bytes in the options' CommandLineTextEncoding, one character per
    // byte, so the request's closing Latin-1 step puts those bytes on the wire (ADR-0067).
    private static string RequestText(string text, HttpProxyTunnelOptions options) =>
        Encoding.Latin1.GetString(options.CommandLineTextEncoding.GetBytes(text));

    /// <summary>
    /// The most bytes one line of the reply may hold, its CR and LF included, before curl
    /// 8.21.0 gives up with <c>CONNECT response too large</c> (measured: 16383 is read).
    /// </summary>
    public const int MaximumLineBytes = 16383;

    /// <summary>
    /// The most bytes the reply's header block may hold at the end of a line before curl
    /// 8.21.0 gives up with <c>Too large response headers</c> (measured).
    /// </summary>
    public const int MaximumHeaderBytes = 307200;

    /// <summary>
    /// Reads the proxy's reply to CONNECT up to and including the empty line that ends its
    /// header block, and returns the status code on its first line and the fields curl acts on.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The status code, <c>0</c> when the first line is not an HTTP status line, as curl
    /// reports it, with the <c>Proxy-Authenticate</c> values, the body length and whether the
    /// connection can carry another CONNECT; or the exit 56 message when the proxy closed the connection before the
    /// header block ended or sent more than curl reads.
    /// </returns>
    public static async ValueTask<HttpProxyTunnelReply> ReadReplyAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var header = new List<byte>();
        var oneByte = new byte[1];
        var lineStart = 0;
        while (await connection.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false) == 1)
        {
            header.Add(oneByte[0]);
            if (ReplyAfterLatestByte(header, ref lineStart) is { } reply)
            {
                return reply;
            }
        }

        return HttpProxyTunnelReply.Failed("Proxy CONNECT aborted");
    }

    // The reply once the byte just added ends it, or null while more is to be read; a line
    // holding nothing but an optional CR ends the block, as curl accepts bare LF line endings.
    private static HttpProxyTunnelReply? ReplyAfterLatestByte(List<byte> header, ref int lineStart)
    {
        var lineBytes = header.Count - lineStart;
        if (lineBytes > MaximumLineBytes)
        {
            return HttpProxyTunnelReply.Failed("CONNECT response too large");
        }

        if (header[^1] != '\n')
        {
            return null;
        }

        if (header.Count > MaximumHeaderBytes)
        {
            return HttpProxyTunnelReply.Failed(
                string.Create(CultureInfo.InvariantCulture, $"Too large response headers: {header.Count} > {MaximumHeaderBytes}"));
        }

        if (IsEmptyLine(header, lineStart))
        {
            return ParseReply(header);
        }

        lineStart = header.Count;
        return null;
    }

    // The line from lineStart to the LF just read holds nothing but that LF, or a CR and it.
    private static bool IsEmptyLine(List<byte> header, int lineStart) =>
        header.Count - lineStart == 1 || (header.Count - lineStart == 2 && header[lineStart] == '\r');

    // The status code and the fields curl 8.21.0 acts on after a CONNECT: Proxy-Authenticate,
    // Content-Length, Connection and Proxy-Connection close, and a chunked Transfer-Encoding.
    private static HttpProxyTunnelReply ParseReply(List<byte> header)
    {
        List<string> proxyAuthenticate = [];
        long contentLength = 0;
        var reusable = true;
        foreach (var (name, value) in HeaderFields(header))
        {
            switch (name.ToUpperInvariant())
            {
                case "PROXY-AUTHENTICATE":
                    proxyAuthenticate.Add(value);
                    break;
                case "CONTENT-LENGTH":
                    contentLength = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : contentLength;
                    break;
                case "CONNECTION" or "PROXY-CONNECTION":
                    reusable &= !HasToken(value, "close");
                    break;
                case "TRANSFER-ENCODING":
                    reusable &= !HasToken(value, "chunked");
                    break;
            }
        }

        return new HttpProxyTunnelReply(ParseStatusCode(header), null)
        {
            ProxyAuthenticate = proxyAuthenticate,
            ContentLength = contentLength,
            LeavesConnectionReusable = reusable,
        };
    }

    // Every "name: value" line after the status line, the value without the blanks around it;
    // a line with no colon, or nothing before it, is not a field.
    private static IEnumerable<(string Name, string Value)> HeaderFields(List<byte> header) =>
        Encoding.Latin1.GetString([.. header])
            .Split('\n')
            .Skip(1)
            .Select(line => (Line: line, Colon: line.IndexOf(':', StringComparison.Ordinal)))
            .Where(field => field.Colon > 0)
            .Select(field => (field.Line[..field.Colon], field.Line[(field.Colon + 1)..].Trim(' ', '\t', '\r')));

    // The comma-separated value names the token, compared without regard to case.
    private static bool HasToken(string value, string token) =>
        value.Split(',').Any(item => item.Trim(' ', '\t').Equals(token, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads and discards the body of a reply that does not open the tunnel, so another
    /// CONNECT can follow on the same connection.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="length">The body's length, <see cref="HttpProxyTunnelReply.ContentLength" />.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// <see langword="true" /> once every byte is read; <see langword="false" /> when the proxy
    /// closed the connection first or reading it failed, so it cannot carry another CONNECT.
    /// </returns>
    public static async ValueTask<bool> DiscardBodyAsync(IConnection connection, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            for (var left = length; left > 0;)
            {
                var read = await connection.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                left -= read;
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    internal static string FormatAuthority(string host, int port) =>
        host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[')
            ? $"[{host}]:{port}"
            : $"{host}:{port}";

    private static int ParseStatusCode(List<byte> header)
    {
        // "HTTP/1.1 407 Proxy Authentication Required": the version, a space, exactly three
        // digits, then a space or the end of the line; anything else is status 0 (measured).
        var firstLine = Encoding.Latin1.GetString([.. header[..header.IndexOf((byte)'\n')]]).TrimEnd('\r');
        var parts = firstLine.Split(' ', 3);
        return parts.Length >= 2
            && parts[0].StartsWith("HTTP/", StringComparison.Ordinal)
            && parts[1].Length == 3
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode)
                ? statusCode
                : 0;
    }
}
