using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that connects for a request, and reports a request that failed before or while connecting.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Opens a connection, or takes a pooled one, and sends <paramref name="plan" /> on it, then
    /// each retry a response asks for - an authentication retry, or a resend without
    /// <c>Expect</c> - on the same connection while it stays open, or on a new one when it
    /// closes. A pooled connection that dies before its response begins is closed and the
    /// request sent again once on a fresh one, as curl 8.21.0 does (BL-336 Notes).
    /// </summary>
    /// <remarks>
    /// The connection is marked reusable when the last response on it persists, and the
    /// end-of-transfer <c>-v</c> line is reported once it is disposed (ADR-0050), after the
    /// final exchange reports the transfer done (ADR-0111). A connect
    /// that fails reports <c>closing connection #N</c> after the connector's own lines, as
    /// curl 8.21.0 does for a failed resolve, a refused dial and a connect timeout (ADR-0105).
    /// </remarks>
    /// <param name="plan">The request to send.</param>
    /// <param name="earlier">
    /// The report of the exchange whose response asked for this retry, or
    /// <see langword="null" /> for the first.
    /// </param>
    /// <param name="resentAfterConnectionDied">
    /// Whether this is the resend of a request whose pooled connection died before its response
    /// began, which curl 8.22.0 draws apart from a retry a response asked for.
    /// </param>
    private async ValueTask<TransferResult> ConnectAndExchangeAsync(HttpRequestPlan plan, TransferReport? earlier, bool resentAfterConnectionDied = false)
    {
        if (FailureBeforeConnecting(plan, earlier) is { } failure)
        {
            return failure;
        }

        ConnectTarget target = TargetOf(plan);
        ConnectResult connect = await ConnectAsync(plan, target).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            if (connect.ProxyConnectResponseCode != 0)
            {
                // The proxy answered the CONNECT, so curl is past connect and still draws the meter (CGR-242).
                ReportRequestStarted(plan, earlier, resentAfterConnectionDied);
            }

            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.Closing(connect.ConnectionNumber));
            return FailedConnectResult(plan, connect);
        }

        plan.OpenedConnection = true;
        plan.TakeServerCertificateFrom(connect);
        ReportRequestStarted(plan, earlier, resentAfterConnectionDied);

        HttpAttemptOutcome outcome;
        await using (connection.ConfigureAwait(false))
        {
            outcome = await ExchangeOnConnectionAsync(plan, connect, connection, earlier).ConfigureAwait(false);
        }

        if (outcome.Retry is null)
        {
            plan.Progress.ReportTransferDone();
        }

        ReportConnectionEnd(plan.Context, target, connect, outcome);
        return outcome.Retry is { } reconnect
            ? await ConnectAndExchangeAsync(await CountedOnIfResentAsync(reconnect, outcome.DiedBeforeResponse).ConfigureAwait(false), outcome.Result.Report, outcome.DiedBeforeResponse).ConfigureAwait(false)
            : outcome.Result;
    }

    /// <summary>
    /// Gives a request resent on a fresh connection (<paramref name="resentAfterConnectionDied" />)
    /// the plan it goes out with: one whose <c>Authorization</c> is a connection-bound answer, an NTLM
    /// Type 3 message or a Negotiate token, starts its handshake again with the authenticator's first
    /// answer (an NTLM Type 1), since the fresh connection knows nothing of the dead one's handshake
    /// (BL-5121); otherwise <see cref="CountedOnIfResent" />'s.
    /// </summary>
    private async ValueTask<HttpRequestPlan> CountedOnIfResentAsync(HttpRequestPlan retry, bool resentAfterConnectionDied)
    {
        if (!resentAfterConnectionDied || !IsConnectionBoundAnswer(retry.Authorization))
        {
            return CountedOnIfResent(retry, resentAfterConnectionDied);
        }

        (string? first, HttpTransferException? failure) = await CreateFirstAuthorizationAsync(retry.AuthRequest, CancellationToken.None).ConfigureAwait(false);
        return failure is null ? retry.WithHandshakeRestarted(first) : CountedOnIfResent(retry, resentAfterConnectionDied);
    }

    /// <summary>Decides whether an <c>Authorization</c> value is a later leg of NTLM (not Type 1) or Negotiate, which belongs to one connection.</summary>
    private static bool IsConnectionBoundAnswer(string? authorization) =>
        authorization is { } value
        && ((value.StartsWith("NTLM ", StringComparison.Ordinal) && !IsNtlmType1(value))
            || value.StartsWith("Negotiate ", StringComparison.Ordinal));

    /// <summary>
    /// Gives a request sent again on a fresh connection because its reused one died before the
    /// response (<paramref name="resentAfterConnectionDied" />) its <c>Authorization</c> value as
    /// <see cref="IHttpAuthenticator.RepeatAuthorization" /> sends it again, so a Digest answer
    /// counts its nonce on (the same <c>cnonce</c>, <c>nc=00000002</c>), as curl 8.22.0 makes the
    /// answer again for the resend (measured, BL-3874, ADR-0950); <paramref name="retry" /> itself otherwise.
    /// </summary>
    private HttpRequestPlan CountedOnIfResent(HttpRequestPlan retry, bool resentAfterConnectionDied) =>
        resentAfterConnectionDied ? retry.WithAuthorizationSentAgain(RepeatAuthorization(retry)) : retry;

    /// <summary>
    /// Reports the progress of a request now connected: the transfer started for the first, a
    /// resend on a fresh connection for one whose pooled connection died (BL-2200), and a retried
    /// request starting for one a response asked for (<paramref name="earlier" /> set), which curl
    /// 8.21.0 draws on a new progress-meter line (BL-2087).
    /// </summary>
    private static void ReportRequestStarted(HttpRequestPlan plan, TransferReport? earlier, bool resentAfterConnectionDied)
    {
        if (earlier is null)
        {
            plan.Progress.ReportTransferStarted();
        }
        else if (resentAfterConnectionDied)
        {
            plan.Progress.ReportRequestResentOnFreshConnection();
        }
        else
        {
            plan.Progress.ReportRequestRetried();
        }
    }

    /// <summary>
    /// Gives the failure a request meets before it connects: <c>--http3-only</c> with a URL that
    /// is not <c>https://</c> (<see cref="Http3NeedsHttps" />), or a resend whose <c>-T</c> upload
    /// cannot be rewound (<see cref="UploadRewindFailed" />): one marked so
    /// (<see cref="HttpRequestPlan.UploadRewindFailure" />), or a chunked resend to an HTTP/1.0
    /// server (<see cref="IsChunkedResendToHttp10" />) of an upload whose
    /// <see cref="IRewindableUpload.CanRewind" /> says no, as stdin read through a pipe off Windows
    /// is, which curl 8.22.0 fails rewinding before it reaches the exit 25 of
    /// <see cref="ThrowIfChunkedResendToHttp10" /> (upstream test1072 and test1073 as GF-0082 runs
    /// them, ADR-0524, BL-2337); <see langword="null" /> for none.
    /// </summary>
    private static TransferResult? FailureBeforeConnecting(HttpRequestPlan plan, TransferReport? earlier)
    {
        if (plan.Options.Version == HttpVersionPreference.Http3Only && plan.Context.Url.Scheme != "https")
        {
            return Http3NeedsHttps(plan);
        }

        return RewindFailureOf(plan) is { } message ? UploadRewindFailed(plan, message, earlier) : null;
    }

    /// <summary>
    /// Gives the message <paramref name="plan" />'s upload failed to rewind with: the one it is
    /// marked with (<see cref="HttpRequestPlan.UploadRewindFailure" />), or for a chunked resend to an
    /// HTTP/1.0 server of an upload curl could not rewind, <see cref="HttpTransferMessages.UploadSeekFailed" />;
    /// <see langword="null" /> when the upload was rewound or needs no rewind.
    /// </summary>
    private static string? RewindFailureOf(HttpRequestPlan plan) =>
        plan.UploadRewindFailure ?? (IsChunkedResendToHttp10(plan) && CannotRewind(plan.Framing.Body) ? HttpTransferMessages.UploadSeekFailed : null);

    /// <summary>
    /// Tells whether <paramref name="body" /> is a stream whose <see cref="IRewindableUpload.CanRewind" />
    /// says curl could not rewind it.
    /// </summary>
    private static bool CannotRewind(HttpRequestBody? body) =>
        body is StreamBody { Content: IRewindableUpload { CanRewind: false } };

    /// <summary>
    /// Fails <c>--http3-only</c> with a URL that is not <c>https://</c> before connecting: exit 3,
    /// <c>HTTP/3 requested for non-HTTPS URL</c>, reported with <c>closing connection #-1</c>, as
    /// curl.se's ngtcp2 build does (measured, ADR-0144).
    /// </summary>
    private static TransferResult Http3NeedsHttps(HttpRequestPlan plan)
    {
        plan.Context.Events.ReportInfo(HttpTransferMessages.Http3NeedsHttps);
        return FailBeforeConnecting(plan, CurlExitCode.UrlMalformat, HttpTransferMessages.Http3NeedsHttps);
    }

    /// <summary>
    /// Fails <c>--http3-only</c> with <paramref name="exitCode" /> and <paramref name="message" />
    /// before connecting, reported with <c>closing connection #-1</c>, as curl.se's ngtcp2 build
    /// does (measured, ADR-0144, ADR-0223; BL-867).
    /// </summary>
    private static TransferResult FailBeforeConnecting(HttpRequestPlan plan, CurlExitCode exitCode, string message)
    {
        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.Closing(-1));
        return TransferResult.Failure(exitCode, message) with
        {
            Report = FailedConnectReport(plan, ConnectResult.Failed(exitCode, message)),
        };
    }

    /// <summary>
    /// Opens the transfer's connection (ADR-0144 section 4, ADR-0172): over QUIC for
    /// <c>--http3-only</c>, whose failure is the transfer's; over QUIC raced against TCP for
    /// <c>--http3</c> (<see cref="RaceQuicAgainstTcpAsync" />); and over TCP for every other
    /// version, for an <c>http://</c> URL and through a SOCKS proxy. A QUIC connection is handed on
    /// as an <see cref="Http3Session" />.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectAsync(HttpRequestPlan plan, ConnectTarget target)
    {
        if (!TriesQuic(plan, target))
        {
            return await plan.Deadline.ConnectAsync(connector, target).ConfigureAwait(false);
        }

        if (plan.Options.Version == HttpVersionPreference.Http3)
        {
            return plan.Options.TriesTcpBeforeQuic
                ? await RaceTcpAgainstQuicAsync(plan, target).ConfigureAwait(false)
                : await RaceQuicAgainstTcpAsync(plan, target).ConfigureAwait(false);
        }

        return await ConnectOverQuicAsync(plan, target).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the failed connect's result: its own exit code, or exit 22 under <c>-f</c> when a proxy refused the CONNECT with 400 or above (STD-125).
    /// </summary>
    private static TransferResult FailedConnectResult(HttpRequestPlan plan, ConnectResult connect)
    {
        // -f turns a refused CONNECT (status 400 or above) into exit 22, as curl does (STD-125, BL-5443).
        bool failsOnConnectStatus = plan.Options.Fail == HttpFailMode.Fail && connect.ProxyConnectResponseCode >= 400;
        return TransferResult.Failure(
            failsOnConnectStatus ? CurlExitCode.HttpReturnedError : connect.ExitCode,
            failsOnConnectStatus ? HttpTransferMessages.RequestedUrlReturnedError(connect.ProxyConnectResponseCode) : connect.ErrorMessage!) with
        {
            Report = FailedConnectReport(plan, connect),
            IsConnectionRefused = connect.IsConnectionRefused,
        };
    }

    /// <summary>
    /// Reports a connect that failed: whether it went to a forward proxy, and the transfer's
    /// timings with <c>%{time_pretransfer}</c>, <c>%{time_posttransfer}</c> and
    /// <c>%{time_starttransfer}</c> taken when it failed, as curl 8.21.0 takes them after a
    /// refused connect or a failed resolve (measured, ADR-0075), and whatever connect timings
    /// the connector recorded before it failed, so a refused dial still reports
    /// <c>%{time_namelookup}</c> (measured, ADR-0091).
    /// </summary>
    private static TransferReport FailedConnectReport(HttpRequestPlan plan, ConnectResult connect)
    {
        long failed = plan.Context.TimeProvider.GetTimestamp();
        return new TransferReport
        {
            UsedProxy = plan.Options.ForwardProxy is not null,
            ProxyConnectResponseCode = connect.ProxyConnectResponseCode,
            HeaderSize = connect.ProxyConnectHeaderBytes,
            ResponseHeadersStored = connect.ConnectReplyHeadersStored,
            RefusedConnectHeaderCount = connect.ConnectReplyHeadersStored,
            Timings = new TransferTimings(plan.Started, connect.Timings, failed, failed, failed, failed),
            ConnectionCount = ReachedAPeer(connect) ? 1 : 0,
        };
    }

    /// <summary>
    /// Whether a failed connect had made a connection curl counts in <c>%{num_connects}</c>: the TCP connect completed before a
    /// TLS or tunnel step failed, or the attempt timed out after the name resolved (STD-145).
    /// </summary>
    private static bool ReachedAPeer(ConnectResult connect) =>
        connect.Timings is { } timings && (timings.Connected is not null || connect.ExitCode == CurlExitCode.OperationTimedOut && timings.NameResolved is not null);
}
