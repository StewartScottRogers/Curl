using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that runs the exchanges on one connection, its retries and its HTTP/2 or HTTP/3 session.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Sends <paramref name="plan" /> on <paramref name="connection" />, then each retry that
    /// may go on the same connection; then marks the connection reusable when the last
    /// response is reported left intact - it persists, or it is an HTTP/1.0 keep-alive body the
    /// server closed, which the pool then finds dead as curl does (ADR-0112) - or reports that a
    /// pooled connection that died is being given up. A connection that speaks HTTP/2
    /// (<see cref="StreamSessionOf" />) carries each request on a stream of its own; its
    /// <see cref="Http2Session" /> is handed to the connection, which keeps it for the next
    /// transfer when pooled and sends its closing GOAWAY when it closes, or, on a connection
    /// that holds no session, the GOAWAY is sent here (BL-817). An HTTP/3 connection is marked
    /// reusable while its session takes new requests (BL-735).
    /// </summary>
    private async ValueTask<HttpAttemptOutcome> ExchangeOnConnectionAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        TransferReport? earlier)
    {
        if (RefusesAbsoluteFormHost(plan))
        {
            return AbsoluteFormTargetUnbuilt(plan, connect);
        }

        if (HttpTimeConditionLimit.Refuses(plan.Context.TimeCondition, OperatingSystem.IsWindows()))
        {
            connection.MarkReusable();
            return TimeValueRefused(plan, connect);
        }

        IHttpStreamSession? streams = StreamSessionOf(plan, connect, connection);
        Http2Session? unheldSession = UnheldHttp2Session(streams, connection);
        (HttpAttemptOutcome outcome, Http2Session? upgradedSession) = await ExchangeWithRetriesAsync(plan, connect, connection, earlier, streams).ConfigureAwait(false);
        unheldSession ??= UnheldHttp2Session(upgradedSession, connection);
        IHttpStreamSession? used = streams ?? upgradedSession;
        outcome.RanOnHttp2Stream = used is Http2Session;
        outcome.RanOnHttp3Stream = used is Http3Session;
        SkipClosingGoAwayWhenGivenUp(outcome, used);
        SettleConnection(plan, connection, used, outcome);
        await ShutDownAsync(unheldSession).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>
    /// Hands an HTTP/2 session to its connection to hold (BL-817), and gives it back when the
    /// connection holds none, so the handler sends its closing GOAWAY itself; gives
    /// <see langword="null" /> for any other session, or none.
    /// </summary>
    private static Http2Session? UnheldHttp2Session(IHttpStreamSession? streams, IConnection connection) =>
        streams is Http2Session http2 && !connection.TryHoldSession(http2) ? http2 : null;

    /// <summary>Has an HTTP/2 session on a connection given up after five refused streams close without its GOAWAY (BL-3838).</summary>
    private static void SkipClosingGoAwayWhenGivenUp(HttpAttemptOutcome outcome, IHttpStreamSession? session)
    {
        if (outcome.GaveUpOnRefusedStream && session is Http2Session http2)
        {
            http2.SkipClosingGoAway();
        }
    }

    /// <summary>Sends the closing GOAWAY of a session no connection holds, if there is one.</summary>
    private static ValueTask ShutDownAsync(Http2Session? unheldSession) =>
        unheldSession?.ShutDownAsync(CancellationToken.None) ?? ValueTask.CompletedTask;

    /// <summary>
    /// Marks the connection reusable when it can carry the next transfer
    /// (<see cref="CarriesNextTransfer" />), so a pool keeps it (BL-735), or reports that a pooled connection that died is being given up.
    /// </summary>
    private static void SettleConnection(HttpRequestPlan plan, IConnection connection, IHttpStreamSession? streams, HttpAttemptOutcome outcome)
    {
        if (CarriesNextTransfer(streams, outcome))
        {
            connection.MarkReusable();
        }
        else if (outcome.DiedBeforeResponse)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.ConnectionDiedRetrying(outcome.RetryCount));
            HttpExchangeLog.For(plan.Context.DiagnosticLog, streams).RetryingOnFreshConnection(outcome.RetryCount);
        }
    }

    /// <summary>
    /// Tells whether the connection can carry the next transfer: its response is reported left
    /// intact, or only its stream ended (<see cref="HttpAttemptOutcome.EndedOnlyItsStream" />,
    /// BL-5976, BL-5977, BL-5978); over HTTP/3, or after only its stream ended, the session must
    /// also still take new streams.
    /// </summary>
    private static bool CarriesNextTransfer(IHttpStreamSession? streams, HttpAttemptOutcome outcome) =>
        outcome.EndedOnlyItsStream
            ? (streams as Http2Session)?.AcceptsNewStreams ?? ((Http3Session)streams!).AcceptsNewStreams
            : outcome.ReportsLeftIntact && streams is not Http3Session { AcceptsNewStreams: false };

    /// <summary>
    /// Ends the stream of an exchange that timed out: an HTTP/2 stream is reset
    /// (<see cref="ResetGivenUpStreamAsync" />); an HTTP/3 stream is cancelled
    /// (<see cref="Http3StreamConnection.CancelUnlessEndedBothWaysAsync" />) and its transfer
    /// marked done, curl's <c>--trace-config http/3</c> lines included (BL-5978). The
    /// transfer's own token is cancelled, so none is passed on.
    /// </summary>
    private static async ValueTask EndTimedOutStreamAsync(IHttpStreamConnection? stream, ConnectResult connect)
    {
        if (stream is Http3StreamConnection http3)
        {
            await http3.CancelUnlessEndedBothWaysAsync(CancellationToken.None).ConfigureAwait(false);
            ReportTransferDone(http3, connect);
            return;
        }

        await ResetGivenUpStreamAsync(stream, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the HTTP/2 stream of an exchange that timed out or that <c>-f</c> failed
    /// (<see cref="Http2StreamConnection.ResetUnlessEndedBothWaysAsync" />); nothing on any
    /// other connection.
    /// </summary>
    private static ValueTask ResetGivenUpStreamAsync(IHttpStreamConnection? stream, CancellationToken cancellationToken) =>
        stream is Http2StreamConnection http2 ? http2.ResetUnlessEndedBothWaysAsync(cancellationToken) : ValueTask.CompletedTask;

    /// <summary>
    /// Resets what a failed exchange leaves open of its HTTP/2 stream: when <c>-f</c> failed it,
    /// the stream unless it has ended both ways (<see cref="ResetGivenUpStreamAsync" />); and
    /// after any failure, one whose response ended it with the upload unsent
    /// (<see cref="ResetFailedStreamUploadAsync" />), even on a connection that takes no new
    /// stream.
    /// </summary>
    private static async ValueTask ResetFailedStreamAsync(HttpTransferException failure, IHttpStreamConnection? requestStream, IConnection connection, CancellationToken cancellationToken)
    {
        if (failure.ExitCode == CurlExitCode.HttpReturnedError)
        {
            await ResetGivenUpStreamAsync(TrailerStreamOf(requestStream, connection), cancellationToken).ConfigureAwait(false);
        }

        await ResetFailedStreamUploadAsync(requestStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <paramref name="plan" />, framed for HTTP/2 or HTTP/3 when <paramref name="streams" /> is set,
    /// then each retry the response asks for while the connection stays open and its peer has not
    /// closed it (<see cref="IConnection.HasPeerClosed" />). After an h2c
    /// upgrade the retries go out on new streams of the session the connection switched to, as
    /// curl sends them (measured, BL-866 Notes), and that session is given back with the outcome.
    /// </summary>
    private async ValueTask<(HttpAttemptOutcome Outcome, Http2Session? UpgradedSession)> ExchangeWithRetriesAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection connection,
        TransferReport? earlier,
        IHttpStreamSession? streams)
    {
        HttpRequestPlan unprobed = OnNtlmAuthenticatedConnection(plan, connect, earlier);
        HttpRequestPlan first = streams is null ? unprobed : unprobed.ForHttp2OrHttp3();
        HttpAttemptOutcome outcome = await ExchangeAsync(first, connect, connection, earlier, newConnection: !connect.IsReused, streams).ConfigureAwait(false);
        Http2Session? upgradedSession = outcome.UpgradedSession;
        streams ??= upgradedSession;
        while (RetryOnSameConnection(outcome, connection) is { } retry)
        {
            ReportRetryOnSameConnection(retry, connect);
            if (retry.UploadRewindFailure is { } rewindFailure)
            {
                return (new HttpAttemptOutcome(UploadRewindFailed(retry, rewindFailure, outcome.Result.Report), null, outcome.KeepsAlive), upgradedSession);
            }

            retry.Progress.ReportRequestRetried();
            HttpRequestPlan next = streams is null ? retry : retry.ForHttp2OrHttp3();
            outcome = await ExchangeAsync(next, connect, connection, outcome.Result.Report, newConnection: false, streams).ConfigureAwait(false);
        }

        return (outcome, upgradedSession);
    }

    /// <summary>
    /// Gives the retry <paramref name="outcome" /> asks for when it can go out on the same
    /// connection: the connection persists and its peer has not closed it. curl 8.21.0 asks a
    /// kept connection whether it is alive before it sends on it again; one the server has
    /// closed is left intact and the retry issued anew, so the pool reports it dead and opens a
    /// fresh one (BL-2018, ADR-0467). Gives <see langword="null" /> otherwise.
    /// </summary>
    private static HttpRequestPlan? RetryOnSameConnection(HttpAttemptOutcome outcome, IConnection connection) =>
        outcome.KeepsAlive && !connection.HasPeerClosed ? outcome.Retry : null;

    /// <summary>
    /// Reports what curl 8.21.0 writes between a response it answers with another request and
    /// that request, sent on the same connection (measured, BL-959 Notes): the connection left
    /// intact, <c>Issue another request to this URL: '...'</c>, then the connection reused, which
    /// the console writes as <c>Reusing existing http: connection with host ...</c>.
    /// </summary>
    private static void ReportRetryOnSameConnection(HttpRequestPlan retry, ConnectResult connect)
    {
        ConnectTarget target = TargetOf(retry);
        ITransferEvents events = retry.Context.Events;
        events.ReportInfo(LeftIntactLine(target, connect));
        events.ReportInfo(HttpConnectionInfoLines.IssueAnotherRequest(retry.Context.Url, retry.Options.CommandLineTextEncoding));
        events.ReportConnectionReused(new ConnectionReusedEvent
        {
            Scheme = target.PoolScheme!,
            IsProxy = target.IsForwardProxy || target.Proxy is not null,
            HostName = target.Proxy?.Host ?? target.Host,
            Port = target.Proxy?.Port ?? target.Port,
            ConnectionNumber = connect.ConnectionNumber,
        });
    }

    /// <summary>
    /// Decides whether the connection speaks HTTP/2 (ADR-0141): with
    /// <see cref="HttpVersionPreference.Http2PriorKnowledge" />, from the first byte, and else
    /// when the TLS handshake agreed <c>h2</c> with ALPN.
    /// </summary>
    private static bool SpeaksHttp2(HttpRequestPlan plan, ConnectResult connect) =>
        plan.Options.Version == HttpVersionPreference.Http2PriorKnowledge || connect.ApplicationProtocol == "h2";

    /// <summary>
    /// Gives the session that carries each request on a stream of its own over the connection:
    /// the HTTP/3 session a QUIC connect made, which a pooled connection holds (BL-735), the HTTP/2 session an earlier transfer left with
    /// a pooled connection (BL-817), a new HTTP/2 session for a connection that speaks HTTP/2
    /// (<see cref="SpeaksHttp2" />), or <see langword="null" /> for HTTP/1.x.
    /// </summary>
    private IHttpStreamSession? StreamSessionOf(HttpRequestPlan plan, ConnectResult connect, IConnection connection) =>
        (IHttpStreamSession?)(connection as Http3Session)
            ?? (connection.Session as IHttpStreamSession)
            ?? (SpeaksHttp2(plan, connect) ? new Http2Session(connection, isPushEnabled: Http2PushSink is not null) : null);
}
