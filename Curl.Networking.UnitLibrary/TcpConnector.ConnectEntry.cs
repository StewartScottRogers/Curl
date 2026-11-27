using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The messages are curl 8.21.0's, cut to 255 characters as curl cuts them (ADR-0072):
    /// <c>Could not resolve host: &lt;host&gt;</c> for
    /// exit 6, and <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms:
    /// Could not connect to server</c> for exit 7, or exit 28 when the system timed the last dial
    /// out, where <c>n</c> is the time spent
    /// dialing as measured by the injected <see cref="TimeProvider" />. A
    /// <see cref="ConnectTarget.IsForwardProxy" /> target names itself as the proxy, as curl does:
    /// <c>Failed to connect to &lt;proxy host&gt;:&lt;port&gt; over proxy &lt;proxy host&gt; after
    /// &lt;n&gt; ms: ...</c>, for exit 7 and for exit 45. When
    /// <see cref="ConnectTarget.UseTls" /> is set, the <see cref="ITlsProvider" />'s result
    /// is returned as it is, so a failed handshake keeps the exit code and message the
    /// provider chose.
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is an HTTP proxy (<see cref="ProxyKind.Http" />
    /// or <see cref="ProxyKind.Http10" />), the connector resolves and dials the proxy, sends
    /// CONNECT for the target, and applies TLS over the tunnel when
    /// <see cref="ConnectTarget.UseTls" /> is set; the proxy's status code is
    /// <see cref="ConnectResult.ProxyConnectResponseCode" />. The failures are curl 8.21.0's:
    /// exit 5 <c>Could not resolve proxy: &lt;proxy host&gt;</c>, exit 7 <c>Failed to connect
    /// to &lt;host&gt;:&lt;port&gt; over proxy &lt;proxy host&gt; after &lt;n&gt; ms: Could not
    /// connect to server</c>, exit 7 <c>CONNECT tunnel failed, response &lt;code&gt;</c> for a
    /// status outside 200 to 299 (<c>0</c> when the reply is not HTTP), and exit 56
    /// <c>Proxy CONNECT aborted</c> when the proxy closes before its header block ends,
    /// <c>CONNECT response too large</c> for a reply line of 16384 bytes or more, and
    /// <c>Too large response headers: &lt;n&gt; &gt; 307200</c> for a longer header block.
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is an HTTPS proxy (<see cref="ProxyKind.Https" />),
    /// the connector resolves and dials the proxy with the same exit 5 and exit 7 failures,
    /// runs TLS to it through the proxy's <see cref="ITlsProvider" />, verified against the proxy host,
    /// then sends the same CONNECT over that TLS and, when <see cref="ConnectTarget.UseTls" />
    /// is set, runs a second handshake to <see cref="ConnectTarget.Host" /> inside it, as curl
    /// 8.21.0 does. A failed handshake to the proxy or to the target is the provider's result
    /// as it is, and a refused CONNECT fails as through an HTTP proxy.
    /// <see cref="ConnectTimings.TlsHandshakeCompleted" /> and
    /// <see cref="ConnectResult.PeerCertificates" /> are the target's handshake's only, so an
    /// http target has none (measured: <c>%{time_appconnect}</c> is <c>0</c>).
    /// </para>
    /// <para>
    /// When <see cref="ConnectTarget.Proxy" /> is a SOCKS proxy (<see cref="ProxyKind.Socks4" />,
    /// <see cref="ProxyKind.Socks4a" />, <see cref="ProxyKind.Socks5" /> or
    /// <see cref="ProxyKind.Socks5Hostname" />), the connector resolves and dials the proxy the
    /// same way, with the same exit 5 and exit 7 failures, runs curl 8.21.0's handshake for
    /// the kind (<see cref="SocksProxyTunnel" />) and applies TLS over the tunnel when
    /// <see cref="ConnectTarget.UseTls" /> is set. SOCKS4 and SOCKS5 resolve the target
    /// locally, <c>--resolve</c> included, and fail with exit 6 when it does not resolve; a
    /// handshake the proxy refuses or cuts short fails with exit 97
    /// (<see cref="CurlExitCode.Proxy" />) and curl's message. The proxy connection is
    /// disposed on every failure. <see cref="ConnectResult.ProxyConnectResponseCode" /> is
    /// <c>0</c>.
    /// </para>
    /// <para>
    /// With a <see cref="PreProxy" />, an HTTP or HTTPS <see cref="ConnectTarget.Proxy" /> and an
    /// <see cref="ConnectTarget.IsForwardProxy" /> target are reached through it, as curl 8.21.0
    /// does for <c>--preproxy</c> (measured, BL-614): the pre-proxy is resolved (exit 5
    /// <c>Could not resolve proxy: &lt;pre-proxy&gt;</c>) and dialled (exit 7 <c>Failed to connect
    /// to &lt;proxy&gt;:&lt;port&gt; over proxy &lt;pre-proxy&gt; after &lt;n&gt; ms: ...</c>), its SOCKS
    /// handshake opens a tunnel to the HTTP proxy with the SOCKS failures above, and the CONNECT,
    /// the HTTPS proxy's TLS or the forwarded request then run inside that tunnel.
    /// </para>
    /// <para>
    /// Before anything is resolved, a <c>--resolve</c> entry that did not parse, or a
    /// <c>--connect-to</c> mapping that matches the target and whose destination does not
    /// parse, fails with exit 49 and curl 8.21.0's message (see <see cref="ResolveOverrides" />
    /// and <see cref="ConnectToMappings" />). A matching mapping's host and port are resolved,
    /// dialled and, through an HTTP proxy, named in the CONNECT request; exit 6 then names the
    /// mapped host and exit 7 reads <c>Failed to connect to &lt;host&gt;:&lt;port&gt; via
    /// &lt;mapped host&gt;:&lt;mapped port&gt; after &lt;n&gt; ms: Could not connect to
    /// server</c> (measured). When no mapping matches, a <see cref="ConnectTarget.AltSvcRoute" />
    /// is dialled the same way, after curl's <c>Alt-svc connecting from</c> line. TLS still
    /// verifies <see cref="ConnectTarget.Host" />. A
    /// <c>--resolve</c> entry answers for the host and port being resolved, the proxy's
    /// included, in place of the <see cref="IDnsResolver" />.
    /// </para>
    /// <para>
    /// The resolve, dials, any tunnel and any TLS handshake run under one connect timeout
    /// (<c>--connect-timeout</c>, or 300 seconds) on the injected <see cref="TimeProvider" />,
    /// counted from when this method begins. When it passes, the connect fails with exit 28
    /// (<see cref="CurlExitCode.OperationTimedOut" />) and curl 8.21.0's <c>Connection timed out
    /// after &lt;n&gt; milliseconds</c>, for every scheme (measured, ADR-0117, BL-510).
    /// </para>
    /// <para>
    /// A success carries <see cref="ConnectResult.Timings" />, taken from the injected
    /// <see cref="TimeProvider" />: <see cref="ConnectTimings.Started" /> when this method
    /// begins, <see cref="ConnectTimings.NameResolved" /> when the host (or proxy) has
    /// resolved, <see cref="ConnectTimings.Connected" /> when the TCP connect completes or,
    /// through a proxy, when the tunnel is open, and <see cref="ConnectTimings.TlsHandshakeCompleted" />
    /// from the <see cref="ITlsProvider" />'s own timings, or when it returned if it recorded
    /// none. <see cref="ConnectResult.LocalEndPoint" /> is the local end point
    /// <see cref="ITcpDialer" /> reports; the remote end point is the connection's
    /// <see cref="IConnection.RemoteEndPoint" />. <see cref="ConnectResult.PeerCertificates" />
    /// are the <see cref="ITlsProvider" />'s, and empty without TLS.
    /// </para>
    /// <para>
    /// A dial that reached no address, to the host or to the proxy, carries
    /// <see cref="ConnectTimings.Started" /> and <see cref="ConnectTimings.NameResolved" /> with
    /// <see cref="ConnectTimings.Connected" /> <see langword="null" />, as curl 8.21.0 reports
    /// <c>%{time_namelookup}</c> but a <c>%{time_connect}</c> of <c>0</c> after a refused
    /// connect (measured, ADR-0091). A failed resolve, a bad <c>--resolve</c> or <c>--connect-to</c>
    /// entry and a failed tunnel carry none; a failed handshake is the provider's result as it is.
    /// </para>
    /// <para>
    /// On <see cref="ConnectTarget.Events" /> it reports curl 8.21.0's <c>-v</c> lines for the
    /// connect (ADR-0100): <c>  Trying &lt;address&gt;:&lt;port&gt;...</c> before each dial,
    /// <c>connect to &lt;address&gt; port &lt;port&gt; from 0.0.0.0 port 0 failed:
    /// &lt;reason&gt;</c> after each dial that fails (<see cref="ConnectFailureReason" />), and
    /// the exit 7 message when none reached. A success is reported through
    /// <see cref="ITransferEvents.ReportConnectionOpened" /> once the connection is ready, after
    /// any tunnel and TLS handshake, naming the host dialled and numbering the connections this
    /// connector opened from <c>0</c> (<see cref="ConnectResult.ConnectionNumber" />). A connect
    /// that fails after its options parse takes the next number too, as curl 8.21.0 numbers the
    /// connection it tried (ADR-0109). A host and port answered by a <c>--resolve</c> entry, or
    /// resolved before through its <see cref="DnsCache" /> (to the host, the proxy or a SOCKS target), are
    /// answered from that cache and reported first as <c>Hostname &lt;host&gt; was found in DNS
    /// cache</c>, as curl 8.21.0 reports them (measured, BL-481). Every answer, cached or
    /// looked up, is then reported as <c>Host &lt;name&gt;:&lt;port&gt; was resolved.</c>,
    /// <c>IPv6: &lt;addresses&gt;</c> and <c>IPv4: &lt;addresses&gt;</c>, naming the host as it
    /// was cached (a <c>--resolve</c> entry's as written, <c>*</c> for a wildcard) and each
    /// family's addresses joined by <c>, </c> or <c>(none)</c>; a name that is an IP address
    /// reports none of the three (measured, BL-482). Before anything else, a connect that no
    /// transfer has preceded with <see cref="LoadResolveEntries" /> loads the <c>--resolve</c>
    /// entries itself, with their <c>Added</c> lines, before a bad entry fails it.
    /// </para>
    /// <para>
    /// Under <c>-4</c> or <c>-6</c> (the constructor's address family) a host or proxy name is
    /// dialled at that family's addresses only, and one with none fails as not resolved: exit 6
    /// <c>Could not resolve host: &lt;host&gt;</c>, or exit 5 for a proxy. An IP address literal
    /// is dialled as written, as curl 8.21.0 dials it (measured, BL-500).
    /// </para>
    /// <para>
    /// A resolver that says why a name did not resolve (<see cref="IDnsResolverWithFailureReason" />,
    /// the <c>--dns-servers</c> client) adds the reason in brackets, <c>Could not resolve host:
    /// &lt;host&gt; (Domain name not found)</c>, and a c-ares option that did not parse is exit 43
    /// <c>Error 43 resolving &lt;host&gt;:&lt;port&gt;</c>, as curl's c-ares build reports them
    /// (<see cref="NameResolutionFailure" />, BL-694).
    /// </para>
    /// </remarks>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ConnectWithinAsync(target, _connectTimeout, keepsTargetPort: false, cancellationToken);

    /// <summary>
    /// Gives FTP's passive data connection's connector: it connects as this one does, through the
    /// same DNS cache, <c>--resolve</c> entries and connection numbering, but holds no connect to
    /// <c>--connect-timeout</c>, so only the caller's cancellation, or the system giving up on a
    /// dial, ends it (measured, BL-797 Notes), and takes a matching <c>--connect-to</c> mapping's
    /// host but keeps the target's port, the <c>EPSV</c> or <c>PASV</c> reply's, as curl 8.21.0's
    /// <c>ftp.c</c> dials the data connection through <c>via_peer2</c>, the control connection's
    /// mapped host with the new port (upstream test713, BL-1976, BL-2042).
    /// </summary>
    /// <returns>The connector.</returns>
    public IConnector ForFtpDataConnections() => new FtpDataConnector(this);

    private async ValueTask<ConnectResult> ConnectWithinAsync(ConnectTarget target, TimeSpan connectLimit, bool keepsTargetPort, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Every failure, and every exception that escapes, is written to the diagnostic log at
        // error (BL-920).
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        try
        {
            var result = await ConnectAndNumberAsync(target, connectLimit, keepsTargetPort, cancellationToken).ConfigureAwait(false);
            if (result.Connection is null)
            {
                log.Failed(DiagnosticLogComponents.Connect, result.ExitCode, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception exception)
        {
            log.Threw(exception);
            throw;
        }
    }

    private async ValueTask<ConnectResult> ConnectAndNumberAsync(ConnectTarget target, TimeSpan connectLimit, bool keepsTargetPort, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = keepsTargetPort
            ? DestinationOf(target) with { Port = target.Port }
            : DestinationOf(target);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return ConnectResult.Failed(_resolveOverrides.ParseFailureCodeWith(destination), parseError);
        }

        var result = await ConnectRedialingByHandAsync(target, destination, started, connectLimit, cancellationToken).ConfigureAwait(false);

        return result.Connection is null
            ? NumberedConnectFailure.Of(result, TakeConnectionNumber())
            : result;
    }

    /// <summary>
    /// Gives where a connection to <paramref name="target" /> goes: the <c>--connect-to</c>
    /// mapping that matches it, else its <see cref="ConnectTarget.AltSvcRoute" />'s alternative,
    /// reported first as curl 8.21.0's <c>Alt-svc connecting from [&lt;id&gt;]&lt;host&gt;:&lt;port&gt;
    /// to [&lt;id&gt;]&lt;host&gt;:&lt;port&gt;</c> (measured, BL-623 Notes), else the target itself.
    /// An alternative counts as mapped, so exit 7 names it after <c>via</c>, as curl's does.
    /// The line is reported once per target object: <c>--http3</c> races QUIC and TCP with one
    /// target, and curl 8.18.0's ngtcp2 build reports it once for both attempts (measured, BL-733
    /// Notes, BL-949). A <see cref="ConnectTarget.IsForwardProxy" /> target names the proxy, which
    /// <c>--connect-to</c> never maps, as curl 8.21.0 maps only the origin (upstream test 2050).
    /// </summary>
    private ConnectDestination DestinationOf(ConnectTarget target)
    {
        var mapped = target.IsForwardProxy
            ? new ConnectDestination(target.Host, target.Port, IsMapped: false, ParseError: null)
            : _connectToMappings.Map(target.Host, target.Port);
        if (mapped.IsMapped || mapped.ParseError is not null || target.AltSvcRoute is not { } route)
        {
            return mapped;
        }

        var alternative = route.Alternative;
        if (_altSvcReported.TryAdd(target, target))
        {
            target.Events.ReportInfo(
                $"Alt-svc connecting from [{route.OriginAlpn}]{target.Host}:{target.Port} to [{alternative.Alpn}]{alternative.Host}:{alternative.Port}");
        }

        return new ConnectDestination(alternative.Host, alternative.Port, IsMapped: true, ParseError: null);
    }

    private long TakeConnectionNumber() => Interlocked.Increment(ref _nextConnectionNumber) - 1;

    /// <summary>
    /// <see cref="ForFtpDataConnections" />'s connector: the owner's connect to the target as
    /// given but for a <c>--connect-to</c> mapping's host, under the longest delay a .NET timer takes,
    /// about 49.7 days, in place of the connect timeout.
    /// </summary>
    private sealed class FtpDataConnector(TcpConnector owner) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            owner.ConnectWithinAsync(target, LongestTimerDelay, keepsTargetPort: true, cancellationToken);
    }
}
