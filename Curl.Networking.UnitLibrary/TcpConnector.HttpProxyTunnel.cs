using System.Runtime.ExceptionServices;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Opens the tunnel through a plain HTTP proxy after saying no ALPN was negotiated, as both
    /// builds of curl 8.21.0 do on every dial, before the CONNECT's lines (measured, BL-863, BL-1145).
    /// </summary>
    /// <remarks>
    /// The setup filter's line adding the tunnel filter and the tunnel's
    /// <see cref="HttpProxyTunnelTrace" /> lines go around it, each when traced (measured, BL-1193 Notes).
    /// </remarks>
    private ValueTask<(ConnectResult? Result, string? RedialAuthorization)> OpenPlainTunnelAsync(
        DialedSocket dialed,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var events = tunnel.Target.Events;
        var trace = new HttpProxyTunnelTrace(TracesHttpProxyFilter, TracesH1ProxyFilter);
        if (TracesSetupFilter)
        {
            events.ReportInfo(HttpProxyTunnelFilterAddedLine);
        }

        trace.ReportConnecting(events);
        events.ReportInfo(ConnectTunnelVerboseLines.NoAlpnNegotiated);
        trace.ReportSubfilterInstalled(events);
        return OpenTunnelAsync(dialed with { TunnelTrace = trace }, trace, tunnel, proxyAuthorization, cancellationToken);
    }

    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> OpenTunnelOverTlsAsync(
        DialedSocket dialed,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        // The setup filter adds the proxy's SSL filter and the tunnel filter once the socket
        // connected, and the HTTP proxy filter polls its CONNECT before, during and after the
        // proxy's handshake (measured, BL-1255 Notes; the poll rounds fixed, ADR-0357).
        var events = tunnel.Target.Events;
        var trace = new HttpProxyTunnelTrace(TracesHttpProxyFilter, TracesH1ProxyFilter);
        if (TracesSetupFilter)
        {
            events.ReportInfo(HttpsProxySslFilterAddedLine);
            events.ReportInfo(HttpProxyTunnelFilterAddedLine);
        }

        trace.ReportConnecting(events);
        var sslTrace = new SslFilterTrace(SslProxyFilterName, TracesSslProxyFilter, trace);
        sslTrace.ReportConnecting(events);

        // curl 8.21.0 verifies the proxy against its own host name and reports a failed
        // handshake to it with the same exit code and message as one to a target (measured).
        // It verifies with the --proxy-* TLS options, not -k or --cacert, so the proxy's own
        // provider runs this handshake (ADR-0061). It offers http/1.1 through ALPN whatever the
        // HTTP version options say, and none under --no-alpn (measured on both builds, ADR-0190);
        // under --proxy-http2 it offers h2,http/1.1 (measured, BL-1413 Notes, ADR-0408).
        var proxyApplicationProtocols = _proxyTunnelOptions.ProxyHttp2 ? HttpApplicationProtocols.H2ThenHttp11 : HttpApplicationProtocols.Http11Only;
        var securedProxy = await AuthenticateAsync(_proxyTlsProvider, dialed.Connection, tunnel.Proxy.Host, tunnel.Target, isProxy: true, applicationProtocols: proxyApplicationProtocols, cancellationToken).ConfigureAwait(false);
        sslTrace.ReportFinished(events, securedProxy);
        if (securedProxy.Connection is not { } proxyConnection)
        {
            return (securedProxy, null);
        }

        // Both builds say what the proxy's ALPN agreed before the CONNECT, with and without
        // --no-alpn (measured, BL-872); the proxy's SSL filter answers the tunnel's ALPN query first
        // (measured, BL-1287 Notes).
        sslTrace.ReportAlpnQueried(events, securedProxy.ApplicationProtocol);
        events.ReportInfo(securedProxy.ApplicationProtocol is { } agreed
            ? $"CONNECT: '{agreed}' negotiated"
            : ConnectTunnelVerboseLines.NoAlpnNegotiated);
        if (securedProxy.ApplicationProtocol == Http2ApplicationProtocol)
        {
            return (await OpenHttp2TunnelAsync(dialed with { Connection = proxyConnection }, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false), null);
        }

        trace.ReportSubfilterInstalled(events);

        // CONNECT and the target's TLS run over the proxy's TLS; the socket's local end point stays.
        var securedDialed = dialed with { Connection = proxyConnection, TunnelTrace = trace };
        return await OpenTunnelAsync(securedDialed, trace, tunnel, proxyAuthorization, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends CONNECT with <paramref name="proxyAuthorization" /> and, when the proxy answers
    /// <c>407</c> and the authenticator answers its challenge, sends it again with that answer,
    /// as curl 8.21.0 does (BL-602): on this connection when the reply leaves it reusable, else
    /// by returning the answer for a new one. A <c>407</c> to a CONNECT that sent a credential
    /// is answered only by a handshake of more than one leg - NTLM's Type 3 for its Type 2, a
    /// Negotiate context's next token (BL-604, ADR-0270) - and is else the tunnel's failure.
    /// </summary>
    private async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> OpenTunnelAsync(
        DialedSocket dialed,
        HttpProxyTunnelTrace trace,
        TunnelRequest tunnel,
        string? proxyAuthorization,
        CancellationToken cancellationToken)
    {
        var connection = dialed.Connection;
        var log = new NetworkDiagnosticLog(tunnel.Target.DiagnosticLog);
        var answersChallenge = tunnel.AuthorizationAnswersChallenge;
        ConnectTunnelVerboseLines.ReportNewProxyConnection(tunnel.Target.Events, _proxyTunnelOptions.MatchesSchannelBuild);
        while (true)
        {
            log.TunnelRequested(tunnel.Proxy, tunnel.Destination.Host, tunnel.Destination.Port);
            var (reply, exception) = await RequestTunnelAsync(connection, tunnel, trace, proxyAuthorization, answersChallenge, cancellationToken).ConfigureAwait(false);
            if (exception is null && reply.OpensTunnel)
            {
                return (WithConnectReplyHeaders(await SecureOpenedTunnelAsync(dialed, trace, tunnel, reply.StatusCode, proxyAuthorization, cancellationToken).ConfigureAwait(false), reply.HeaderCount, tunnel.ReplyHeadBytes.Value), null);
            }

            var (answer, onThisConnection, failure) = exception is null
                ? await AnswerProxyChallengeAsync(connection, reply, tunnel, proxyAuthorization, answersChallenge, cancellationToken).ConfigureAwait(false)
                : (null, false, null);
            if (failure is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return (failure, null);
            }

            if (!onThisConnection)
            {
                return await CloseUnopenedTunnelAsync(connection, reply, proxyAuthorization, exception, answer, tunnel.AuthenticatorEvents.FirstSspiFailure, tunnel.Target.Events, tunnel.ReplyHeadBytes.Value, tunnel.LastStatusReply.Value).ConfigureAwait(false);
            }

            proxyAuthorization = answer;
            answersChallenge = true;
        }
    }

    /// <summary>
    /// Disposes the proxy connection whether CONNECT failed, could not be sent or read, or is
    /// to be sent again on a new connection; then rethrows, or returns the tunnel's failure, or
    /// <paramref name="redialAuthorization" /> for the new connection. The failure's message is
    /// <paramref name="firstSspiFailure" /> when the authenticator reported one, as curl
    /// 8.21.0's error buffer keeps the transfer's first <c>failf</c> (BL-1033).
    /// </summary>
    private static async ValueTask<(ConnectResult? Result, string? RedialAuthorization)> CloseUnopenedTunnelAsync(
        IConnection connection,
        HttpProxyTunnelReply reply,
        string? proxyAuthorization,
        ExceptionDispatchInfo? exception,
        string? redialAuthorization,
        string? firstSspiFailure,
        ITransferEvents events,
        long headBytes,
        (int StatusCode, int HeaderCount) lastStatusReply)
    {
        await connection.DisposeAsync().ConfigureAwait(false);
        exception?.Throw();
        return redialAuthorization is null ? (TunnelFailure(reply, proxyAuthorization, firstSspiFailure, events, headBytes, lastStatusReply), null) : (null, redialAuthorization);
    }

    /// <summary>
    /// Sends one CONNECT and reads the proxy's reply, whose complete head goes to the
    /// transfer's header output when its events take it (<see cref="IConnectReplyHeadWritingEvents" />),
    /// whatever the status, as curl 8.21.0 writes a <c>407</c>'s and a <c>403</c>'s too (BL-613 Notes).
    /// The CONNECT's <c>-v</c> lines, its request head and the reply's header lines go to the
    /// target's events as <see cref="ConnectTunnelVerboseLines" /> words them (BL-863).
    /// </summary>
    private async ValueTask<(HttpProxyTunnelReply Reply, ExceptionDispatchInfo? Exception)> RequestTunnelAsync(
        IConnection connection,
        TunnelRequest tunnel,
        HttpProxyTunnelTrace trace,
        string? proxyAuthorization,
        bool answersChallenge,
        CancellationToken cancellationToken)
    {
        try
        {
            var destination = tunnel.Destination;
            var events = tunnel.Target.Events;
            ConnectTunnelVerboseLines.ReportBeforeConnect(events, ProxyAuthRequestOf(destination, tunnel.Proxy), proxyAuthorization, answersChallenge);
            trace.ReportSending(events);
            var request = HttpProxyTunnel.BuildConnectRequest(destination.Host, destination.Port, tunnel.Proxy, _proxyTunnelOptions, proxyAuthorization);
            events.ReportRequestHeader(request);
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            trace.ReportReceiving(events);
            var reply = await HttpProxyTunnel.ReadReplyAsync(connection, cancellationToken).ConfigureAwait(false);
            tunnel.ReplyHeadBytes.Value += reply.Head.Length;
            if (reply.StatusCode > 0)
            {
                tunnel.LastStatusReply.Value = (reply.StatusCode, reply.HeaderCount);
            }

            ConnectTunnelVerboseLines.ReportReplyHead(events, reply.Head.Span, reply.StatusCode, proxyAuthorization, DigestStaleChallenge.IsOfferedIn(reply.ProxyAuthenticate));
            ConnectTunnelVerboseLines.ReportReplyFailure(events, reply);
            trace.ReportResponse(events, reply.OpensTunnel);
            if (tunnel.HeadOutput is { } headOutput && !reply.Head.IsEmpty)
            {
                await headOutput.WriteConnectReplyHeadAsync(reply.Head, cancellationToken).ConfigureAwait(false);
            }

            return (reply, null);
        }
        catch (Exception exception)
        {
            return (default, ExceptionDispatchInfo.Capture(exception));
        }
    }

    /// <summary>
    /// Gives an opened tunnel's result carrying how many header lines the proxy's opening reply
    /// held, which curl 8.21.0 counts toward its 5000 response headers (measured, BL-1609 Notes),
    /// and how many bytes every reply head to the tunnel's CONNECTs held, which it adds to
    /// <c>%{size_header}</c> (BL-2010); a failed result, such as a refused TLS handshake inside
    /// the tunnel, as it is.
    /// </summary>
    private static ConnectResult WithConnectReplyHeaders(ConnectResult opened, int headerCount, long headBytes) =>
        opened.Connection is { } connection
            ? ConnectResult.Connected(
                connection,
                opened.Timings,
                opened.LocalEndPoint,
                opened.ProxyConnectResponseCode,
                opened.PeerCertificates,
                opened.IsReused,
                opened.ConnectionNumber,
                opened.ApplicationProtocol,
                opened.UnixSocketPath,
                opened.MappedHost,
                opened.MappedPort,
                headerCount,
                headBytes,
                lastSocketError: opened.LastSocketError)
            : opened;
}
