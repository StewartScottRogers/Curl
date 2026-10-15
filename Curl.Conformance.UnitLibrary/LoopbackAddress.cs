using System.Net;
using System.Net.Sockets;

namespace Curl.Conformance;

/// <summary>
/// The loopback address an in-memory connection's end points take: <c>::1</c> when the dialled host
/// is an IPv6 address (upstream's <c>http-ipv6</c>, <c>ftp-ipv6</c> and <c>gopher-ipv6</c> servers
/// on <c>%HOST6IP</c>), <c>127.0.0.1</c> otherwise.
/// </summary>
internal static class LoopbackAddress
{
    /// <summary>Gives the loopback address of <paramref name="host"/>'s family.</summary>
    /// <param name="host">The dialled host, an IPv6 address bracketed or not, or anything else.</param>
    /// <returns><see cref="IPAddress.IPv6Loopback"/> for an IPv6 address, otherwise <see cref="IPAddress.Loopback"/>.</returns>
    public static IPAddress For(string host) =>
        IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6
            ? IPAddress.IPv6Loopback
            : IPAddress.Loopback;
}
