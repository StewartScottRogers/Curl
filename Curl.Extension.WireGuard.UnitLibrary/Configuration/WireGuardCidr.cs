using System.Net;

namespace Curl.Extension.WireGuard.Configuration;

/// <summary>
/// An IP address with a prefix length, as wg-quick's <c>Address</c> and wg's <c>AllowedIPs</c> write it
/// (<c>10.0.0.2/24</c>). The address keeps its host bits, because <c>Address</c> names the interface's own
/// address within its network.
/// </summary>
/// <param name="Address">The address.</param>
/// <param name="PrefixLength">The number of leading bits that name the network: 0 to 32 for IPv4, 0 to 128 for IPv6.</param>
public sealed record WireGuardCidr(IPAddress Address, int PrefixLength);
