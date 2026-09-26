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
    /// Builds the CONNECT request for <paramref name="target" /> through <paramref name="proxy" />.
    /// </summary>
    /// <param name="target">The host and port the tunnel reaches.</param>
    /// <param name="proxy">The proxy, whose kind picks HTTP/1.0 or HTTP/1.1 and whose credential becomes <c>Proxy-Authorization</c>.</param>
    /// <param name="options">The <c>User-Agent</c> and the credential encoding.</param>
    /// <returns>The request bytes, headers in curl's order, ending with the empty line.</returns>
    public static byte[] BuildConnectRequest(ConnectTarget target, ProxyEndpoint proxy, HttpProxyTunnelOptions options)
    {
        var authority = FormatAuthority(target);
        var version = proxy.Kind == ProxyKind.Http10 ? "HTTP/1.0" : "HTTP/1.1";
        var request = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"CONNECT {authority} {version}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Host: {authority}\r\n");

        if (proxy.Credential is { } credential)
        {
            var userAndPassword = options.CredentialEncoding.GetBytes($"{credential.UserName}:{credential.Password}");
            request.Append(CultureInfo.InvariantCulture, $"Proxy-Authorization: Basic {Convert.ToBase64String(userAndPassword)}\r\n");
        }

        if (options.UserAgent is { } userAgent)
        {
            request.Append(CultureInfo.InvariantCulture, $"User-Agent: {userAgent}\r\n");
        }

        request.Append("Proxy-Connection: Keep-Alive\r\n\r\n");
        return Encoding.Latin1.GetBytes(request.ToString());
    }

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
    /// header block, and returns the status code on its first line.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The status code, <c>0</c> when the first line is not an HTTP status line, as curl
    /// reports it; or the exit 56 message when the proxy closed the connection before the
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
            return new HttpProxyTunnelReply(ParseStatusCode(header), null);
        }

        lineStart = header.Count;
        return null;
    }

    // The line from lineStart to the LF just read holds nothing but that LF, or a CR and it.
    private static bool IsEmptyLine(List<byte> header, int lineStart) =>
        header.Count - lineStart == 1 || (header.Count - lineStart == 2 && header[lineStart] == '\r');

    private static string FormatAuthority(ConnectTarget target) =>
        target.Host.Contains(':', StringComparison.Ordinal) && !target.Host.StartsWith('[')
            ? $"[{target.Host}]:{target.Port}"
            : $"{target.Host}:{target.Port}";

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
