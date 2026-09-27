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
    /// Determines whether a document last modified at <paramref name="documentTime" /> meets
    /// <paramref name="condition" />: newer than its time for <c>-z date</c>, older for
    /// <c>-z -date</c>, so an equal time meets neither; an unknown time always meets it.
    /// </summary>
    /// <param name="condition">The <c>-z</c> condition.</param>
    /// <param name="documentTime">The document's last-modified time, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the body is to be delivered.</returns>
    internal static bool IsMet(TimeCondition condition, DateTimeOffset? documentTime) =>
        documentTime is not { } time
        || (condition.Kind == TimeConditionKind.IfUnmodifiedSince ? time < condition.Value : time > condition.Value);

    /// <summary>
    /// Compares the response's <c>Last-Modified</c> with the <c>-z</c> condition, when there is
    /// one and no range was asked for.
    /// </summary>
    private static HttpBodyDelivery DecideTimeCondition(ITransferContext context, HttpResponseHead head) =>
        context.TimeCondition is { } condition && context.Range is null && !IsMet(condition, HttpLastModified.Find(head))
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
