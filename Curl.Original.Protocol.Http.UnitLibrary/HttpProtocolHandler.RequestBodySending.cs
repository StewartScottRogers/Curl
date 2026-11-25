using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that sends the request body.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Sends the request body as <see cref="SendBodyAsync" /> does, and holds a send failure after a
    /// final 2xx answered a request that waited for <c>100 Continue</c> instead of throwing it, so that
    /// response is read and delivered first, as curl 8.22.0 does (measured, BL-4909).
    /// </summary>
    /// <returns>
    /// Whether the body was left unsent, as <see cref="SendBodyAsync" /> gives it, and the send
    /// failure held, or <see langword="null" /> for none.
    /// </returns>
    private static async ValueTask<(bool BodyLeftUnsent, HttpTransferException? SendFailedAfter2xx)> SendBodyBefore2xxAsync(
        ITransferContext context,
        HttpRequestFraming framing,
        IConnection responseConnection,
        HttpRequestBodyWriter upload,
        HttpResponseHeadReader headReader,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await SendBodyAsync(context, framing, responseConnection, upload, headReader, cancellationToken).ConfigureAwait(false), null);
        }
        catch (HttpTransferException failure) when (failure.ExitCode == CurlExitCode.SendError && responseConnection is HttpContinueWaitConnection { AnsweredWithFinal2xx: true })
        {
            return (false, failure);
        }
    }

    /// <summary>
    /// Throws a send failure held by <see cref="SendBodyBefore2xxAsync" /> or by a held body's send
    /// once the response has been delivered, unless that response names
    /// <c>Connection: close</c>: curl 8.22.0 then ends the transfer with the response, exit 0, and
    /// otherwise reports <c>Send failure</c>, exit 55, after it (measured, BL-4909).
    /// </summary>
    /// <exception cref="HttpTransferException"><paramref name="sendFailedAfter2xx" />, when the response does not close the connection.</exception>
    private static void ThrowIfSendFailedUnlessClosing(HttpTransferException? sendFailedAfter2xx, HttpResponseHead actedOn)
    {
        if (sendFailedAfter2xx is not null && !HttpConnectionPersistence.NamesConnectionClose(actedOn))
        {
            throw sendFailedAfter2xx;
        }
    }

    /// <summary>
    /// Sends the request head and body, if there is one: the head alone when there is no body
    /// or the request waits for <c>100 Continue</c>, and else with the body's first read, so a
    /// body whose first read fails sends nothing (measured, BL-184 Notes); the body at once, or
    /// once <paramref name="responseConnection" /> has waited for <c>100 Continue</c> and not
    /// been answered with a final status instead.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when a final status, or the end of the connection, came during the
    /// wait and the body has not been sent (over HTTP/1.x a final status leaves it held,
    /// <see cref="HttpContinueWaitConnection.HoldsBody" />); <see langword="false" /> when the
    /// body was sent or there is none.
    /// </returns>
    private static async ValueTask<bool> SendBodyAsync(
        ITransferContext context,
        HttpRequestFraming framing,
        IConnection responseConnection,
        HttpRequestBodyWriter upload,
        HttpResponseHeadReader headReader,
        CancellationToken cancellationToken)
    {
        if (framing.Body is not { } requestBody)
        {
            await upload.WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (responseConnection is HttpContinueWaitConnection waiting)
        {
            await upload.WriteHeadBeforeContinueAsync(requestBody, cancellationToken).ConfigureAwait(false);
            waiting.SendBody = token => SendHeldBodyAsync(context.Events, framing, upload, requestBody, waiting, token);
            if (!await waiting.WaitForContinueAsync(context.TimeProvider, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            ReportContinueWaitRanOut(context.Events, waiting);
            if (waiting.AnsweredWithInformational)
            {
                await headReader.ReadInterimHeadAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await upload.WriteAsync(requestBody, framing.IsChunked, cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Sends a body held until the wait for <c>100 Continue</c> ran out after a final status, with
    /// curl 8.22.0's lines around it (measured, BL-5960 Notes), and keeps a send failure for after
    /// the response (<see cref="FinishSendingBodyAsync" />). On an HTTP/2 stream whose response
    /// has already ended, the body still goes out and then fails with exit 55 after
    /// <c>stream N closed</c>, the connection staying open, as curl 8.18.0's nghttp2 build does
    /// (measured, BL-5972 Notes).
    /// </summary>
    private static async ValueTask SendHeldBodyAsync(
        ITransferEvents events,
        HttpRequestFraming framing,
        HttpRequestBodyWriter upload,
        HttpRequestBody requestBody,
        HttpContinueWaitConnection waiting,
        CancellationToken cancellationToken)
    {
        Http2StreamConnection? answeredStream = waiting.Http2Stream is { IsResponseEnded: true } ended ? ended : null;
        if (answeredStream is not null)
        {
            answeredStream.SendBodyAfterResponseEnd();
            upload.LeaveSentDataUnreported();
        }

        events.ReportInfo(HttpConnectionInfoLines.DoneWaitingForContinue);
        try
        {
            await upload.WriteAsync(requestBody, framing.IsChunked, cancellationToken).ConfigureAwait(false);
            if (answeredStream is null)
            {
                ReportRequestSent(events, framing, upload, bodyLeftUnsent: false);
                return;
            }

            events.ReportInfo(HttpConnectionInfoLines.StreamClosed(answeredStream.StreamId));
            waiting.HeldSendFailure = new HttpTransferException(CurlExitCode.SendError, HttpTransferMessages.SendFailed) { LeavesConnectionIntact = true };
        }
        catch (HttpTransferException failure) when (failure.ExitCode == CurlExitCode.SendError)
        {
            waiting.HeldSendFailure = failure;
        }
    }

    /// <summary>
    /// Settles an unsent body once the final status's head is read. A held body, as curl 8.22.0
    /// does (measured, BL-5960 Notes): a response answered with a resend gives it up; a status of
    /// 300 or above on a connection that stays open stops the upload, which shuts the connection
    /// down (<see cref="ReportUploadStopped" />); any other response leaves it held while it is
    /// read. A body left unsent on an HTTP/2 or HTTP/3 stream
    /// (<see cref="HttpContinueWaitConnection.StoppedStreamUpload" />) writes the same two stop
    /// lines unless a resend answers it, and the connection stays open (measured, BL-5970 Notes).
    /// </summary>
    private static void SettleUnsentBodyAtHead(HttpRequestPlan plan, HttpResponseHead head, HttpRequestPlan? retry, HttpRequestBodyWriter upload, IConnection responseConnection)
    {
        if (responseConnection is not HttpContinueWaitConnection waiting)
        {
            return;
        }

        if (waiting.HoldsBody)
        {
            LeaveEndedStreamsEmptyDataUnreported(waiting);
            StopOrKeepHolding(plan, head, retry, upload, waiting);
        }
        else if (waiting.StoppedStreamUpload && retry is null)
        {
            ReportStopBeforeEndOfSend(plan.Context.Events, upload);
        }
    }

    /// <summary>
    /// Leaves unreported the empty data of a stream whose head ended it while the body is held:
    /// curl writes no <c>{ [0 bytes data]</c> for it (measured, BL-5972 Notes).
    /// </summary>
    private static void LeaveEndedStreamsEmptyDataUnreported(HttpContinueWaitConnection waiting)
    {
        if (waiting.Http2Stream is { IsResponseEnded: true } ended)
        {
            ended.LeavesEmptyDataUnreported = true;
        }
    }

    /// <summary>Gives up a held body that a resend or the response's status stops, and else leaves it held.</summary>
    private static void StopOrKeepHolding(HttpRequestPlan plan, HttpResponseHead head, HttpRequestPlan? retry, HttpRequestBodyWriter upload, HttpContinueWaitConnection waiting)
    {
        bool stops = retry is null && StopsHeldUpload(plan, head);
        if (stops)
        {
            upload.StopBeforeSending();
        }

        if (stops || retry is not null)
        {
            waiting.Dispose();
        }
    }

    /// <summary>
    /// Finishes sending the body once the response is read: the rest of one kept sending past an
    /// error; a body still held, which goes out when its wait runs out on a connection that stays
    /// open, as an HTTP/2 or HTTP/3 stream's does, and is given up with curl 8.22.0's lines on one
    /// that closes (measured, BL-5960 Notes); the reset of an HTTP/2 stream whose upload a status stopped (measured, BL-5972
    /// Notes); and then any send failure kept for after the response.
    /// </summary>
    /// <exception cref="HttpTransferException">The send failed and the response does not close the connection (exit 55).</exception>
    private static async ValueTask FinishSendingBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpRequestBodyWriter upload,
        IConnection responseConnection,
        HttpTransferException? sendFailedAfter2xx,
        CancellationToken cancellationToken)
    {
        await upload.WriteRestAfterResponseAsync(cancellationToken).ConfigureAwait(false);
        if (responseConnection is HttpContinueWaitConnection waiting)
        {
            await ResetStoppedStreamUploadAsync(waiting, cancellationToken).ConfigureAwait(false);
            await SettleHeldBodyAfterResponseAsync(plan, head, waiting, cancellationToken).ConfigureAwait(false);
            sendFailedAfter2xx ??= waiting.HeldSendFailure;
        }

        ThrowIfSendFailedUnlessClosing(sendFailedAfter2xx, head);
    }

    /// <summary>
    /// Resets the HTTP/2 stream whose upload a status stopped, during the wait for
    /// <c>100 Continue</c> or part way through the body, once its response is read.
    /// </summary>
    private static ValueTask ResetStoppedStreamUploadAsync(HttpContinueWaitConnection waiting, CancellationToken cancellationToken) =>
        waiting.Http2Stream is { } stream && (waiting.StoppedStreamUpload || stream.IsUploadStopped)
            ? stream.ResetUnendedRequestAsync(cancellationToken)
            : ValueTask.CompletedTask;

    /// <summary>
    /// Resets a failed exchange's HTTP/2 stream whose response ended it with the upload unsent
    /// (<see cref="Http2StreamConnection.ResetUnendedRequestOfEndedResponseAsync" />); nothing on
    /// any other connection.
    /// </summary>
    private static ValueTask ResetFailedStreamUploadAsync(IHttpStreamConnection? requestStream, CancellationToken cancellationToken) =>
        requestStream is Http2StreamConnection stream ? stream.ResetUnendedRequestOfEndedResponseAsync(cancellationToken) : ValueTask.CompletedTask;

    /// <summary>Sends or gives up a body still held once the response is read.</summary>
    private static async ValueTask SettleHeldBodyAfterResponseAsync(HttpRequestPlan plan, HttpResponseHead head, HttpContinueWaitConnection waiting, CancellationToken cancellationToken)
    {
        if (!waiting.HoldsBody)
        {
            return;
        }

        // A stream's response does not close its connection, whatever its length (measured, BL-5972 Notes).
        if (waiting.IsMultiplexedStream || KeepsConnectionOpen(plan, head))
        {
            await waiting.SendHeldBodyAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.DoneReadingStopSending);
        plan.Context.Events.ReportInfo(HttpConnectionInfoLines.AbortUpload(0));
        waiting.Dispose();
    }

    /// <summary>Tells whether <paramref name="head" /> stops a held upload: a status of 300 or above that leaves the connection open.</summary>
    private static bool StopsHeldUpload(HttpRequestPlan plan, HttpResponseHead head) =>
        head.StatusLine.StatusCode >= 300 && KeepsConnectionOpen(plan, head);

    /// <summary>Tells whether <paramref name="head" /> leaves its connection open.</summary>
    private static bool KeepsConnectionOpen(HttpRequestPlan plan, HttpResponseHead head) =>
        HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, plan.Options.PassesTransferCoding, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding, IsThroughHttpProxy(plan.Options));

    /// <summary>
    /// Reports <c>Done waiting for 100-continue</c> before the body when the wait for
    /// <c>100 Continue</c> ran out with nothing received, as curl 8.21.0 does (measured,
    /// BL-449 Notes); nothing when a <c>100 Continue</c> ended it.
    /// </summary>
    private static void ReportContinueWaitRanOut(ITransferEvents events, HttpContinueWaitConnection waiting)
    {
        if (waiting.WaitRanOut)
        {
            events.ReportInfo(HttpConnectionInfoLines.DoneWaitingForContinue);
        }
    }
}
