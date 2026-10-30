namespace Curl.Protocol.Abstractions;

/// <summary>
/// The transports the engine builds its own protocol handlers over, handed to the handlers an
/// extension adds (ADR-0999), so an extension's handler connects, resolves and secures its
/// connections exactly as curl's handlers do and never constructs a socket itself.
/// </summary>
/// <param name="Connector">Connects TCP; the engine's end-point recording connector, so <c>%{remote_ip}</c> and the like work for the extension's scheme too.</param>
/// <param name="DatagramConnector">Opens UDP channels; the engine's end-point recording datagram connector.</param>
/// <param name="TlsProvider">Upgrades a connection to TLS.</param>
/// <param name="DnsResolver">Resolves host names.</param>
public sealed record ProtocolHandlerServices(
    IConnector Connector,
    IDatagramConnector DatagramConnector,
    ITlsProvider TlsProvider,
    IDnsResolver DnsResolver);
