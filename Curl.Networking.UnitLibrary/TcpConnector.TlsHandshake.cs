using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    private async ValueTask<ConnectResult> SecureWhenAskedAsync(
        DialedSocket dialed,
        ConnectTarget target,
        ConnectTimings timings,
        int proxyConnectResponseCode,
        CancellationToken cancellationToken,
        bool writesSslFilterAdded = false)
    {
        if (haproxyProtocol is { } header)
        {
            await WriteHaproxyLineAsync(header, dialed, target.Events, cancellationToken).ConfigureAwait(false);
        }

        if (!target.UseTls)
        {
            return OpenedInPlaintext(dialed, target, timings, proxyConnectResponseCode);
        }

        var secured = await AuthenticateTracingRecordsAsync(dialed, target, writesSslFilterAdded, cancellationToken).ConfigureAwait(false);
        if (secured.Connection is not { } securedConnection)
        {
            return secured;
        }

        // A provider that measured its handshake is trusted for the moment it completed; for
        // one that did not, the moment it returned is that moment.
        var handshakeCompleted = secured.Timings?.TlsHandshakeCompleted ?? timeProvider.GetTimestamp();
        var opened = WithSetupFiltersRemoved(
            Opened(
                dialed,
                target.Events,
                securedConnection,
                timings with { TlsHandshakeCompleted = handshakeCompleted },
                proxyConnectResponseCode,
                secured.PeerCertificates,
                secured.ApplicationProtocol),
            dialed,
            target.Events);
        WriteSslQueryAlpnLines(target, secured.ApplicationProtocol);
        return opened;
    }

    // Runs the target's handshake over the dialled connection, traced as TLS records when
    // TlsRecordTraceFor says so, the trace moving to the application data's lines once it succeeds.
    private async ValueTask<ConnectResult> AuthenticateTracingRecordsAsync(DialedSocket dialed, ConnectTarget target, bool writesSslFilterAdded, CancellationToken cancellationToken)
    {
        var recordTrace = TlsRecordTraceFor(dialed.Connection, dialed.TracesTcpFilter, target);
        var secured = await AuthenticateTargetAsync(recordTrace ?? dialed.Connection, target, writesSslFilterAdded, cancellationToken).ConfigureAwait(false);
        if (secured.Connection is not null)
        {
            recordTrace?.Lines = TcpIoTraceConnection.HttpsApplicationDataLines;
        }

        return secured;
    }

    // A forward proxy is the target itself, so its handshake runs through the proxy's
    // provider, as curl 8.21.0 verifies it with --proxy-insecure and not -k (measured, BL-441),
    // and is reported as a proxy's, as curl's OpenSSL build says Proxy certificate: for it
    // (measured, BL-405).
    // Under writesSslFilterAdded the setup filter first reports adding the SSL filter, after any
    // HAPROXY filter and before the handshake's own lines (measured for a direct https:// connect,
    // BL-1192 Notes). The SSL filter's own lines go around the handshake (measured, BL-1287 Notes).
    private async ValueTask<ConnectResult> AuthenticateTargetAsync(
        IConnection plaintext,
        ConnectTarget target,
        bool writesSslFilterAdded,
        CancellationToken cancellationToken)
    {
        if (writesSslFilterAdded)
        {
            target.Events.ReportInfo(SslFilterAddedLine);
        }

        var sslTrace = SslTraceFor(target);
        sslTrace.ReportConnecting(target.Events);
        var secured = await AuthenticateTargetAsync(plaintext, target, cancellationToken).ConfigureAwait(false);
        sslTrace.ReportFinished(target.Events, secured);
        return secured;
    }

    private ValueTask<ConnectResult> AuthenticateTargetAsync(
        IConnection plaintext,
        ConnectTarget target,
        CancellationToken cancellationToken) =>
        AuthenticateAsync(
            TrustedAs(target, target.IsForwardProxy ? _proxyTlsProvider : tlsProvider),
            plaintext,
            target.TlsServerName ?? target.Host,
            target,
            target.IsForwardProxy,
            ApplicationProtocolsFor(target, HttpOverTlsApplicationProtocols),
            cancellationToken);

    /// <summary>
    /// Returns the provider whose trust <paramref name="target" /> asks for: one trusting only the
    /// system store when <see cref="ConnectTarget.TrustsSystemStoreOnly" /> is set and the provider
    /// can make one, otherwise <paramref name="provider" /> itself (CGR-289, ADR-0979).
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <param name="provider">The provider the target's handshake would otherwise run through.</param>
    /// <returns>The provider to run the handshake through.</returns>
    internal static ITlsProvider TrustedAs(ConnectTarget target, ITlsProvider provider) =>
        target.TrustsSystemStoreOnly && provider is ISystemStoreTrustTlsProvider systemStoreTrust
            ? systemStoreTrust.WithSystemStoreOnlyTrust()
            : provider;

    /// <summary>
    /// Returns the protocols the handshake with <paramref name="target" /> offers through ALPN:
    /// <paramref name="httpOverTls" /> for HTTP over TLS to the origin, the one target the HTTP
    /// handler pools as <c>https</c> (BL-490, ADR-0141), unless the target names its own
    /// <see cref="ConnectTarget.ApplicationProtocols" />, as a connect to an Alt-Svc alternative of
    /// another HTTP version does (BL-948); <c>http/1.1</c> alone for an HTTPS forward proxy, whatever
    /// the HTTP version options say, as both curl 8.21.0 builds offer it (measured, BL-753,
    /// ADR-0190); nothing for any other protocol, which curl offers no ALPN.
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <param name="httpOverTls">What HTTP over TLS to the origin offers.</param>
    /// <returns>The protocols, in preference order; empty to offer none.</returns>
    internal static IReadOnlyList<string> ApplicationProtocolsFor(ConnectTarget target, IReadOnlyList<string> httpOverTls) =>
        target.IsForwardProxy
            ? HttpApplicationProtocols.Http11Only
            : string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase)
                ? target.ApplicationProtocols ?? httpOverTls
                : [];

    // A provider that cannot finish a handshake itself runs the next one with the host on the
    // hand-built client: its failed handshake unwinds the connect, which dials again (ADR-1459).
    private static async ValueTask<ConnectResult> AuthenticateAsync(
        ITlsProvider provider,
        IConnection plaintext,
        string host,
        ConnectTarget target,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken)
    {
        if (provider is not ITlsProviderWithHandBuiltRedial redialing)
        {
            return await AuthenticateOnceAsync(provider, plaintext, host, target, isProxy, applicationProtocols, cancellationToken).ConfigureAwait(false);
        }

        var secured = await AuthenticateOnceAsync(redialing.ProviderFor(host), plaintext, host, target, isProxy, applicationProtocols, cancellationToken).ConfigureAwait(false);
        return redialing.AsksForRedial(secured) ? throw new HandBuiltRedialRequiredException(isProxy) : secured;
    }

    // A provider that can report its handshake reports its trust and handshake on the
    // target's events, marked as the proxy's when it is with an HTTPS proxy (BL-404, BL-452),
    // and offers the target's application protocols through ALPN (BL-490).
    // Either way the handshake's outcome goes to the diagnostic log (BL-920): the events are
    // wrapped to catch the reported handshake only when info is enabled, so with the log off the
    // provider receives the target's own events.
    private static async ValueTask<ConnectResult> AuthenticateOnceAsync(
        ITlsProvider provider,
        IConnection plaintext,
        string host,
        ConnectTarget target,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken)
    {
        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        if (provider is not IHandshakeReportingTlsProvider reportingProvider)
        {
            var unreported = await provider.AuthenticateAsClientAsync(plaintext, host, cancellationToken).ConfigureAwait(false);
            if (!LoggedFailure(log, unreported))
            {
                log.HandshakeCompleted(host, null, null, null, unreported.ApplicationProtocol);
            }

            return unreported;
        }

        var capturing = log.IsEnabled(DiagnosticLogLevel.Warning) ? new HandshakeCapturingTransferEvents(target.Events) : null;
        var secured = await reportingProvider.AuthenticateAsClientAsync(plaintext, host, capturing ?? target.Events, isProxy, applicationProtocols, target.DiagnosticLog, cancellationToken).ConfigureAwait(false);
        if (!LoggedFailure(log, secured))
        {
            LogReportedHandshake(log, host, reportingProvider, capturing, secured);
        }

        return secured;
    }

    private static bool LoggedFailure(NetworkDiagnosticLog log, ConnectResult secured)
    {
        if (secured.Connection is not null)
        {
            return false;
        }

        log.Failed(DiagnosticLogComponents.Tls, secured.ExitCode, secured.ErrorMessage);
        return true;
    }

    // A best-effort revocation acceptance is a warning before the handshake's info line, which
    // names why the options chose the provider (BL-968).
    private static void LogReportedHandshake(NetworkDiagnosticLog log, string host, IHandshakeReportingTlsProvider provider, HandshakeCapturingTransferEvents? capturing, ConnectResult secured)
    {
        if (capturing is { RevocationCheckIncomplete: true })
        {
            log.RevocationCheckIncomplete(host);
        }

        log.HandshakeCompleted(host, provider.Route, provider.RouteReason, capturing?.Handshake, secured.ApplicationProtocol);
    }
}
