using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Keeps only the addresses of the family <c>-4</c> or <c>-6</c> chose, as curl 8.21.0 does
/// for a host name: a URL or proxy host that is itself an IP address literal is dialled as
/// written whatever the family on Windows (<c>-6 http://127.0.0.1/</c> connects, measured, BL-500).
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
        Dialable(host, addresses, family, OperatingSystem.IsWindows());

    /// <summary>
    /// Returns the addresses of <paramref name="family" /> among <paramref name="addresses" />, as
    /// <see cref="Dialable(string, IReadOnlyList{IPAddress}, AddressFamily)" /> does, for a platform
    /// named by <paramref name="isWindows" />: off Windows an IP address literal of the other family
    /// is filtered out too, so <c>-6 http://127.0.0.1/</c> has nothing to dial and fails with exit 7
    /// as curl 8.18.0 on Linux does (CGR-186, BL-3715).
    /// </summary>
    /// <param name="host">The host the addresses were resolved for.</param>
    /// <param name="addresses">The addresses resolved for it.</param>
    /// <param name="family">The family <c>-4</c> or <c>-6</c> chose, or <see cref="AddressFamily.Unspecified" />.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns>The addresses that may be dialled.</returns>
    internal static IReadOnlyList<IPAddress> Dialable(string host, IReadOnlyList<IPAddress> addresses, AddressFamily family, bool isWindows) =>
        family == AddressFamily.Unspecified || (isWindows && IsAddressLiteral(host))
            ? addresses
            : addresses.Where(address => address.AddressFamily == family).ToArray();

    /// <summary>
    /// Tells whether <paramref name="host" /> is an IP address literal that <paramref name="family" />
    /// leaves nothing to dial off Windows, which curl reports as exit 7 rather than exit 6.
    /// </summary>
    /// <param name="host">The host of the URL or proxy.</param>
    /// <param name="family">The family <c>-4</c> or <c>-6</c> chose, or <see cref="AddressFamily.Unspecified" />.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns><see langword="true" /> for a literal under a family choice off Windows.</returns>
    internal static bool IsRefusedLiteral(string host, AddressFamily family, bool isWindows) =>
        family != AddressFamily.Unspecified && !isWindows && IsAddressLiteral(host);

    private static bool IsAddressLiteral(string host) => IPAddress.TryParse(host, out _);
}
