using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Applies <c>-C</c>/<c>--continue-at</c>, <c>-z</c>/<c>--time-cond</c> and
/// <c>--max-filesize</c> to a final response head, before its body is read, as curl 8.21.0 does
/// (measured, BL-178 Notes).
/// </summary>
internal static class HttpDownloadConditions
{
    private const int NotModified = 304;

    private const int RangeNotSatisfiable = 416;

    private const string EntireDocumentAlreadyDownloaded = "The entire document is already downloaded";

    private const string DocumentNotNewEnough = "The requested document is not new enough";

    private const string DocumentNotOldEnough = "The requested document is not old enough";

    private const string SimulateNotModified = "Simulate an HTTP 304 response";

    /// <summary>
    /// Gives the body size <c>--max-filesize</c> allows: <see langword="null" /> when none was
    /// given or it is zero, which curl also takes as no limit.
    /// </summary>
    /// <param name="maxFileSize">The <c>--max-filesize</c> value.</param>
    /// <returns>The limit, or <see langword="null" /> for none.</returns>
    internal static long? LimitOf(long? maxFileSize) => maxFileSize is > 0 ? maxFileSize : null;

    /// <summary>
    /// Ends the transfer with exit 63 when the head's Content-Length is over the
    /// <c>--max-filesize</c> limit. curl checks it after <c>-f</c> and with <c>-I</c> too.
    /// </summary>
    /// <param name="maxFileSize">The <c>--max-filesize</c> value.</param>
    /// <param name="head">The final response's head.</param>
    /// <exception cref="HttpTransferException">
    /// The Content-Length is over the limit (exit 63), or invalid (exit 8).
    /// </exception>
    internal static void ThrowIfContentLengthExceeds(long? maxFileSize, HttpResponseHead head)
    {
        if (LimitOf(maxFileSize) is { } limit && HttpContentLength.Find(head.Headers) > limit)
        {
            throw new HttpTransferException(CurlExitCode.FilesizeExceeded, HttpTransferMessages.MaximumFileSizeExceeded);
        }
    }

    /// <summary>
    /// Decides whether the body of a response that is not being discarded is delivered.
    /// </summary>
    /// <remarks>
    /// Under <c>-z</c> a 304 delivers nothing. Otherwise, for anything but <c>-I</c>: a resume of
    /// a request without a body delivers nothing on a 416 or when the Content-Length is the
    /// resume offset, delivers the body when a <c>Content-Range</c> honours it, and fails with
    /// exit 33 otherwise; and with no range or resume, <c>-z</c> delivers nothing when the
    /// response's <c>Last-Modified</c> fails the condition.
    /// </remarks>
    /// <param name="context">The transfer.</param>
    /// <param name="sendsBody"><see langword="true" /> when the request has a body.</param>
    /// <param name="head">The final response's head.</param>
    /// <returns>What becomes of the body.</returns>
    /// <exception cref="HttpTransferException">The resume was not honoured (exit 33).</exception>
    internal static HttpBodyDelivery Decide(ITransferContext context, bool sendsBody, HttpResponseHead head)
    {
        if (context.TimeCondition is not null && head.StatusLine.StatusCode == NotModified)
        {
            return HttpBodyDelivery.TimeConditionUnmet;
        }

        if (context.NoBody)
        {
            return HttpBodyDelivery.Deliver;
        }

        long resumeFrom = context.ResumeFrom.GetValueOrDefault();
        return resumeFrom > 0 ? DecideResume(head, resumeFrom, sendsBody) : DecideTimeCondition(context, head);
    }

    /// <summary>
    /// Reports the <c>-v</c> lines curl 8.21.0's <c>http_firstwrite</c> writes, before the head's
    /// empty line, for a body it leaves unread although the response has one (measured, BL-1398
    /// Notes): <c>The entire document is already downloaded</c> for a resume at the
    /// Content-Length, and <c>The requested document is not new enough</c> (or <c>not old
    /// enough</c>) then <c>Simulate an HTTP 304 response</c> for a <c>Last-Modified</c> that fails
    /// <c>-z</c>. A 416 or a real 304 writes neither.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <param name="head">The final response's head.</param>
    /// <param name="delivery">What <see cref="Decide" /> made of the body.</param>
    internal static void ReportUndeliveredBody(ITransferContext context, HttpResponseHead head, HttpBodyDelivery delivery)
    {
        int statusCode = head.StatusLine.StatusCode;
        if (delivery == HttpBodyDelivery.NothingLeftToResume && statusCode != RangeNotSatisfiable)
        {
            context.Events.ReportInfo(EntireDocumentAlreadyDownloaded);
        }
        else if (delivery == HttpBodyDelivery.TimeConditionUnmet && statusCode != NotModified)
        {
            context.Events.ReportInfo(context.TimeCondition!.Kind == TimeConditionKind.IfUnmodifiedSince ? DocumentNotOldEnough : DocumentNotNewEnough);
            context.Events.ReportInfo(SimulateNotModified);
        }
    }

    /// <summary>
    /// Determines whether the server's own answer, not a body curl chose to leave, ends the
    /// exchange: a 416 to a resume, whose body is read and ignored, or a real 304 under
    /// <c>-z</c>, which has none. curl 8.21.0 leaves the connection intact after either, but
    /// closes it after a resume at the Content-Length or a <c>Last-Modified</c> that fails
    /// <c>-z</c>, whose body it never reads (measured, BL-1412 Notes).
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <param name="delivery">What <see cref="Decide" /> made of the body.</param>
    /// <returns><see langword="true" /> for a 416 to a resume or a real 304 under <c>-z</c>.</returns>
    internal static bool IsServerAnswer(HttpResponseHead head, HttpBodyDelivery delivery) =>
        (delivery, head.StatusLine.StatusCode) is (HttpBodyDelivery.NothingLeftToResume, RangeNotSatisfiable)
            or (HttpBodyDelivery.TimeConditionUnmet, NotModified);

    /// <summary>
    /// Determines whether a document last modified at <paramref name="documentTime" /> meets
    /// <paramref name="condition" />: newer than its time for <c>-z date</c>, older for
    /// <c>-z -date</c>, so an equal time meets neither; an unknown time always meets it.
    /// </summary>
    /// <param name="condition">The <c>-z</c> condition.</param>
    /// <param name="documentTime">
    /// The document's last-modified time in seconds since the Unix epoch, or
    /// <see langword="null" />. It is compared with <see cref="TimeCondition.ValueUnixSeconds" />,
    /// as curl 8.21.0's <c>Curl_meets_timecondition</c> compares <c>time_t</c>s, so a year past
    /// 9999 compares too (ADR-0410).
    /// </param>
    /// <returns><see langword="true" /> when the body is to be delivered.</returns>
    internal static bool IsMet(TimeCondition condition, long? documentTime) =>
        documentTime is not { } time
        || (condition.Kind == TimeConditionKind.IfUnmodifiedSince ? time < condition.ValueUnixSeconds : time > condition.ValueUnixSeconds);

    /// <summary>
    /// Compares the response's <c>Last-Modified</c> with the <c>-z</c> condition, when there is
    /// one and no range was asked for.
    /// </summary>
    private static HttpBodyDelivery DecideTimeCondition(ITransferContext context, HttpResponseHead head) =>
        context.TimeCondition is { } condition && context.RangeText is null && !IsMet(condition, HttpLastModified.Find(head))
            ? HttpBodyDelivery.TimeConditionUnmet
            : HttpBodyDelivery.Deliver;

    /// <summary>
    /// Decides a resume: a request with a body is not checked, as curl checks only a GET.
    /// </summary>
    private static HttpBodyDelivery DecideResume(HttpResponseHead head, long resumeFrom, bool sendsBody)
    {
        if (sendsBody)
        {
            return HttpBodyDelivery.Deliver;
        }

        if (head.StatusLine.StatusCode == RangeNotSatisfiable)
        {
            return HttpBodyDelivery.NothingLeftToResume;
        }

        if (HttpContentRange.HonoursResume(head, resumeFrom))
        {
            return HttpBodyDelivery.Deliver;
        }

        return HttpContentLength.Find(head.Headers) == resumeFrom
            ? HttpBodyDelivery.NothingLeftToResume
            : throw new HttpTransferException(CurlExitCode.RangeError, HttpTransferMessages.ResumeNotSupported);
    }
}
