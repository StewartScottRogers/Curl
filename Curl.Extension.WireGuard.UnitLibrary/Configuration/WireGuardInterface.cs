namespace Curl.Extension.WireGuard.Configuration;

/// <summary>The <c>[Interface]</c> section of a wg-quick configuration file: this end of the tunnel.</summary>
/// <param name="PrivateKey">The 32-byte Curve25519 private key.</param>
/// <param name="Addresses">The interface's own addresses, from every <c>Address</c> line.</param>
/// <param name="DnsServers">The <c>DNS</c> entries, IP addresses or search domains, empty when there are none.</param>
/// <param name="ListenPort">The UDP port to listen on, or <see langword="null" /> to choose one.</param>
/// <param name="Mtu">The tunnel's MTU, or <see langword="null" /> for the default.</param>
public sealed record WireGuardInterface(
    byte[] PrivateKey,
    IReadOnlyList<WireGuardCidr> Addresses,
    IReadOnlyList<string> DnsServers,
    int? ListenPort,
    int? Mtu);
