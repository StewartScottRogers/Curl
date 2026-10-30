using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Creates the FTP handler for <c>ftp</c> and <c>ftps</c> (ADR-0102): the control connection
    /// through <paramref name="connector" />, passive data
    /// connections through <paramref name="dataConnector" />, active ones (<c>-P</c>) on a
    /// <see cref="TcpConnectionListener" />, a <c>-P</c> interface name looked up with
    /// <see cref="SystemNetworkInterfaceLookup" /> (ADR-0110) and a <c>-P</c> host name
    /// resolved with <paramref name="dnsResolver" /> (ADR-0108), and TLS from
    /// <paramref name="tlsProvider" />.
    /// </summary>
    /// <param name="connector">Supplies the control connection.</param>
    /// <param name="dataConnector">Supplies the passive data connection.</param>
    /// <param name="tlsProvider">Upgrades a connection after an accepted <c>AUTH</c> or <c>PROT P</c>.</param>
    /// <param name="dnsResolver">Resolves a host name given to <c>-P</c>.</param>
    /// <param name="tracesFtp">Whether it writes the <c>--trace-config ftp</c> lines (<see cref="TracesFtp" />, BL-1162).</param>
    /// <param name="listener">Listens for active-mode data connections; a <see cref="TcpConnectionListener" /> when not given.</param>
    /// <param name="wildcardHooks">The wildcard hooks (ADR-0615, ADR-0619); <see langword="null" /> fetches a wildcard URL as a plain path.</param>
    /// <returns>The handler.</returns>
    /// <param name="stopsBeforeEntryPath">Whether a new control connection stops before <c>PWD</c> (<see cref="FtpProtocolHandler.StopsBeforeEntryPath" />, the debug build's <c>CURL_FTP_PWD_STOP</c>, BL-2636).</param>
    internal static FtpProtocolHandler CreateFtpProtocolHandler(IConnector connector, IConnector dataConnector, ITlsProvider tlsProvider, IDnsResolver dnsResolver, bool tracesFtp = false, IConnectionListener? listener = null, IFtpWildcardHooks? wildcardHooks = null, bool stopsBeforeEntryPath = false) =>
        new(connector, dataConnector, listener ?? new TcpConnectionListener(), tlsProvider, dnsResolver, new SystemNetworkInterfaceLookup()) { TracesStateMachine = tracesFtp, WildcardHooks = wildcardHooks, StopsBeforeEntryPath = stopsBeforeEntryPath };

    /// <summary>
    /// The connector FTP's passive data connections go through: the option group's pooling
    /// connector over its TCP connector's <see cref="TcpConnector.ForFtpDataConnections" />, so
    /// they share the pool's numbering and the connector's DNS cache and <c>--resolve</c>, but
    /// take only a matching <c>--connect-to</c> host, keeping the reply's port, as curl 8.21.0 does
    /// (upstream test713, BL-1976), and only <c>-m</c> or the system's own connect timeout ends
    /// one, as curl 8.21.0 does not hold it to <c>--connect-timeout</c> (measured, ADR-0286, BL-797).
    /// </summary>
    /// <param name="transports">The option group's connectors.</param>
    /// <returns>The connector.</returns>
    internal static IConnector FtpDataConnectorOf(CurlTransports transports) =>
        transports.PoolingConnector.Over(transports.TcpConnector.ForFtpDataConnections());
}
