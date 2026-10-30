using System.Text;

namespace Curl.Extension.DnsQuery.Transport.Https;

/// <summary>
/// Writes the HTTP/1.1 request that carries a DNS query over DNS over HTTPS (RFC 8484 section 4.1, ADR-1145): a POST to
/// <see cref="Path" /> whose body is the query in wire format, with <c>Content-Type</c> and <c>Accept</c>
/// <see cref="MediaType" /> (sections 4.1 and 6), and <c>Connection: close</c>, since one connection carries one query.
/// </summary>
internal static class DohRequest
{
    /// <summary>The path RFC 8484 section 4.1's example URI template uses, which DoH servers serve.</summary>
    public const string Path = "/dns-query";

    /// <summary>The media type of a DNS message (RFC 8484 section 6).</summary>
    public const string MediaType = "application/dns-message";

    /// <summary>Encodes the request.</summary>
    /// <param name="host">The server's host, an IPv6 address without brackets.</param>
    /// <param name="port">The server's port; the <c>Host</c> header names it unless it is 443.</param>
    /// <param name="query">The encoded DNS query.</param>
    /// <returns>The request's octets: its head, then the query.</returns>
    public static byte[] Encode(string host, int port, byte[] query)
    {
        string authority = host.Contains(':') ? $"[{host}]" : host;
        string hostHeader = port == DnsHttpsTransport.Port ? authority : $"{authority}:{port}";
        string head =
            $"POST {Path} HTTP/1.1\r\n" +
            $"Host: {hostHeader}\r\n" +
            $"Accept: {MediaType}\r\n" +
            $"Content-Type: {MediaType}\r\n" +
            $"Content-Length: {query.Length}\r\n" +
            "Connection: close\r\n\r\n";
        return [.. Encoding.ASCII.GetBytes(head), .. query];
    }
}
