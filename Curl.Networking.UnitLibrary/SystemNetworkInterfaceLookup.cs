using System.Net;
using System.Net.NetworkInformation;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="INetworkInterfaceLookup" />: finds an interface among
/// <see cref="NetworkInterface.GetAllNetworkInterfaces" /> off Windows, as the OpenSSL builds
/// of curl 8.21.0 find one with <c>getifaddrs</c>, and finds none on Windows, whose Schannel
/// build has no <c>getifaddrs</c> and resolves every <c>-P</c> name as a host name (ADR-0108,
/// ADR-0110).
/// </summary>
public sealed class SystemNetworkInterfaceLookup : INetworkInterfaceLookup
{
    private readonly Func<IEnumerable<(string Name, IPAddress[] Addresses)>> _listInterfaces;

    /// <summary>
    /// Initializes a lookup of this machine's interfaces, which finds none on Windows.
    /// </summary>
    public SystemNetworkInterfaceLookup()
        : this(OperatingSystem.IsWindows(), ListSystemInterfaces)
    {
    }

    /// <summary>
    /// Initializes a lookup that behaves as it does on Windows or elsewhere, over
    /// <paramref name="listInterfaces" />, so a test can pin either platform's answer.
    /// </summary>
    /// <param name="onWindows">Whether to find no interface at all, as the Schannel build does.</param>
    /// <param name="listInterfaces">Lists each interface's name and unicast addresses.</param>
    internal SystemNetworkInterfaceLookup(bool onWindows, Func<IEnumerable<(string Name, IPAddress[] Addresses)>> listInterfaces)
    {
        _listInterfaces = onWindows ? static () => [] : listInterfaces;
    }

    /// <inheritdoc />
    public IReadOnlyList<IPAddress>? FindAddresses(string interfaceName)
    {
        ArgumentNullException.ThrowIfNull(interfaceName);

        foreach ((string name, IPAddress[] addresses) in _listInterfaces())
        {
            if (string.Equals(name, interfaceName, StringComparison.OrdinalIgnoreCase))
            {
                return addresses;
            }
        }

        return null;
    }

    /// <summary>Lists this machine's interfaces with their unicast addresses.</summary>
    /// <returns>Each interface's name and unicast addresses, in the system's order.</returns>
    internal static IEnumerable<(string Name, IPAddress[] Addresses)> ListSystemInterfaces() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Select(networkInterface => (
                networkInterface.Name,
                networkInterface.GetIPProperties().UnicastAddresses.Select(unicast => unicast.Address).ToArray()));
}
