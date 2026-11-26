using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that sends one request and reads its response.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Sends the request on <paramref name="transport" /> and reads the response into the
    /// transfer's outputs; or, when the response is one this handler retries (a 401 it
    /// answers, or a 417 to a request whose body waited for <c>100 Continue</c>), writes its
    /// head and trailers, reads and discards its body, and returns the retry's plan. Over
    /// HTTP/2 or HTTP/3 the exchange runs on a new stream of <paramref name="streams" />
    /// (<see cref="IHttpStreamConnection" />), which the same code reads and writes.
    /// </summary>
    private async ValueTask<HttpAttemptOutcome> ExchangeAsync(
        HttpRequestPlan plan,
        ConnectResult connect,
        IConnection transport,
        TransferReport? earlier,
        bool newConnection,
        IHttpStreamSession? streams)
    {
        ITransferContext context = plan.Context;
        HttpRequestOptions options = plan.Options;
        CancellationToken cancellationToken = plan.Deadline.Token;

        // curl 8.21.0 says which version it uses before it builds the request, so the cookie
        // store's limit lines come after it (measured, BL-1136 Notes).
        ReportProtocolChosen(context.Events, newConnection, streams);
        IHttpStreamConnection? requestStream = CreateRequestStream(plan, streams);
        IConnection connection = ExchangeConnectionOf(plan, requestStream, transport);
        plan = PlanForConnection(plan, connection);
        HttpRequestFraming framing = plan.Framing;
        byte[] request = FormatRequestHead(plan, streams, connection is HttpH2cUpgradeConnection, options.EarlierResponseWasHttp10 || Http1VersionSeen.DowngradesToHttp10(transport));
        HttpFirstByteTimingConnection timedConnection = new(connection, context.TimeProvider);
        IConnection responseConnection = ResponseConnectionOf(plan, requestStream, timedConnection);
        // Ends a wait still holding the body however the exchange ends.
        using HttpContinueWaitConnection? continueWait = responseConnection as HttpContinueWaitConnection;
        HttpRequestBodyWriter upload = CreateUpload(plan, connection, responseConnection, requestStream, request);
        HttpExchange exchange = CreateExchange(plan, connect, connection, timedConnection, request.Length, upload, earlier, newConnection);
        HttpExchangeLog exchangeLog = HttpExchangeLog.For(context.DiagnosticLog, streams);
        HttpResponseBodyReader body = CreateBodyReader(plan, connect, responseConnection, exchangeLog, earlier);
        HttpResponseHeadReader headReader = CreateHeadReader(plan, responseConnection, connection, requestStream, body, upload);
        HttpRequestPlan? retry = null;
        HttpResponseHead? actedOn = null;
        HttpBodyDelivery delivery = HttpBodyDelivery.Deliver;
        HttpFailMode fail;
        LogVersionChosen(exchangeLog, plan, transport, newConnection, streams);
        ReportAuthorizationLines(plan);
        try
        {
            HttpRequestHeadFormatter.ThrowIfTooLarge(request);
            ThrowIfRefused(framing);
            ThrowIfChunkedResendToHttp10(plan);
            ThrowIfAuthorizationFailed(plan);
            exchange.RequestReady = context.TimeProvider.GetTimestamp();
            (bool bodyLeftUnsent, HttpTransferException? sendFailedAfter2xx) = await SendBodyBefore2xxAsync(context, framing, responseConnection, upload, headReader, cancellationToken).ConfigureAwait(false);
            ReportRequestSent(context.Events, framing, upload, bodyLeftUnsent);
            exchangeLog.RequestSent(framing.Method, context.Url.AbsolutePath, request);
            exchange.RequestSent = context.TimeProvider.GetTimestamp();
            exchange.Head = await ReadHeadAsync(headReader, context.HeaderOutput, newConnection, cancellationToken).ConfigureAwait(false);
            ThrowIfVersionMismatched(transport, streams is not null || IsSwitchedToHttp2(connection), exchange.Head);
            HttpHeadRefusal? refusal = headReader.Refusal;
            exchange.Head = HeadCurlRead(exchange.Head, refusal);
            actedOn = headReader.HeadActedOn(exchange.Head);
            body.HeadersStoredBefore = headReader.HeadersStoredWith(exchange.Head);
            exchangeLog.ReplyRead(actedOn);
            exchange.RedirectUrl = HttpRedirectLocation.Find(context.Url, actedOn, options.CommandLineTextEncoding);
            await WriteHeadersAsync(context.HeaderOutput, exchange.Head.HeadBytes, cancellationToken).ConfigureAwait(false);
            ThrowIfHeaderRefused(refusal);
            ThrowIfClosedBeforeContentLength(plan, actedOn, headReader);
            headReader.ReportHeaderHeldAtClose();
            ReportNoEndOfMessageIndicator(plan, actedOn, headReader);
            plan.RecordSchemesOffered(actedOn);
            exchange.AuthSchemesOffered = plan.AuthSchemesOffered;
            exchange.ProxyAuthSchemesOffered = plan.ProxyAuthSchemesOffered;
            try
            {
                retry = await RetryOfAsync(plan, actedOn, bodyLeftUnsent, upload, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // A challenge's lines go before any failure that ends the exchange while its answer is made (BL-5932).
                headReader.ReleaseDeferredHeaders();
            }

            ReportNtlmConnectionClosure(plan, actedOn);
            fail = FailModeOf(plan, retry, actedOn);
            ThrowIfFailingBeforeBody(fail, actedOn);
            bool discardsBody = DiscardsBody(options, retry, actedOn, exchange.RedirectUrl);
            ReportAuthRetryRewind(plan, actedOn, retry);
            SettleUnsentBodyAtHead(plan, actedOn, retry, upload, responseConnection);
            ReportIgnoredBody(plan, actedOn, discardsBody && !upload.CutShort);
            ReportUploadStopped(context.Events, upload, retry, discardsBody);
            delivery = DeliveryOf(plan, actedOn, discardsBody);
            HttpDownloadConditions.ReportUndeliveredBody(context, actedOn, delivery);
            bool ignoresBody = IgnoresBody(plan, actedOn, delivery, discardsBody);
            headReader.ReportHeldLines();
            await ReadBodyAsync(plan, actedOn, body, TrailerStreamOf(requestStream, connection), delivery, ignoresBody, cancellationToken).ConfigureAwait(false);
            await FinishSendingBodyAsync(plan, actedOn, upload, responseConnection, sendFailedAfter2xx, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException failure)
        {
            HttpAttemptOutcome failedOutcome = OutcomeOfFailure(plan, exchange, body, exchangeLog, headReader, upload, !newConnection, failure);
            failedOutcome.UpgradedSession = UpgradedSessionOf(connection);
            await ResetFailedStreamAsync(failure, requestStream, connect, connection, cancellationToken).ConfigureAwait(false);
            return failedOutcome;
        }
        catch (OperationCanceledException canceled) when (plan.Deadline.EndedByLimit)
        {
            HttpAttemptOutcome timedOutOutcome = OutcomeOfTimeout(plan, exchange, body, exchangeLog, headReader, canceled);
            timedOutOutcome.UpgradedSession = UpgradedSessionOf(connection);
            await EndGivenUpStreamAsync(TrailerStreamOf(requestStream, connection), connect, CancellationToken.None).ConfigureAwait(false);
            return timedOutOutcome;
        }
        catch (OperationCanceledException canceled)
        {
            // A cancellation that is not this exchange's own limit (the -Y/-y watchdog) ends the transfer in the engine, which reads the report as far as the exchange got from the exception (BL-5862).
            canceled.Data[CancelledReportDataKey] = exchange.Report(body);
            throw;
        }

        ReportTransferDone(requestStream, connect);
        bool failsAfterBody = FailsAfterBody(fail, actedOn!);
        TransferResult result = failsAfterBody
            ? FailedAfterBody(actedOn!, exchange.Report(body), body.BytesWritten)
            : Succeeded(delivery, actedOn!, exchange.Report(body));
        LogExchanged(exchangeLog, context.TimeProvider, exchange.RequestReady, actedOn!, body);
        HttpAttemptOutcome completed = new(result, retry, KeepsAlive(plan, actedOn, upload, headReader, delivery, StreamSessionAfter(streams, connection), body.FoundExcess))
        {
            FailedAfterBody = failsAfterBody,
            LeftIntactAfterServerClosed = LeftIntactAfterServerClosed(plan, actedOn, upload, headReader, delivery),
            UpgradedSession = UpgradedSessionOf(connection),
        };
        return RefusingResendPastRedirectLimit(plan, actedOn!.StatusLine.StatusCode, exchangeLog, completed);
    }

    /// <summary>
    /// Ends an exchange whose response asks for a resend that would pass
    /// <see cref="HttpRequestOptions.MaxRedirects" /> with exit 47 and no resend; any other
    /// outcome is given back as it is. curl counts each resend after a 417 and each
    /// authentication retry after a 401 or 407 as a followed redirect, with or without
    /// <c>-L</c>, sharing the count with the hops <c>-L</c> followed before this request, and
    /// <c>--max-redirs 0</c> refuses even the first (417: measured, BL-396 Notes; 401 and 407:
    /// curl 8.22.0, <c>--max-redirs 0</c> with <c>--digest</c> or <c>--anyauth</c> ends with
    /// exit 47). curl refuses it only once the response is done, so its body was discarded as
    /// before a resend and its connection ends as a successful exchange's does, ahead of
    /// <c>Maximum (N) redirects followed</c> (measured, BL-5944 Notes). Its
    /// <c>%{redirect_url}</c> is the URL the resend would go to
    /// (<see cref="HttpRedirectLocation.OfResend" />), whatever <c>Location</c> the response has
    /// (measured, BL-5951 Notes). The send of a body a probe held back, which a 2xx asks for, is
    /// no redirect.
    /// </summary>
    private static HttpAttemptOutcome RefusingResendPastRedirectLimit(HttpRequestPlan plan, int statusCode, HttpExchangeLog exchangeLog, HttpAttemptOutcome outcome)
    {
        int limit = plan.Options.MaxRedirects;
        if (outcome.Retry is null || statusCode / 100 == 2 || limit < 0 || plan.RedirectsFollowed < limit)
        {
            return outcome;
        }

        HttpTransferException refusal = new(CurlExitCode.TooManyRedirects, HttpTransferMessages.MaximumRedirectsFollowed(limit));
        exchangeLog.Failed(refusal.ExitCode, refusal);
        TransferReport report = outcome.Result.Report!;
        TransferResult refused = TransferResult.Failure(refusal.ExitCode, refusal.Message, report.DownloadSize) with { Report = report with { RedirectUrl = HttpRedirectLocation.OfResend(plan.Context.Url) } };
        return new HttpAttemptOutcome(refused, null, outcome.KeepsAlive)
        {
            FailedAfterBody = true,
            LeftIntactAfterServerClosed = outcome.LeftIntactAfterServerClosed,
            UpgradedSession = outcome.UpgradedSession,
            RefusedResendMessage = refusal.Message,
        };
    }
}
