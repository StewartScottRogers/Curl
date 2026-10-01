using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The seams an <see cref="FtpSession" /> opens and secures its connections through.
/// </summary>
/// <param name="DataConnector">Opens a passive-mode data connection.</param>
/// <param name="Listener">Binds the port an active-mode data connection is accepted on.</param>
/// <param name="TlsProvider">Upgrades the control connection after <c>AUTH</c>, and data connections after <c>PROT P</c>.</param>
/// <param name="DnsResolver">Resolves a host name given to <c>-P</c> to the address to listen on and announce.</param>
/// <param name="InterfaceLookup">Finds the addresses of a network interface named by <c>-P</c>, tried before <paramref name="DnsResolver" />.</param>
internal sealed record FtpSessionConnections(
    IConnector DataConnector,
    IConnectionListener Listener,
    ITlsProvider TlsProvider,
    IDnsResolver DnsResolver,
    INetworkInterfaceLookup InterfaceLookup);
