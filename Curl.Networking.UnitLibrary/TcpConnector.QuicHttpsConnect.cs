using System.Runtime.CompilerServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>[SETUP]</c> and <c>[HTTPS-CONNECT]</c> lines of a direct QUIC connect to an <c>https://</c>
/// origin, and of the TCP attempt <c>--http3</c> starts after it, as curl.se's curl 8.22.0 ngtcp2
/// build writes them (measured, BL-1284 Notes; ADR-0357's BL-1284 amendment).
/// </summary>
public sealed partial class TcpConnector
{
    private readonly ConditionalWeakTable<ConnectTarget, QuicHttpsConnectAttempt> _quicHttpsConnectAttempts = new();

    /// <summary>
    /// Gets the HTTP version the <c>[HTTPS-CONNECT] 2nd attempt uses &lt;version&gt; from wanted
    /// versions</c> line names under <see cref="TracesHttpsConnectFilter" /> for a QUIC connect that a
    /// TCP attempt may follow: <c>h2</c> under <c>--http3</c>, <see langword="null" /> (no line, and
    /// a failed QUIC connect is the filter's failure) under <c>--http3-only</c> and every other version.
    /// </summary>
    public string? HttpsConnectSecondAttemptVersion { get; init; }

    // Under TracesSetupFilter or TracesHttpsConnectFilter a direct QUIC connect reports through the
    // [SETUP] and [HTTPS-CONNECT] filters as a TCP one does; no [DNS] filter goes between them, since
    // curl's QUIC filter lines were not measured with it. With a second attempt to come, the filter's
    // state is kept on the target for the TCP attempt the race starts on the same target. Through a
    // CONNECT-UDP proxy the setup filter eyeballs to the proxy, as curl 8.22.0 does (BL-1320 Notes).
    private (ConnectTarget Target, HttpsConnectFilterTraceEvents? HttpsConnect, QuicHttpsConnectAttempt? Attempt) QuicConnectionFilters(ConnectTarget target, ProxyEndpoint? udpTunnelProxy = null)
    {
        var httpsOrigin = IsHttpsOrigin(target);
        if (!TracesSetupFilter && !TracesHttpsConnectFor(httpsOrigin))
        {
            return (target, null, null);
        }

        var (events, httpsConnect) = SetupAndDnsFilterEvents(
            target.Events,
            httpsOrigin,
            TracesSetupFilter,
            static below => below,
            QuicSetupFilterStarter(DestinationOf(target), udpTunnelProxy),
            secondAttemptVersion: HttpsConnectSecondAttemptVersion);
        var traced = target with { Events = events };

        // DestinationOf writes the Alt-svc line once per target; the traced copy is the same target.
        _altSvcReported.TryAdd(traced, traced);
        if (httpsConnect is null || HttpsConnectSecondAttemptVersion is null)
        {
            return (traced, httpsConnect, null);
        }

        var attempt = new QuicHttpsConnectAttempt();
        _quicHttpsConnectAttempts.AddOrUpdate(target, attempt);
        return (traced, httpsConnect, attempt);
    }

    // The setup filter of a QUIC connect: eyeballing to the origin, or to the CONNECT-UDP proxy.
    private static Func<ITransferEvents, ITransferEvents> QuicSetupFilterStarter(ConnectDestination destination, ProxyEndpoint? udpTunnelProxy) =>
        udpTunnelProxy is null
            ? below => new SetupFilterTraceEvents(below, destination.Host, destination.Port)
            : below => new SetupFilterTraceEvents(below, udpTunnelProxy.Host, udpTunnelProxy.Port, SetupFilterTraceEvents.ToProxy);

    // The [HTTPS-CONNECT] filter over the given events, going on from the QUIC attempt's when there is one.
    private HttpsConnectFilterTraceEvents HttpsConnectFilterOver(ITransferEvents events, bool tracesSetup, string? secondAttemptVersion, QuicHttpsConnectAttempt? quicAttempt)
    {
        var httpsConnect = new HttpsConnectFilterTraceEvents(events, HttpsConnectFirstAttemptVersion, secondAttemptVersion);
        if (quicAttempt is not null)
        {
            httpsConnect.ContinueAfterQuicAttempt(quicAttempt.Failure, HappyEyeballsTimeout, tracesSetup);
        }

        return httpsConnect;
    }

    // A failed QUIC connect: the filter's failure lines under --http3-only, or, with a TCP attempt to
    // follow, the failure kept for it to report (measured, BL-1284 Notes).
    private static void ReportQuicHttpsConnectFailure(HttpsConnectFilterTraceEvents? httpsConnect, QuicHttpsConnectAttempt? attempt, CurlExitCode exitCode)
    {
        if (attempt is not null)
        {
            attempt.Fail(exitCode);
            return;
        }

        httpsConnect?.ReportConnectFailed(exitCode);
    }

    // The QUIC attempt a TCP connect to the same target follows, taken once.
    private QuicHttpsConnectAttempt? TakeQuicHttpsConnectAttempt(ConnectTarget target) =>
        _quicHttpsConnectAttempts.TryGetValue(target, out var attempt) && _quicHttpsConnectAttempts.Remove(target) ? attempt : null;

    // What the TCP attempt needs of the QUIC one: its exit code once it failed, written by the QUIC
    // connect and read by the TCP one, which may run beside it.
    private sealed class QuicHttpsConnectAttempt
    {
        private readonly Lock _gate = new();
        private CurlExitCode? _failure;

        public CurlExitCode? Failure
        {
            get
            {
                lock (_gate)
                {
                    return _failure;
                }
            }
        }

        public void Fail(CurlExitCode exitCode)
        {
            lock (_gate)
            {
                _failure = exitCode;
            }
        }
    }
}
