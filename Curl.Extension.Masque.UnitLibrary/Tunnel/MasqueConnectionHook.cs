using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Masque.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// The <c>--masque</c> connection hook: curl's stream connector is kept, and is the one that reaches the proxy;
/// curl's datagram connector, which QUIC and TFTP open their sockets through (ADR-1010), is replaced by a
/// <see cref="MasqueDatagramConnector" /> opening each datagram channel through the proxy.
/// </summary>
/// <remarks>
/// The engine asks for the datagram connector when it builds the transports, before it hands the hook curl's
/// stream connector, so the hook's datagram connector reaches the proxy through a <see cref="LateBoundConnector" />
/// that <see cref="ConnectorOver(IConnector)" /> binds.
/// </remarks>
/// <param name="uriTemplate">The proxy's URI template.</param>
/// <param name="kind">Which tunnel the template asks for.</param>
internal sealed class MasqueConnectionHook(string uriTemplate, MasqueTunnelKind kind) : IConnectionHook
{
    private readonly LateBoundConnector _proxyConnector = new();

    /// <inheritdoc />
    public ExtensionOption Option => MasqueExtension.Option;

    /// <summary>Gets the proxy's URI template.</summary>
    public string UriTemplate => uriTemplate;

    /// <summary>Gets which tunnel the hook opens.</summary>
    public MasqueTunnelKind Kind => kind;

    /// <summary>Keeps <paramref name="curlConnector" /> for the transfer's streams and binds it as the way to the proxy.</summary>
    /// <param name="curlConnector">The connector curl would use.</param>
    /// <returns><paramref name="curlConnector" />.</returns>
    public IConnector ConnectorOver(IConnector curlConnector)
    {
        ArgumentNullException.ThrowIfNull(curlConnector);
        _proxyConnector.Bind(curlConnector);
        return curlConnector;
    }

    /// <summary>Gives the datagram connector that opens every channel through the MASQUE proxy.</summary>
    /// <param name="curlDatagramConnector">The datagram connector curl would use; set aside.</param>
    /// <returns>The MASQUE datagram connector.</returns>
    public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) =>
        new MasqueDatagramConnector(_proxyConnector, uriTemplate, kind);
}
