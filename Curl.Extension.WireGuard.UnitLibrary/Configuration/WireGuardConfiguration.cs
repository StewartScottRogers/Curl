namespace Curl.Extension.WireGuard.Configuration;

/// <summary>A parsed wg-quick configuration file: one interface and at least one peer.</summary>
/// <param name="Interface">The <c>[Interface]</c> section.</param>
/// <param name="Peers">The <c>[Peer]</c> sections, in file order.</param>
public sealed record WireGuardConfiguration(WireGuardInterface Interface, IReadOnlyList<WireGuardPeer> Peers);
