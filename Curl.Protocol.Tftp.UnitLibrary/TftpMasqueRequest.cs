using System.Globalization;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Builds the MASQUE <c>connect-udp</c> request curl 8.21.0 sends to an HTTP or HTTPS proxy
/// for a <c>tftp://</c> URL before it fails with exit 7 (ADR-0056, rule 4; ADR-0074).
/// </summary>
/// <remarks>
/// Measured by BL-330, BL-345 and BL-398: the request line is <c>GET</c> of
/// <c>http://proxy/.well-known/masque/udp/host/port/</c> (<c>https://proxy/…</c> through an
/// HTTPS proxy), <c>HTTP/1.0</c> for
/// <c>--proxy1.0</c>, else <c>HTTP/1.1</c>; the target host is percent-encoded, so
/// <c>::1</c> is <c>%3A%3A1</c>. The headers are <c>Host</c> (the proxy),
/// <c>Proxy-Authorization</c> (only with a proxy credential), <c>User-Agent</c> (the
/// <c>-A</c> value, none for an empty one, else <c>curl/8.21.0</c>),
/// <c>Proxy-Connection: Keep-Alive</c>, <c>Connection: Upgrade</c>,
/// <c>Upgrade: connect-udp</c> and <c>Capsule-Protocol: ?1</c>.
/// </remarks>
internal static class TftpMasqueRequest
{
    /// <summary>The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.</summary>
    private const string DefaultUserAgent = "curl/8.21.0";

    /// <summary>
    /// Builds the request for <paramref name="host" /> and <paramref name="port" /> through
    /// <paramref name="proxy" />.
    /// </summary>
    /// <param name="host">The TFTP server's host, IPv6 addresses without brackets.</param>
    /// <param name="port">The TFTP server's UDP port.</param>
    /// <param name="proxy">The HTTP or HTTPS proxy: its kind picks the URL scheme and the HTTP version, and its credential becomes <c>Proxy-Authorization</c>.</param>
    /// <param name="userAgent">The <c>-A</c> value, or <see langword="null" /> for curl's own.</param>
    /// <param name="credentialEncoding">The encoding the proxy credential is base64-encoded from.</param>
    /// <returns>The request bytes, ending with the empty line.</returns>
    public static byte[] Build(string host, int port, ProxyEndpoint proxy, string? userAgent, Encoding credentialEncoding)
    {
        var proxyAuthority = FormatAuthority(proxy.Host, proxy.Port);
        var scheme = proxy.Kind == ProxyKind.Https ? "https" : "http";
        var version = proxy.Kind == ProxyKind.Http10 ? "HTTP/1.0" : "HTTP/1.1";
        var request = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"GET {scheme}://{proxyAuthority}/.well-known/masque/udp/{Uri.EscapeDataString(host)}/{port}/ {version}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Host: {proxyAuthority}\r\n");

        if (proxy.Credential is { } credential)
        {
            var basic = Convert.ToBase64String(credentialEncoding.GetBytes($"{credential.UserName}:{credential.Password}"));
            request.Append(CultureInfo.InvariantCulture, $"Proxy-Authorization: Basic {basic}\r\n");
        }

        userAgent ??= DefaultUserAgent;
        if (userAgent.Length > 0)
        {
            request.Append(CultureInfo.InvariantCulture, $"User-Agent: {userAgent}\r\n");
        }

        request.Append("Proxy-Connection: Keep-Alive\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n");
        return Encoding.Latin1.GetBytes(request.ToString());
    }

    // host:port, with an IPv6 address in brackets.
    private static string FormatAuthority(string host, int port) =>
        host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[')
            ? $"[{host}]:{port}"
            : $"{host}:{port}";
}
