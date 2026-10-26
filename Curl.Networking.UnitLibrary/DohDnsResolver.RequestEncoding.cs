using System.Net;
using System.Text;

namespace Curl.Networking;

// The HTTPS query and the POST that carries each query.
public sealed partial class DohDnsResolver
{
    /// <summary>The HTTPS query for <paramref name="host" />, or <see langword="null" /> for a literal, <c>localhost</c> or a name that does not encode.</summary>
    private static byte[]? HttpsQueryFor(string host, int port)
    {
        if (IPAddress.TryParse(host.Trim('[', ']'), out _) || TcpConnector.IsLocalhost(host))
        {
            return null;
        }

        var query = DnsQueryEncoder.Encode(HttpsQueryName(host, port), DnsRecordType.Https);
        return query.Failure == DnsMessageFailure.None ? query.Bytes : null;
    }

    /// <summary>The name curl asks the HTTPS record of: the host on port 443, <c>_&lt;port&gt;._https.&lt;host&gt;</c> on any other.</summary>
    /// <param name="host">The host name.</param>
    /// <param name="port">The port the transfer connects to.</param>
    /// <returns>The query name.</returns>
    internal static string HttpsQueryName(string host, int port) =>
        port == 443 ? host : $"_{port}._https.{host}";

    // The request line and header block of the POST curl sends for one query: through a forwarding
    // proxy the target is absolute and Proxy-Connection follows Accept (measured, CGR-037).
    private byte[] BuildRequestHead(byte[] query)
    {
        var host = _dohUrl.IsDefaultPort ? _dohUrl.Host : $"{_dohUrl.Host}:{_dohUrl.Port}";
        var forwarded = ForwardsThroughProxy;
        var target = forwarded ? $"{_dohUrl.Scheme}://{host}{_dohUrl.PathAndQuery}" : _dohUrl.PathAndQuery;
        var head = $"POST {target} HTTP/1.1\r\n"
            + $"Host: {host}\r\n"
            + "Accept: */*\r\n"
            + (forwarded ? "Proxy-Connection: Keep-Alive\r\n" : string.Empty)
            + "Content-Type: application/dns-message\r\n"
            + $"Content-Length: {query.Length}\r\n"
            + "\r\n";
        return Encoding.Latin1.GetBytes(head);
    }
}
