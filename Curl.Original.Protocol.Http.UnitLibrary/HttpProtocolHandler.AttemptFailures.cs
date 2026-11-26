using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that decides the outcome of a failed attempt, including whether it is sent again.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reports a read a socket error failed as the <c>-v</c> line curl 8.21.0's <c>failf</c> prints
    /// for it, such as <c>Recv failure: Connection was reset</c>, before the connection's end is
    /// reported (measured, BL-449 Notes), then the failure's own <see cref="HttpTransferException.InfoLines" />,
    /// such as the line curl 8.21.0's <c>cf-ngtcp2.c</c> prints for an HTTP/3 stream the server refused
    /// (ADR-0187) or reset (BL-5997). Any other failure is left to the transfer's result.
    /// </summary>
    private static void ReportReceiveFailure(ITransferEvents events, HttpTransferException failure)
    {
        if (failure.Message.StartsWith(HttpTransferMessages.ReceiveFailurePrefix, StringComparison.Ordinal))
        {
            events.ReportInfo(failure.Message);
        }

        foreach (string line in failure.InfoLines)
        {
            events.ReportInfo(line);
        }
    }

    /// <summary>
    /// Builds the outcome of an exchange whose HTTP/3 or HTTP/2 stream the server refused, as curl
    /// 8.21.0's <c>Curl_retry_request</c> does (ADR-0187): when no byte of the response had
    /// arrived, the request is sent again on a new connection up to
    /// <see cref="MaximumStreamRefusedRetries" /> times, and the refusal after that fails with
    /// <c>Connection died, tried 5 times before giving up</c>; once the response had begun it
    /// fails at once with curl's text for exit 56. A <c>-T</c> upload is rewound for the retry
    /// as curl's <c>Curl_creader_set_rewind</c> does (BL-885, ADR-0279): a seekable one to where
    /// <paramref name="upload" /> began reading it, one that implements <see cref="IUploadRewinder" />
    /// through it, and one that cannot seek, or whose rewinder fails, makes the retry fail with exit 65
    /// before it connects (<see cref="HttpRequestPlan.UploadRewindFailure" />).
    /// </summary>
    private static HttpAttemptOutcome StreamRefusedOutcome(HttpRequestPlan plan, HttpRequestBodyWriter upload, TransferResult refused, bool responseBegan)
    {
        if (responseBegan)
        {
            return new HttpAttemptOutcome(refused.ErrorMessage == HttpTransferMessages.Http2GoAwayReceiveFailure ? refused : refused with { ErrorMessage = HttpTransferMessages.ReceiveFailed }, null, KeepsAlive: false);
        }

        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.RefusedStreamRetrying);
        return plan.StreamRefusedRetries < MaximumStreamRefusedRetries
            ? new HttpAttemptOutcome(refused, plan.AfterStreamRefused(RewindUploadFailure(plan.Framing.Body, upload)), KeepsAlive: false) { DiedBeforeResponse = true, RetryCount = plan.StreamRefusedRetries + 1 }
            : new HttpAttemptOutcome(refused with { ErrorMessage = HttpTransferMessages.ConnectionDiedGivingUp(MaximumStreamRefusedRetries) }, null, KeepsAlive: false) { GaveUpOnRefusedStream = true };
    }

    /// <summary>
    /// Rewinds a <c>-T</c> upload read from a seekable stream to where <paramref name="upload" />
    /// began reading it, or one that implements <see cref="IUploadRewinder" /> through it, so it is
    /// sent again whole; gives the message the rewind failed with:
    /// <see cref="HttpTransferMessages.UploadSeekFailed" /> for any other stream that cannot seek,
    /// such as stdin, the rewinder's own when it fails, and <see langword="null" /> when the upload
    /// was rewound or is a body of bytes or none.
    /// </summary>
    private static string? RewindUploadFailure(HttpRequestBody? body, HttpRequestBodyWriter upload)
    {
        if (body is not StreamBody stream)
        {
            return null;
        }

        if (!CanResend(stream.Content))
        {
            return HttpTransferMessages.UploadSeekFailed;
        }

        upload.Rewound(stream);
        return upload.RewindFailure;
    }

    /// <summary>
    /// Tells whether a request body can go out again: it is not a stream, is an empty stream, or is
    /// one <see cref="CanResend" /> says can be rewound (BL-5130).
    /// </summary>
    private static bool BodyCanBeSentAgain(HttpRequestBody? body) =>
        body is not StreamBody { Length: not 0 } stream || CanResend(stream.Content);

    /// <summary>
    /// Rewinds a non-empty stream upload for a resend on a fresh connection, as
    /// <see cref="RewindUploadFailure" /> does; an empty one has nothing to rewind (BL-5130).
    /// </summary>
    private static string? RewindStreamUploadFailure(HttpRequestBody? body, HttpRequestBodyWriter upload) =>
        body is StreamBody { Length: not 0 } ? RewindUploadFailure(body, upload) : null;

    /// <summary>
    /// Tells whether a stream body can be sent again from its start: it can seek, or it
    /// implements <see cref="IUploadRewinder" />.
    /// </summary>
    private static bool CanResend(Stream content) => content.CanSeek || content is IUploadRewinder;

    /// <summary>
    /// Fails a retry whose <c>-T</c> upload cannot be rewound before it connects, with exit 65 and
    /// <paramref name="message" /> - <c>seek callback returned error 2</c>, or an
    /// <see cref="IUploadRewinder" />'s own - after the <c>-v</c> lines curl 8.21.0's
    /// <c>cr_in_rewind</c> and <c>Curl_client_start</c> write for it (ADR-0279).
    /// </summary>
    private static TransferResult UploadRewindFailed(HttpRequestPlan plan, string message, TransferReport? earlier)
    {
        plan.Context.Events.ReportInfo(message);
        plan.Context.Events.ReportInfo(HttpTransferMessages.UploadReaderRewindFailed);
        return TransferResult.Failure(CurlExitCode.SendFailRewind, message) with { Report = earlier };
    }

    /// <summary>
    /// Builds the outcome of an exchange whose HTTP/2 stream the server reset with
    /// <c>HTTP_1_1_REQUIRED</c>, as curl 8.18.0's <c>multi_runsingle</c> does for
    /// <c>Curl_h2_http_1_1_error</c> (CGR-032, ADR-0906): <c>-v</c> says <c>Downgrades to HTTP/1.1</c>,
    /// the HTTP/2 connection is closed and the request is sent again, once, over HTTP/1.1 on a
    /// new connection, its body rewound as for a refused stream.
    /// </summary>
    private static HttpAttemptOutcome Http11RequiredOutcome(HttpRequestPlan plan, HttpRequestBodyWriter upload, TransferResult failed)
    {
        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.DowngradesToHttp11);
        return new HttpAttemptOutcome(failed, plan.AfterHttp11Required(RewindUploadFailure(plan.Framing.Body, upload)), KeepsAlive: false);
    }

    /// <summary>
    /// Builds the outcome of an exchange that failed with <paramref name="failure" />: as
    /// <see cref="StreamRefusedOutcome" /> says when the server refused its stream; sent
    /// again once on a fresh connection when a connection that already carried a request died
    /// before the response
    /// (<see cref="DiedBeforeResponse" />); and final otherwise, keeping the connection alive
    /// when only the request's HTTP/2 or HTTP/3 stream was reset (CGR-140, BL-5997).
    /// </summary>
    private static HttpAttemptOutcome FailedOutcome(HttpRequestPlan plan, bool reusedConnection, HttpRequestBodyWriter upload, HttpResponseHeadReader headReader, HttpTransferException failure, TransferResult failed)
    {
        if (failure.IsStreamRefused)
        {
            return StreamRefusedOutcome(plan, upload, failed, headReader.HasReceived);
        }

        if (failure.RequiresHttp11 && !plan.DowngradedToHttp11)
        {
            return Http11RequiredOutcome(plan, upload, failed);
        }

        return DiedBeforeResponse(plan, reusedConnection, headReader, failure)
            ? new HttpAttemptOutcome(failed, plan.OnFreshConnection().FailingUploadRewind(RewindStreamUploadFailure(plan.Framing.Body, upload)), KeepsAlive: false) { DiedBeforeResponse = true }
            : new HttpAttemptOutcome(failed, null, KeepsAlive: failure.LeavesConnectionIntact);
    }
}
