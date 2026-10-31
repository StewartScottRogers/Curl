using System.Net;

namespace Curl.Extension.Dane;

/// <summary>The recursive resolver <c>--dane</c> named: a host name or address, and a port, 53 unless one is given.</summary>
/// <param name="Host">The resolver's host name or address, an IPv6 address without brackets.</param>
/// <param name="Port">The resolver's UDP and TCP port.</param>
internal sealed record DaneResolverAddress(string Host, int Port)
{
    private const int DnsPort = 53;

    /// <summary>Reads <c>host</c>, <c>host:port</c>, a bare IPv4 or IPv6 address or <c>[IPv6]:port</c>.</summary>
    /// <param name="text">The <c>--dane</c> value.</param>
    /// <returns>The address, or <see langword="null" /> when the text is not one of those forms.</returns>
    internal static DaneResolverAddress? Parse(string text)
    {
        if (!text.StartsWith('[') && IPAddress.TryParse(text, out IPAddress? address))
        {
            return new DaneResolverAddress(address.ToString(), DnsPort);
        }

        return Uri.TryCreate("dns://" + text, UriKind.Absolute, out Uri? uri) && NamesOnlyAServer(uri)
            ? new DaneResolverAddress(uri.IdnHost, uri.IsDefaultPort ? DnsPort : uri.Port)
            : null;
    }

    // Nothing but the host and port: no user information, path, query or fragment.
    private static bool NamesOnlyAServer(Uri uri) =>
        uri.IdnHost.Length > 0 && uri.UserInfo.Length == 0 && uri.PathAndQuery.Length + uri.Fragment.Length <= 1;
}
