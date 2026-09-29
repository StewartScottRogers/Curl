using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Keeps only the addresses of the family <c>-4</c> or <c>-6</c> chose, as curl 8.21.0 does
/// for a host name: a URL or proxy host that is itself an IP address literal is dialled as
/// written whatever the family (<c>-6 http://127.0.0.1/</c> connects, measured, BL-500).
/// </summary>
internal static class AddressFamilyFilter
{
    /// <summary>
    /// Returns the addresses of <paramref name="family" /> among <paramref name="addresses" />, in
    /// order; all of them when <paramref name="family" /> is <see cref="AddressFamily.Unspecified" />
    /// or <paramref name="host" /> is an IP address literal, bracketed or not.
    /// </summary>
    /// <param name="host">The host the addresses were resolved for.</param>
    /// <param name="addresses">The addresses resolved for it.</param>
    /// <param name="family">
    /// <see cref="AddressFamily.InterNetwork" /> for <c>-4</c>, <see cref="AddressFamily.InterNetworkV6" />
    /// for <c>-6</c>, or <see cref="AddressFamily.Unspecified" /> for either.
    /// </param>
    /// <returns>The addresses that may be dialled.</returns>
    internal static IReadOnlyList<IPAddress> Dialable(string host, IReadOnlyList<IPAddress> addresses, AddressFamily family) =>
        family == AddressFamily.Unspecified || IPAddress.TryParse(host, out _)
            ? addresses
            : addresses.Where(address => address.AddressFamily == family).ToArray();
}
