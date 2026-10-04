using System.Runtime.CompilerServices;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>[SETUP]</c> and <c>[HTTPS-CONNECT]</c> lines of a direct QUIC connect to an <c>https://</c>
/// origin, and of the TCP attempt <c>--http3</c> starts after it, as curl.se's curl 8.22.0 ngtcp2
/// build writes them (measured, BL-1284 Notes; ADR-0357's BL-1284 amendment), and of the race an
/// <c>--alt-svc</c> entry naming the origin with <c>h2</c> or <c>h1</c> starts with TCP (measured,
/// BL-1320 Notes; ADR-0357's BL-1360 amendment).
/// </summary>
public sealed partial class TcpConnector
{
    private const string QuicAttemptVersion = "h3";

    private readonly ConditionalWeakTable<ConnectTarget, FirstHttpsConnectAttempt> _firstHttpsConnectAttempts = new();

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
    // After a TCP attempt an --alt-svc entry put first, the QUIC attempt goes on from that one's filter.
    private (ConnectTarget Target, HttpsConnectFilterTraceEvents? HttpsConnect, FirstHttpsConnectAttempt? Attempt) QuicConnectionFilters(ConnectTarget target, ProxyEndpoint? udpTunnelProxy = null)
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
            secondAttemptVersion: HttpsConnectSecondAttemptVersion,
            firstAttempt: TcpAttemptBefore(target));
        var traced = target with { Events = events };

        // DestinationOf writes the Alt-svc line once per target; the traced copy is the same target.
        _altSvcReported.TryAdd(traced, traced);
        return (traced, httpsConnect, QuicAttemptKeptFor(target, httpsConnect));
    }

    // The TCP attempt an --alt-svc entry put before this QUIC one, whose filter it goes on from.
    private FirstHttpsConnectAttempt? TcpAttemptBefore(ConnectTarget target) =>
        target.TcpFirstAttemptVersion is null ? null : TakeFirstHttpsConnectAttempt(target);

    // The QUIC attempt kept on the target for the TCP attempt the race starts after it: only when
    // QUIC went first, the filter is traced and a second attempt is to come.
    private FirstHttpsConnectAttempt? QuicAttemptKeptFor(ConnectTarget target, HttpsConnectFilterTraceEvents? httpsConnect) =>
        httpsConnect is null || HttpsConnectSecondAttemptVersion is not { } nextVersion || target.TcpFirstAttemptVersion is not null
            ? null
            : KeepFirstHttpsConnectAttempt(target, QuicAttemptVersion, nextVersion);

    // The TCP attempt an --alt-svc entry put before the QUIC one (ConnectTarget.TcpFirstAttemptVersion):
    // its failure is kept on the target for the QUIC attempt the race starts after it (BL-1320 Notes).
    private FirstHttpsConnectAttempt? TcpFirstAttemptOf(ConnectTarget target, HttpsConnectFilterTraceEvents? httpsConnect) =>
        httpsConnect is not null && target.TcpFirstAttemptVersion is { } version
            ? KeepFirstHttpsConnectAttempt(target, version, QuicAttemptVersion)
            : null;

    private FirstHttpsConnectAttempt KeepFirstHttpsConnectAttempt(ConnectTarget target, string version, string nextVersion)
    {
        var attempt = new FirstHttpsConnectAttempt(version, nextVersion);
        _firstHttpsConnectAttempts.AddOrUpdate(target, attempt);
        return attempt;
    }

    // The setup filter of a QUIC connect: eyeballing to the origin, or to the CONNECT-UDP proxy.
    private static Func<ITransferEvents, ITransferEvents> QuicSetupFilterStarter(ConnectDestination destination, ProxyEndpoint? udpTunnelProxy) =>
        udpTunnelProxy is null
            ? below => new SetupFilterTraceEvents(below, destination.Host, destination.Port)
            : below => new SetupFilterTraceEvents(below, udpTunnelProxy.Host, udpTunnelProxy.Port, SetupFilterTraceEvents.ToProxy);

    // The [HTTPS-CONNECT] filter over the given events: a TCP attempt an --alt-svc entry put first names
    // its version as preferred and h3 second; the attempt after a first one goes on from that one's.
    private HttpsConnectFilterTraceEvents HttpsConnectFilterOver(ITransferEvents events, bool tracesSetup, string? secondAttemptVersion, FirstHttpsConnectAttempt? firstAttempt, string? tcpFirstAttemptVersion)
    {
        var httpsConnect = tcpFirstAttemptVersion is null
            ? new HttpsConnectFilterTraceEvents(events, HttpsConnectFirstAttemptVersion, secondAttemptVersion)
            : new HttpsConnectFilterTraceEvents(events, tcpFirstAttemptVersion, QuicAttemptVersion, firstAttemptIsPreferred: true);
        if (firstAttempt is not null)
        {
            httpsConnect.ContinueAfterFirstAttempt(firstAttempt.Version, firstAttempt.NextVersion, firstAttempt.Failure, HappyEyeballsTimeout, tracesSetup);
        }

        return httpsConnect;
    }

    // A failed first attempt of a race keeps its failure for the attempt that follows to report
    // (measured, BL-1284 and BL-1320 Notes); any other failed connect writes the filter's failure lines.
    private static void ReportHttpsConnectAttemptFailure(HttpsConnectFilterTraceEvents? httpsConnect, FirstHttpsConnectAttempt? attempt, CurlExitCode exitCode)
    {
        if (attempt is not null)
        {
            attempt.Fail(exitCode);
            return;
        }

        httpsConnect?.ReportConnectFailed(exitCode);
    }

    // The first attempt the second attempt of a race to the same target follows, taken once.
    private FirstHttpsConnectAttempt? TakeFirstHttpsConnectAttempt(ConnectTarget target) =>
        _firstHttpsConnectAttempts.TryGetValue(target, out var attempt) && _firstHttpsConnectAttempts.Remove(target) ? attempt : null;

    // What the second attempt of a race needs of the first: both versions, and the first's exit code
    // once it failed, written by the first connect and read by the second, which may run beside it.
    private sealed class FirstHttpsConnectAttempt(string version, string nextVersion)
    {
        private readonly Lock _gate = new();
        private CurlExitCode? _failure;

        public string Version => version;

        public string NextVersion => nextVersion;

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
