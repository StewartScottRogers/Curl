using System.Globalization;
using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Writes the connect steps to Curl's own diagnostic log (ADR-0222, BL-920): name resolution
/// as <see cref="DiagnosticLogComponents.Dns" />, dials and pooling as
/// <see cref="DiagnosticLogComponents.Connect" />, tunnels as
/// <see cref="DiagnosticLogComponents.Proxy" />, handshakes as
/// <see cref="DiagnosticLogComponents.Tls" /> and QUIC dials as
/// <see cref="DiagnosticLogComponents.Quic" />.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message, so
/// a disabled level costs no formatting. No method takes a credential: a proxy is named by its
/// kind, host and port, never its user or password, and a <c>Proxy-Authorization</c> is never
/// passed in (ADR-0222, decision 7).
/// </remarks>
internal sealed class NetworkDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>Gets whether a line at <paramref name="level" /> would be written.</summary>
    /// <param name="level">The level asked about.</param>
    /// <returns><see langword="true" /> when it would.</returns>
    public bool IsEnabled(DiagnosticLogLevel level) => log.IsEnabled(level);

    /// <summary>Logs, at <c>info</c>, the addresses a name resolved to and how long it took.</summary>
    /// <param name="host">The name resolved.</param>
    /// <param name="port">The port it was resolved for.</param>
    /// <param name="addresses">The addresses, in the order they are dialled.</param>
    /// <param name="fromCache">Whether the DNS cache or a <c>--resolve</c> entry answered.</param>
    /// <param name="elapsed">How long the resolve took.</param>
    public void Resolved(string host, int port, IReadOnlyList<IPAddress> addresses, bool fromCache, TimeSpan elapsed)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            var source = fromCache ? "from the DNS cache" : "by lookup";
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns, string.Create(
                CultureInfo.InvariantCulture,
                $"{host}:{port} resolved {source} to {JoinAddresses(addresses)} in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, a name that resolved to no address that can be dialled.</summary>
    /// <param name="host">The name.</param>
    /// <param name="port">The port it was resolved for.</param>
    public void NotResolved(string host, int port)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Dns, string.Create(
                CultureInfo.InvariantCulture,
                $"{host}:{port} did not resolve to an address that can be dialled"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an address about to be dialled.</summary>
    /// <param name="remoteEndPoint">The address and port.</param>
    public void Dialling(EndPoint remoteEndPoint)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect, $"dialling {remoteEndPoint}");
        }
    }

    /// <summary>
    /// Logs, at <c>warning</c>, a dial that failed; the next address, if any, is tried.
    /// </summary>
    /// <param name="remoteEndPoint">The address and port dialled.</param>
    /// <param name="exception">Why it failed.</param>
    public void DialFailed(EndPoint remoteEndPoint, SocketException exception)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Connect, string.Create(
                CultureInfo.InvariantCulture,
                $"dial to {remoteEndPoint} failed with {exception.SocketErrorCode} ({exception.GetType().Name}: {exception.Message})"));
        }
    }

    /// <summary>Logs, at <c>info</c>, the TCP connection made.</summary>
    /// <param name="remoteEndPoint">The end point connected to.</param>
    /// <param name="localEndPoint">The local end point, or <see langword="null" /> when the dialer reported none.</param>
    public void Connected(EndPoint remoteEndPoint, EndPoint? localEndPoint)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Connect, $"connected to {remoteEndPoint} from {localEndPoint?.ToString() ?? "an unreported local end point"}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, a CONNECT about to be sent to an HTTP or HTTPS proxy.</summary>
    /// <param name="proxy">The proxy, named by kind, host and port only.</param>
    /// <param name="host">The host the CONNECT names.</param>
    /// <param name="port">The port the CONNECT names.</param>
    public void TunnelRequested(ProxyEndpoint proxy, string host, int port)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy, $"sending CONNECT {HttpProxyTunnel.FormatAuthority(host, port)} to {ProxyName(proxy)}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, a SOCKS handshake about to run.</summary>
    /// <param name="proxy">The proxy.</param>
    /// <param name="host">The host asked for.</param>
    /// <param name="port">The port asked for.</param>
    public void SocksHandshakeStarting(ProxyEndpoint proxy, string host, int port)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy, $"{ProxyName(proxy)} handshake for {HttpProxyTunnel.FormatAuthority(host, port)}");
        }
    }

    /// <summary>Logs, at <c>info</c>, a tunnel open through a proxy.</summary>
    /// <param name="proxy">The proxy.</param>
    /// <param name="host">The host the tunnel reaches.</param>
    /// <param name="port">The port the tunnel reaches.</param>
    /// <param name="statusCode">The CONNECT reply's status, or <c>0</c> for SOCKS.</param>
    public void TunnelEstablished(ProxyEndpoint proxy, string host, int port, int statusCode)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            var reply = statusCode == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" (CONNECT {statusCode})");
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Proxy, $"tunnel to {HttpProxyTunnel.FormatAuthority(host, port)} established through {ProxyName(proxy)}{reply}");
        }
    }

    /// <summary>
    /// Logs a completed handshake: at <c>info</c> its version, cipher suite, ALPN and route, at
    /// <c>verbose</c> each certificate the server sent and whether the chain was verified.
    /// </summary>
    /// <param name="host">The host the handshake was with.</param>
    /// <param name="route">The TLS client that ran it, or <see langword="null" /> for a provider that does not say.</param>
    /// <param name="routeReason">Why the options chose <paramref name="route" />, as <see cref="TlsClientRouting.Reason" /> words it, or <see langword="null" /> when none did.</param>
    /// <param name="handshake">What the provider reported, or <see langword="null" /> when it reported nothing.</param>
    /// <param name="applicationProtocol">The protocol ALPN agreed, or <see langword="null" />.</param>
    public void HandshakeCompleted(string host, TlsClientRoute? route, string? routeReason, TlsHandshakeEvent? handshake, string? applicationProtocol)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            var reason = routeReason is null ? string.Empty : $" ({routeReason})";
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Tls, HandshakeLine(host, route, handshake, applicationProtocol) + reason);
        }

        if (handshake is not null && log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            CertificatesVerified(handshake);
        }
    }

    private static string HandshakeLine(string host, TlsClientRoute? route, TlsHandshakeEvent? handshake, string? applicationProtocol) =>
        $"handshake with {host} complete: {VersionOf(handshake)}, {CipherSuiteOf(handshake)}, ALPN {applicationProtocol ?? "none"}, route {RouteName(route)}";

    private static string VersionOf(TlsHandshakeEvent? handshake) =>
        handshake is null ? "an unreported version" : handshake.ProtocolVersion.ToString();

    private static string CipherSuiteOf(TlsHandshakeEvent? handshake) =>
        handshake?.CipherSuite is { } cipherSuite ? cipherSuite.ToString() : "an unreported cipher suite";

    private static string RouteName(TlsClientRoute? route) =>
        route is { } known ? known.ToString() : "unreported";

    /// <summary>
    /// Logs, at <c>warning</c>, a certificate <c>--ssl-revoke-best-effort</c> accepted although its
    /// revocation status was offline or unknown (ADR-0222, decision 2; BL-968).
    /// </summary>
    /// <param name="host">The host the handshake was with.</param>
    public void RevocationCheckIncomplete(string host)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Tls, $"certificate of {host} accepted with its revocation status offline or unknown (--ssl-revoke-best-effort)");
        }
    }

    /// <summary>Logs, at <c>info</c>, a connection made through a Unix domain socket (BL-968).</summary>
    /// <param name="path">The socket's path, <see cref="UnixSocketAddress.Path" />.</param>
    public void UnixSocketConnected(string path)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Connect, $"connected to Unix socket {path}");
        }
    }

    /// <summary>Logs, at <c>info</c>, a UDP channel opened to a server (BL-968).</summary>
    /// <param name="serverEndPoint">The address and port the channel sends to.</param>
    public void DatagramChannelOpened(EndPoint serverEndPoint)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Connect, $"UDP channel open to {serverEndPoint}");
        }
    }

    /// <summary>Logs, at <c>error</c>, a connect that failed with its <see cref="CurlExitCode" />.</summary>
    /// <param name="component">The step that failed, one of the <see cref="DiagnosticLogComponents" /> names.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="errorMessage">curl's message.</param>
    public void Failed(string component, CurlExitCode exitCode, string? errorMessage)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, component, string.Create(
                CultureInfo.InvariantCulture,
                $"failed with {exitCode} ({(int)exitCode}): {errorMessage}"));
        }
    }

    /// <summary>Logs, at <c>error</c>, an exception that ends a connect.</summary>
    /// <param name="exception">The exception, which the caller lets escape.</param>
    public void Threw(Exception exception)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Connect, $"failed with {exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, the pool's decision for a connect.</summary>
    /// <param name="target">The target, named by scheme, host and port only.</param>
    /// <param name="reusedConnectionNumber">The idle connection reused, or <see langword="null" /> for a new connection.</param>
    public void PoolDecision(ConnectTarget target, long? reusedConnectionNumber)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"{target.PoolScheme ?? "(unpooled)"}://{target.Host}:{target.Port}");
            var decision = reusedConnectionNumber is { } number
                ? string.Create(CultureInfo.InvariantCulture, $"reusing idle connection #{number}")
                : target.PoolScheme is null ? "new connection: the target is never pooled" : "new connection: no idle connection matches";
            Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect, $"pool {name}: {decision}");
        }
    }

    /// <summary>
    /// Logs, at <c>verbose</c>, that the pool is carrying a transfer on a connection another
    /// transfer is using, on a stream of its own (BL-717).
    /// </summary>
    /// <param name="target">The target the connection was asked for.</param>
    /// <param name="connectionNumber">curl's number for the shared connection.</param>
    public void PoolShare(ConnectTarget target, long connectionNumber) =>
        Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect, string.Create(CultureInfo.InvariantCulture, $"pool {target.PoolScheme}://{target.Host}:{target.Port}: sharing multiplexed connection #{connectionNumber}"));

    /// <summary>Logs, at <c>verbose</c>, a QUIC dial about to run.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <param name="addresses">The addresses tried, in order.</param>
    public void QuicDialling(string host, int port, IReadOnlyList<IPAddress> addresses)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Quic, string.Create(
                CultureInfo.InvariantCulture,
                $"dialling {host}:{port} over QUIC at {JoinAddresses(addresses)}"));
        }
    }

    /// <summary>Logs how a QUIC dial ended: <c>info</c> when connected, <c>error</c> with the exit code when not.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <param name="result">The dial's result.</param>
    public void QuicDialled(string host, int port, MultiplexedConnectResult result)
    {
        if (result.Connection is null)
        {
            Failed(DiagnosticLogComponents.Quic, result.ExitCode, result.ErrorMessage);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Quic, string.Create(CultureInfo.InvariantCulture, $"QUIC connection to {host}:{port} established"));
        }
    }

    private static string ProxyName(ProxyEndpoint proxy) =>
        string.Create(CultureInfo.InvariantCulture, $"{proxy.Kind} proxy {proxy.Host}:{proxy.Port}");

    private static string JoinAddresses(IReadOnlyList<IPAddress> addresses) =>
        string.Join(", ", addresses.Select(address => address.ToString()));

    private void CertificatesVerified(TlsHandshakeEvent handshake)
    {
        foreach (var certificate in handshake.PeerCertificateChain)
        {
            Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Tls, $"certificate subject '{certificate.Subject}', issuer '{certificate.Issuer}'");
        }

        var verdict = handshake.CertificateVerified ? "verified" : "not verified";
        Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Tls, $"certificate chain {verdict}");
    }

    private void Write(DiagnosticLogLevel level, string component, string message) => log.Write(level, component, message);
}
