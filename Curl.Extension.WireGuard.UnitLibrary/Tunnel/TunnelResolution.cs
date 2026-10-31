using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>What DNS through the tunnel gave: an address, a tunnel failure to report as it is, or neither (the name has no answer).</summary>
/// <param name="Address">The first address of the answer, or <see langword="null" />.</param>
/// <param name="Failure">The failure to reach the DNS server through the tunnel, or <see langword="null" /> when it was reached.</param>
internal sealed record TunnelResolution(IPAddress? Address, ConnectResult? Failure);
