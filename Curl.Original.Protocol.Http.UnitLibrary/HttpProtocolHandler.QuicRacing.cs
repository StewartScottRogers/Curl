using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that connects over QUIC, racing it against TCP for --http3.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Connects over QUIC as an <see cref="Http3Session" />, which a pooling connector shares
    /// between the transfers to the origin, each on a request stream of its own (BL-735).
    /// </summary>
    private ValueTask<ConnectResult> ConnectOverQuicAsync(HttpRequestPlan plan, ConnectTarget target, CancellationToken abandoned = default) =>
        plan.Deadline.ConnectMultiplexedSessionAsync(connector, target, quic => new Http3Session(quic), abandoned);

    /// <summary>
    /// Races QUIC against TCP for <c>--http3</c> as curl's ngtcp2 build does
    /// (<c>lib/cf-https-connect.c</c>, ADR-0144 section 4): the TCP connect starts when the QUIC
    /// connect fails or once <see cref="HttpRequestOptions.HappyEyeballsTimeout" /> has passed on
    /// the transfer's clock without it completing; the first to connect carries the transfer
    /// and the other is cancelled, its connection disposed should it still complete; when both
    /// fail the transfer fails with the QUIC attempt's exit code and message, or through a proxy
    /// with the TCP <c>CONNECT</c>'s, as curl 8.22.0 does after a refused CONNECT-UDP (measured,
    /// BL-942).
    /// </summary>
    private async ValueTask<ConnectResult> RaceQuicAgainstTcpAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        using CancellationTokenSource quicAbandoned = new();
        using CancellationTokenSource tcpAbandoned = new();
        Task<ConnectResult> quic = ConnectOverQuicAsync(plan, target, quicAbandoned.Token).AsTask();
        await FirstAttemptOrHappyEyeballsTimeoutAsync(plan, quic, HeadStartOfFirstQuicAttempt(plan)).ConfigureAwait(false);
        if (quic.IsCompleted && (await quic.ConfigureAwait(false)).Connection is not null)
        {
            return quic.Result;
        }

        Task<ConnectResult> tcp = plan.Deadline.ConnectAsync(connector, target, tcpAbandoned.Token).AsTask();
        if (await Task.WhenAny(quic, tcp).ConfigureAwait(false) == tcp && (await tcp.ConfigureAwait(false)).Connection is not null)
        {
            await quicAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(quic);
            return tcp.Result;
        }

        ConnectResult quicResult = await quic.ConfigureAwait(false);
        if (quicResult.Connection is not null)
        {
            await tcpAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(tcp);
            return quicResult;
        }

        ConnectResult tcpResult = await tcp.ConfigureAwait(false);
        return tcpResult.Connection is null && target.Proxy is null
            ? ConnectResult.Failed(quicResult.ExitCode, quicResult.ErrorMessage!, tcpResult.Timings, tcpResult.ConnectionNumber)
            : tcpResult;
    }

    /// <summary>
    /// Races TCP against QUIC for <c>--http3</c> with TCP as the preferred first attempt, as curl
    /// 8.21.0 does for an <c>--alt-svc</c> entry naming the origin itself with <c>h2</c> or
    /// <c>h1</c> (<c>cf_hc_get_pref_alpn</c>, BL-948): <see cref="RaceQuicAgainstTcpAsync" /> with the
    /// two attempts swapped, so the QUIC connect starts when the TCP connect fails or once the
    /// happy-eyeballs timeout has passed, and when both fail the transfer fails with the TCP
    /// attempt's result, the first attempt's, as curl's <c>cf_hc_connect</c> reports it.
    /// Both attempts get one target naming the TCP attempt's version (<see cref="ConnectTarget.TcpFirstAttemptVersion" />),
    /// so a connector can write the <c>[HTTPS-CONNECT]</c> lines of the race (BL-1360).
    /// </summary>
    private async ValueTask<ConnectResult> RaceTcpAgainstQuicAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        target = target with { TcpFirstAttemptVersion = plan.Options.TcpFirstAttemptVersion };
        using CancellationTokenSource quicAbandoned = new();
        using CancellationTokenSource tcpAbandoned = new();
        Task<ConnectResult> tcp = plan.Deadline.ConnectAsync(connector, target, tcpAbandoned.Token).AsTask();
        await FirstAttemptOrHappyEyeballsTimeoutAsync(plan, tcp, plan.Options.HappyEyeballsTimeout).ConfigureAwait(false);
        if (tcp.IsCompleted && (await tcp.ConfigureAwait(false)).Connection is not null)
        {
            return tcp.Result;
        }

        Task<ConnectResult> quic = ConnectOverQuicAsync(plan, target, quicAbandoned.Token).AsTask();
        if (await Task.WhenAny(tcp, quic).ConfigureAwait(false) == quic && (await quic.ConfigureAwait(false)).Connection is not null)
        {
            await tcpAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(tcp);
            return quic.Result;
        }

        ConnectResult tcpResult = await tcp.ConfigureAwait(false);
        if (tcpResult.Connection is not null)
        {
            await quicAbandoned.CancelAsync().ConfigureAwait(false);
            _ = DisposeLosingAttemptAsync(quic);
            return tcpResult;
        }

        ConnectResult quicResult = await quic.ConfigureAwait(false);
        return quicResult.Connection is not null ? quicResult : tcpResult;
    }

    /// <summary>
    /// The head start QUIC has over TCP: the happy-eyeballs timeout, plus <see cref="QuicColdStartAllowance" />
    /// for the first QUIC attempt of the process only (BL-5660, ADR-1326).
    /// </summary>
    private TimeSpan HeadStartOfFirstQuicAttempt(HttpRequestPlan plan) =>
        QuicColdStartAllowance > TimeSpan.Zero && QuicColdStart.TryTakeColdStart()
            ? plan.Options.HappyEyeballsTimeout + QuicColdStartAllowance
            : plan.Options.HappyEyeballsTimeout;

    /// <summary>
    /// Waits until the first attempt of a race, <paramref name="first" />, completes or the
    /// happy-eyeballs timeout passes on the transfer's clock, whichever comes first.
    /// </summary>
    private static async Task FirstAttemptOrHappyEyeballsTimeoutAsync(HttpRequestPlan plan, Task first, TimeSpan headStart)
    {
        using CancellationTokenSource firstCompleted = new();
        Task timeout = Task.Delay(headStart, plan.Context.TimeProvider, firstCompleted.Token);
        await Task.WhenAny(first, timeout).ConfigureAwait(false);
        await firstCompleted.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>Disposes the connection a connect that lost the race opens anyway.</summary>
    private static async Task DisposeLosingAttemptAsync(Task<ConnectResult> attempt)
    {
        await ((Task)attempt).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (attempt.IsCompletedSuccessfully && attempt.Result.Connection is { } connection)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Decides whether the transfer tries QUIC: <c>--http3</c> or <c>--http3-only</c> with an
    /// <c>https://</c> URL, no Unix domain socket, and no proxy or an HTTP or HTTPS one, which
    /// the connector tunnels QUIC through with CONNECT-UDP (BL-942).
    /// </summary>
    private static bool TriesQuic(HttpRequestPlan plan, ConnectTarget target) =>
        plan.Options.Version is HttpVersionPreference.Http3 or HttpVersionPreference.Http3Only
            && target.UseTls
            && (plan.Options.ForwardProxy is null || IsHttpProxy(plan.Options.ForwardProxy))
            && !plan.Options.OverUnixSocket;
}
