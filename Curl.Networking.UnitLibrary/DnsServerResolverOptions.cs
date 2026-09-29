using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// What <see cref="DnsServerResolver" /> is configured with: the four c-ares options' verbatim
/// values, parsed when a name is resolved as curl's c-ares build parses them, and the <c>-4</c> or
/// <c>-6</c> choice.
/// </summary>
/// <param name="ServerList">The <c>--dns-servers</c> value; <see langword="null" /> for the system's DNS servers.</param>
/// <param name="InterfaceName">The <c>--dns-interface</c> value, or <see langword="null" />.</param>
/// <param name="IPv4Address">The <c>--dns-ipv4-addr</c> value, or <see langword="null" />.</param>
/// <param name="IPv6Address">The <c>--dns-ipv6-addr</c> value, or <see langword="null" />.</param>
/// <param name="AddressFamily">
/// <see cref="AddressFamily.InterNetwork" /> under <c>-4</c> (A queries only),
/// <see cref="AddressFamily.InterNetworkV6" /> under <c>-6</c> (AAAA only), otherwise
/// <see cref="AddressFamily.Unspecified" /> (AAAA and A).
/// </param>
public sealed record DnsServerResolverOptions(
    string? ServerList,
    string? InterfaceName,
    string? IPv4Address,
    string? IPv6Address,
    AddressFamily AddressFamily = AddressFamily.Unspecified)
{
    /// <summary>
    /// Gets a value indicating whether any of the four options was given, so a lookup goes through
    /// <see cref="DnsServerResolver" /> rather than the system resolver.
    /// </summary>
    public bool IsAnyGiven => (ServerList ?? InterfaceName ?? IPv4Address ?? IPv6Address) is not null;
}
