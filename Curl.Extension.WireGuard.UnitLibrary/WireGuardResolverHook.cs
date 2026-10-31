using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard;

/// <summary>The <c>--wireguard</c> resolver hook: curl's resolver is set aside for the tunnel's DNS (ADR-1161), so an HTTP/3 name is never looked up outside it (BL-4392).</summary>
/// <param name="configuration">The wg-quick configuration <c>--wireguard</c> named.</param>
/// <param name="opener">Opens the UDP socket to the peer.</param>
/// <param name="timeProvider">The clock the handshake and TCP timers run on.</param>
/// <param name="random">The randomness for the handshake.</param>
internal sealed class WireGuardResolverHook(WireGuardConfiguration configuration, IWireGuardUdpOpener opener, TimeProvider timeProvider, RandomNumberGenerator random) : IResolverHook
{
    /// <inheritdoc />
    public ExtensionOption Option => WireGuardExtension.Option;

    /// <inheritdoc />
    public IDnsResolver ResolverOver(IDnsResolver curlResolver) =>
        new WireGuardTunnelDnsResolver(new WireGuardConnector(configuration, opener, timeProvider, random));
}
