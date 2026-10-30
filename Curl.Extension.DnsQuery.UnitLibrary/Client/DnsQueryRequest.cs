using Curl.Extension.DnsQuery.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.Client;

/// <summary>
/// What a <c>dns:</c> URL asks for (RFC 4501 section 2): the server from <c>dnsauthority</c>, if any, on port 53 unless
/// it names another; the percent-decoded <c>dnsname</c>; and the QTYPE and QCLASS from the <c>TYPE=</c> and
/// <c>CLASS=</c> query elements, in either case, separated by <c>;</c> as RFC 4501 writes them or by <c>&amp;</c>.
/// </summary>
/// <param name="Server">The server's host, or <see langword="null" /> when the URL names none.</param>
/// <param name="Port">The server's port.</param>
/// <param name="Name">The name to query, in presentation format.</param>
/// <param name="Type">The QTYPE; A unless the URL gives one.</param>
/// <param name="Class">The QCLASS; IN unless the URL gives one.</param>
internal sealed record DnsQueryRequest(string? Server, int Port, string Name, ushort Type, ushort Class)
{
    /// <summary>The URL scheme.</summary>
    public const string Scheme = "dns";

    /// <summary>The DNS port (RFC 1035 section 4.2).</summary>
    public const int DefaultPort = 53;

    private const ushort AddressType = 1;
    private const ushort InternetClass = 1;

    /// <summary>Reads the request a URL makes.</summary>
    /// <param name="url">The <c>dns:</c> URL.</param>
    /// <returns>The request.</returns>
    /// <exception cref="DnsQueryException">The URL names no name, or a query element that is not a known TYPE= or CLASS= (exit 3).</exception>
    public static DnsQueryRequest Of(CurlUrl url)
    {
        string name = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/'));
        if (name.Length == 0)
        {
            throw Malformed("The dns: URL names no DNS name to query");
        }

        DnsQueryRequest request = new(url.IdnHost.Length == 0 ? null : url.IdnHost, url.Port >= 0 ? url.Port : DefaultPort, name, AddressType, InternetClass);
        return (url.Query ?? string.Empty)
            .Split([';', '&'], StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(request, WithElement);
    }

    private static DnsQueryRequest WithElement(DnsQueryRequest request, string element)
    {
        string[] parts = element.Split('=', 2);
        string value = Uri.UnescapeDataString(parts.Length == 2 ? parts[1] : string.Empty);
        return parts[0].ToUpperInvariant() switch
        {
            "TYPE" when DnsMnemonics.TryParseType(value, out ushort type) => request with { Type = type },
            "CLASS" when DnsMnemonics.TryParseClass(value, out ushort recordClass) => request with { Class = recordClass },
            _ => throw Malformed($"The dns: URL's query element \"{element}\" is not TYPE= or CLASS= with a known value"),
        };
    }

    private static DnsQueryException Malformed(string message) => new(CurlExitCode.UrlMalformat, message);
}
