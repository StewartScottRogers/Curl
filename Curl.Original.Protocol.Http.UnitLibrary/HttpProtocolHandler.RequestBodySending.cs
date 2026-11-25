using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that sends the request body.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Sends the request body as <see cref="SendBodyAsync" /> does, and holds a send failure after a
    /// final 2xx answered the wait for <c>100 Continue</c> instead of throwing it, so that response is
    /// read and delivered first, as curl 8.22.0 does (measured, BL-4909).
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
    /// Throws the send failure <see cref="SendBodyBefore2xxAsync" /> held once the 2xx that answered
    /// the wait for <c>100 Continue</c> has been delivered, unless that response names
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
    /// <see langword="true" /> when a final status arrived during the wait and the body was left
    /// unsent; <see langword="false" /> when the body was sent or there is none.
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
