using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Finds the addresses of a network interface by its name, as curl's <c>Curl_if2ip</c>
/// does through <c>getifaddrs</c> for a <c>-P</c> value such as <c>lo</c> or <c>eth0</c>.
/// </summary>
/// <remarks>
/// Injected rather than called statically so that a test can name an interface without the
/// machine having one (ADR-0110).
/// </remarks>
public interface INetworkInterfaceLookup
{
    /// <summary>
    /// Finds the interface named <paramref name="interfaceName" />, compared without regard
    /// to case as curl compares it.
    /// </summary>
    /// <param name="interfaceName">The interface name, such as <c>lo</c>.</param>
    /// <returns>
    /// The interface's unicast addresses of every family, in the order the system lists them,
    /// or <see langword="null" /> when no interface has that name.
    /// </returns>
    IReadOnlyList<IPAddress>? FindAddresses(string interfaceName);
}
