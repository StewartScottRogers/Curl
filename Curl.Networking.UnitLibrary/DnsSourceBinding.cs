using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Where a DNS query's socket is bound: the <c>--dns-ipv4-addr</c> address for a query to an
/// IPv4 server and the <c>--dns-ipv6-addr</c> address for one to an IPv6 server, else the first
/// address of that family on the <c>--dns-interface</c> interface (a link-local IPv6 address only
/// when it has no other), else any address (BL-694).
/// </summary>
/// <param name="InterfaceName">The <c>--dns-interface</c> name, or <see langword="null" />.</param>
/// <param name="IPv4Address">The <c>--dns-ipv4-addr</c> address, or <see langword="null" />.</param>
/// <param name="IPv6Address">The <c>--dns-ipv6-addr</c> address, or <see langword="null" />.</param>
public sealed record DnsSourceBinding(string? InterfaceName, IPAddress? IPv4Address, IPAddress? IPv6Address)
{
    /// <summary>Gets the binding that leaves every socket on any address.</summary>
    public static DnsSourceBinding None { get; } = new(null, null, null);

    /// <summary>
    /// Parses the three options' verbatim values. An address that is not of its option's family
    /// is refused, which curl 8.22.0's c-ares build reports as exit 43 when it resolves a name
    /// (measured, BL-643); an interface name is taken as given.
    /// </summary>
    /// <param name="interfaceName">The <c>--dns-interface</c> value, or <see langword="null" />.</param>
    /// <param name="ipv4Address">The <c>--dns-ipv4-addr</c> value, or <see langword="null" />.</param>
    /// <param name="ipv6Address">The <c>--dns-ipv6-addr</c> value, or <see langword="null" />.</param>
    /// <param name="binding">The binding, when both addresses parsed.</param>
    /// <returns><see langword="true" /> when each given address is of its option's family.</returns>
    public static bool TryParse(string? interfaceName, string? ipv4Address, string? ipv6Address, [NotNullWhen(true)] out DnsSourceBinding? binding)
    {
        binding = null;
        if (!TryParseOptional(ipv4Address, AddressFamily.InterNetwork, out var ipv4) || !TryParseOptional(ipv6Address, AddressFamily.InterNetworkV6, out var ipv6))
        {
            return false;
        }

        binding = new DnsSourceBinding(interfaceName, ipv4, ipv6);
        return true;
    }

    /// <summary>
    /// Chooses the local address a socket to a server of <paramref name="family" /> binds. An
    /// interface that does not exist, or has no address of the family, leaves the socket on any
    /// address, as the c-ares build goes on when it cannot bind to the device (measured, BL-694).
    /// </summary>
    /// <param name="family">The server's address family.</param>
    /// <param name="findInterfaceAddresses">Finds an interface's addresses by name; <see langword="null" /> when there is none.</param>
    /// <returns>The address to bind.</returns>
    public IPAddress LocalAddressFor(AddressFamily family, Func<string, IReadOnlyList<IPAddress>?> findInterfaceAddresses)
    {
        ArgumentNullException.ThrowIfNull(findInterfaceAddresses);

        return GivenAddressFor(family)
            ?? InterfaceAddressFor(family, findInterfaceAddresses)
            ?? (family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any);
    }

    private IPAddress? GivenAddressFor(AddressFamily family) =>
        family == AddressFamily.InterNetworkV6 ? IPv6Address : IPv4Address;

    private IPAddress? InterfaceAddressFor(AddressFamily family, Func<string, IReadOnlyList<IPAddress>?> findInterfaceAddresses) =>
        InterfaceName is null
            ? null
            : findInterfaceAddresses(InterfaceName)?.Where(address => address.AddressFamily == family).OrderBy(address => address.IsIPv6LinkLocal).FirstOrDefault();

    private static bool TryParseOptional(string? text, AddressFamily family, out IPAddress? address)
    {
        address = null;
        return text is null || DnsServerList.TryParseAddress(text, family, out address);
    }
}
