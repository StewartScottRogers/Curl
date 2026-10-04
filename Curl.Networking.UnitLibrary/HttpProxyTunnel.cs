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
        AppendCommonHeaders(request, proxyHeaders, options, proxyAuthorization);
        return EndRequest(request, proxyHeaders);
    }

    /// <summary>
    /// Builds the CONNECT-UDP request (RFC 9298) for <paramref name="host" /> and
    /// <paramref name="port" /> through <paramref name="proxy" />, as curl 8.22.0 sends it over
    /// HTTP/1.x (measured, BL-942): a <c>GET</c> of the proxy's
    /// <c>/.well-known/masque/udp/&lt;host&gt;/&lt;port&gt;/</c> in absolute form, <c>Host</c>
    /// naming the proxy, then the CONNECT's headers and <c>Connection: Upgrade</c>,
    /// <c>Upgrade: connect-udp</c> and <c>Capsule-Protocol: ?1</c> before the
    /// <c>--proxy-header</c> lines.
    /// </summary>
    /// <param name="host">The host the tunnel reaches; an IPv6 literal goes without brackets, its colons percent-encoded.</param>
    /// <param name="port">The port the tunnel reaches.</param>
    /// <param name="proxy">The proxy, whose kind picks <c>http</c> or <c>https</c> and HTTP/1.0 or HTTP/1.1.</param>
    /// <param name="options">The <c>User-Agent</c> and the <c>--proxy-header</c> values.</param>
    /// <param name="proxyAuthorization">The <c>Proxy-Authorization</c> value, or <see langword="null" /> to send none.</param>
    /// <returns>The request bytes, ending with the empty line.</returns>
    public static byte[] BuildConnectUdpRequest(string host, int port, ProxyEndpoint proxy, HttpProxyTunnelOptions options, string? proxyAuthorization)
    {
        var proxyAuthority = FormatAuthority(proxy.Host, proxy.Port);
        var scheme = proxy.Kind == ProxyKind.Https ? "https" : "http";
        var version = proxy.Kind == ProxyKind.Http10 ? "HTTP/1.0" : "HTTP/1.1";
        HttpProxyTunnelHeader[] proxyHeaders = [.. options.ProxyHeaders.Select(value => HttpProxyTunnelHeader.Parse(RequestText(value, options)))];
        var target = string.Create(CultureInfo.InvariantCulture, $"/.well-known/masque/udp/{Uri.EscapeDataString(host.Trim('[', ']'))}/{port}/");
        var request = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"GET {scheme}://{proxyAuthority}{target} {version}\r\n");

        AppendUnlessNamed(request, proxyHeaders, "Host", proxyAuthority);
        AppendCommonHeaders(request, proxyHeaders, options, proxyAuthorization);
        AppendUnlessNamed(request, proxyHeaders, "Connection", "Upgrade");
        AppendUnlessNamed(request, proxyHeaders, "Upgrade", "connect-udp");
        AppendUnlessNamed(request, proxyHeaders, "Capsule-Protocol", "?1");
        return EndRequest(request, proxyHeaders);
    }

    // The headers both tunnel requests send after Host: Proxy-Authorization, User-Agent and
    // Proxy-Connection.
    private static void AppendCommonHeaders(StringBuilder request, HttpProxyTunnelHeader[] proxyHeaders, HttpProxyTunnelOptions options, string? proxyAuthorization)
    {
        AppendUnlessNamed(request, proxyHeaders, "Proxy-Authorization", proxyAuthorization);
        AppendUnlessNamed(request, proxyHeaders, "User-Agent", options.UserAgent is { } userAgent ? RequestText(userAgent, options) : null);
        AppendUnlessNamed(request, proxyHeaders, "Proxy-Connection", "Keep-Alive");
    }

    // The --proxy-header lines, the empty line, and the request as Latin-1 bytes.
    private static byte[] EndRequest(StringBuilder request, HttpProxyTunnelHeader[] proxyHeaders)
    {
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
    public static ValueTask<HttpProxyTunnelReply> ReadReplyAsync(IConnection connection, CancellationToken cancellationToken) =>
        ReadReplyAsync(connection, forConnectUdp: false, cancellationToken);

    /// <summary>
    /// Reads the proxy's reply to CONNECT, or to CONNECT-UDP when <paramref name="forConnectUdp" />
    /// is set, up to and including the empty line that ends its header block.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="forConnectUdp">
    /// <see langword="true" /> for a CONNECT-UDP reply, whose <c>101</c> ignores <c>Content-Length</c>
    /// as a <c>2xx</c> does (curl 8.21.0's <c>lib/cf-h1-proxy.c</c>, BL-1399).
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The reply as <see cref="ReadReplyAsync(IConnection, CancellationToken)" /> returns it; a
    /// reply whose status does not ignore <c>Content-Length</c> (<see cref="IgnoresBodyFields" />)
    /// and whose <c>Content-Length</c> is not a number is exit 8
    /// <see cref="UnsupportedContentLength" />, its head ending with that field's line.
    /// </returns>
    public static async ValueTask<HttpProxyTunnelReply> ReadReplyAsync(IConnection connection, bool forConnectUdp, CancellationToken cancellationToken)
    {
        var header = new List<byte>();
        var oneByte = new byte[1];
        var lineStart = 0;
        while (await connection.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false) == 1)
        {
            header.Add(oneByte[0]);
            if (ReplyAfterLatestByte(header, ref lineStart, forConnectUdp) is { } reply)
            {
                return reply;
            }
        }

        return HttpProxyTunnelReply.Failed("Proxy CONNECT aborted");
    }

    // The reply once the byte just added ends it, or null while more is to be read; a line
    // holding nothing but an optional CR ends the block, as curl accepts bare LF line endings.
    private static HttpProxyTunnelReply? ReplyAfterLatestByte(List<byte> header, ref int lineStart, bool forConnectUdp)
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
            return ParseReply(header, forConnectUdp);
        }

        lineStart = header.Count;
        return null;
    }

    // The line from lineStart to the LF just read holds nothing but that LF, or a CR and it.
    private static bool IsEmptyLine(List<byte> header, int lineStart) =>
        header.Count - lineStart == 1 || (header.Count - lineStart == 2 && header[lineStart] == '\r');

    /// <summary>
    /// The message curl 8.21.0 fails a reply with, exit 8, when its status does not ignore
    /// <c>Content-Length</c> and the value is not a number (measured, BL-1399).
    /// </summary>
    internal const string UnsupportedContentLength = "Unsupported Content-Length value";

    /// <summary>
    /// Decides whether curl 8.21.0 ignores a reply's <c>Content-Length</c> and
    /// <c>Transfer-Encoding</c>, as RFC 9110 section 9.3.6 has a client do for a <c>2xx</c> to
    /// CONNECT: any <c>2xx</c>, and for CONNECT-UDP a <c>101</c> too (<c>lib/cf-h1-proxy.c</c>).
    /// </summary>
    /// <param name="statusCode">The reply's status code.</param>
    /// <param name="forConnectUdp">Whether the reply answers CONNECT-UDP.</param>
    /// <returns><see langword="true" /> when the fields are ignored.</returns>
    internal static bool IgnoresBodyFields(int statusCode, bool forConnectUdp) =>
        statusCode / 100 == 2 || (forConnectUdp && statusCode == 101);

    // The status code and the fields curl 8.21.0 acts on after a CONNECT: Proxy-Authenticate,
    // Content-Length, Connection and Proxy-Connection close, and a chunked Transfer-Encoding;
    // or exit 8 at the first Content-Length that is not a number, unless the status ignores it.
    private static HttpProxyTunnelReply ParseReply(List<byte> header, bool forConnectUdp)
    {
        var statusCode = ParseStatusCode(header);
        var readsContentLength = !IgnoresBodyFields(statusCode, forConnectUdp);
        List<string> proxyAuthenticate = [];
        long contentLength = 0;
        var reusable = true;
        var chunked = false;
        foreach (var (name, value, lineEnd) in HeaderFields(header))
        {
            switch (name.ToUpperInvariant())
            {
                case "PROXY-AUTHENTICATE":
                    proxyAuthenticate.Add(value);
                    break;
                case "CONTENT-LENGTH" when readsContentLength:
                    if (ContentLengthOf(value) is not { } length)
                    {
                        return UnsupportedContentLengthReply(header, lineEnd);
                    }

                    contentLength = length;
                    break;
                case "CONNECTION" or "PROXY-CONNECTION":
                    reusable &= !HasToken(value, "close");
                    break;
                case "TRANSFER-ENCODING":
                    chunked |= HasToken(value, "chunked");
                    break;
            }
        }

        return new HttpProxyTunnelReply(statusCode, null)
        {
            ProxyAuthenticate = proxyAuthenticate,
            ContentLength = contentLength,
            IsChunked = chunked,
            LeavesConnectionReusable = reusable,
            Head = header.ToArray(),
        };
    }

    // The exit 8 reply, its head the bytes up to the end of the offending line, as curl's -v
    // shows no reply line after it (measured, BL-1399).
    private static HttpProxyTunnelReply UnsupportedContentLengthReply(List<byte> header, int lineEnd) =>
        HttpProxyTunnelReply.Failed(UnsupportedContentLength) with
        {
            FailureExitCode = CurlExitCode.WeirdServerReply,
            Head = header[..lineEnd].ToArray(),
        };

    // The number a Content-Length value starts with, as curl's curlx_str_numblanks reads it - its
    // leading digits, the blanks before them already trimmed - or null when it starts with no
    // digit or overflows.
    private static long? ContentLengthOf(string value)
    {
        var digits = value.Length - value.AsSpan().TrimStart("0123456789").Length;
        return long.TryParse(value.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : null;
    }

    // Every "name: value" line after the status line, the value without the blanks around it,
    // and the offset just past the line's LF; a line with no colon, or nothing before it, is not
    // a field. The head ends with an LF, so every line has one.
    private static IEnumerable<(string Name, string Value, int LineEnd)> HeaderFields(List<byte> header)
    {
        var text = Encoding.Latin1.GetString([.. header]);
        for (var lineStart = text.IndexOf('\n', StringComparison.Ordinal) + 1; lineStart < text.Length;)
        {
            var lineEnd = text.IndexOf('\n', lineStart) + 1;
            var line = text[lineStart..(lineEnd - 1)];
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                yield return (line[..colon], line[(colon + 1)..].Trim(' ', '\t', '\r'), lineEnd);
            }

            lineStart = lineEnd;
        }
    }

    // The comma-separated value names the token, compared without regard to case.
    internal static bool HasToken(string value, string token) =>
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

    /// <summary>
    /// Reads and discards the chunked body of a reply that does not open the tunnel, one byte
    /// at a time so the next reply stays on the connection, as curl 8.21.0 ignores a chunked
    /// <c>407</c>'s body before it sends the next CONNECT on the same connection (BL-862 Notes).
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// <see langword="null" /> once the body has ended; else the exit 56 message curl prints:
    /// the chunk parser's for a malformed body, <c>Proxy CONNECT aborted</c> when the proxy
    /// closed the connection first or reading it failed.
    /// </returns>
    public static async ValueTask<string?> DiscardChunkedBodyAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var body = new HttpProxyTunnelChunkedBody();
        var oneByte = new byte[1];
        try
        {
            while (await connection.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false) == 1)
            {
                if (!body.Accept(oneByte[0]))
                {
                    return body.MalformedMessage;
                }
            }
        }
        catch (IOException)
        {
        }

        return "Proxy CONNECT aborted";
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
