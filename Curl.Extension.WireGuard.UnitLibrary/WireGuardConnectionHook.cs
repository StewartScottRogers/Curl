using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard;

/// <summary>The <c>--wireguard</c> connection hook: curl's connector is set aside for one that tunnels each connection through WireGuard.</summary>
/// <param name="configuration">The wg-quick configuration <c>--wireguard</c> named.</param>
/// <param name="opener">Opens the UDP socket to the peer.</param>
/// <param name="timeProvider">The clock the handshake and TCP timers run on.</param>
/// <param name="random">The randomness for the handshake.</param>
internal sealed class WireGuardConnectionHook(WireGuardConfiguration configuration, IWireGuardUdpOpener opener, TimeProvider timeProvider, RandomNumberGenerator random) : IConnectionHook
{
    /// <inheritdoc />
    public ExtensionOption Option => WireGuardExtension.Option;

    /// <summary>Gets the configuration the tunnel is set up from.</summary>
    public WireGuardConfiguration Configuration => configuration;

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector) => new WireGuardConnector(configuration, opener, timeProvider, random);

    /// <summary>Gives the connector that tunnels each connection and runs curl's TLS handshake through it for a TLS target (ADR-1055), offering what <paramref name="curlConnector" /> offers through ALPN.</summary>
    /// <param name="curlConnector">The connector curl would use; set aside but for its ALPN list.</param>
    /// <param name="curlTlsProvider">The TLS provider curl built from the option group's TLS options.</param>
    /// <returns>The tunnel's connector.</returns>
    public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider)
    {
        ArgumentNullException.ThrowIfNull(curlConnector);
        return new WireGuardConnector(configuration, opener, timeProvider, random, curlTlsProvider, curlConnector.HttpOverTlsApplicationProtocols);
    }

    /// <summary>Gives a datagram connector that refuses every UDP socket, so nothing leaves outside the tunnel.</summary>
    /// <param name="curlDatagramConnector">The datagram connector curl would use; set aside.</param>
    /// <returns>The refusing datagram connector.</returns>
    public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) => new WireGuardDatagramConnector();
}
