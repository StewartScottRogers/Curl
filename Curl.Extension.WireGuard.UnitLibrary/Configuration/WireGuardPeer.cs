namespace Curl.Extension.WireGuard.Configuration;

/// <summary>One <c>[Peer]</c> section of a wg-quick configuration file: a far end of the tunnel.</summary>
/// <param name="PublicKey">The peer's 32-byte Curve25519 public key.</param>
/// <param name="PresharedKey">The 32-byte preshared key, or <see langword="null" /> when there is none.</param>
/// <param name="Endpoint">Where the peer is reached, or <see langword="null" /> when it is not given.</param>
/// <param name="AllowedIps">The addresses routed to and accepted from the peer, from every <c>AllowedIPs</c> line.</param>
/// <param name="PersistentKeepalive">Seconds between keepalives, or <see langword="null" /> when off.</param>
public sealed record WireGuardPeer(
    byte[] PublicKey,
    byte[]? PresharedKey,
    WireGuardEndpoint? Endpoint,
    IReadOnlyList<WireGuardCidr> AllowedIps,
    int? PersistentKeepalive);
