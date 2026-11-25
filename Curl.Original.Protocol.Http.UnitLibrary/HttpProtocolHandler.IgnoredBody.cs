using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that recognises and reports a response body that is read and not delivered.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reports that a body is read only to be discarded, as curl 8.21.0 does before the
    /// head's empty line (measured, BL-449 Notes): <c>Ignoring the response-body</c> when the
    /// body is discarded - a redirect <c>-L</c> follows, or a response answered with a retry -
    /// on a connection that stays open, and then <c>setting size while ignoring</c> when its
    /// length is known from a Content-Length. Nothing when the connection closes after it, as
    /// it does once the request body was cut short (measured, BL-1446 Notes): curl reads no
    /// such body at all.
    /// </summary>
    private static void ReportIgnoredBody(HttpRequestPlan plan, HttpResponseHead head, bool discardsBody)
    {
        HttpRequestOptions options = plan.Options;
        if (!discardsBody
            || !HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, options.PassesTransferCoding, options.IgnoreContentLength, options.TransferEncoding, IsThroughHttpProxy(options)))
        {
            return;
        }

        ITransferEvents events = plan.Context.Events;
        events.ReportInfo(HttpConnectionInfoLines.IgnoringBody);
        if (IgnoredBodyLength(plan, head) is not null)
        {
            events.ReportInfo(HttpConnectionInfoLines.SettingSizeWhileIgnoring);
        }
    }

    /// <summary>
    /// Tells whether the response's body is read and discarded: one <c>-L</c> or a retry
    /// discards, or a 416's to a resume (<see cref="HttpDownloadConditions.IsServerAnswer" />),
    /// which curl 8.21.0 reads and ignores, writing <c>setting size while ignoring</c> before
    /// the head's empty line when a Content-Length gives its size (measured, BL-1412 Notes). A
    /// real 304 under <c>-z</c> has no body to read.
    /// </summary>
    private static bool IgnoresBody(HttpRequestPlan plan, HttpResponseHead head, HttpBodyDelivery delivery, bool discardsBody)
    {
        if (discardsBody)
        {
            return true;
        }

        if (!HttpDownloadConditions.IsServerAnswer(head, delivery))
        {
            return false;
        }

        if (HttpResponseBodyReader.HasBody(head, plan.Context.NoBody) && IgnoredBodyLength(plan, head) is not null)
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.SettingSizeWhileIgnoring);
        }

        return true;
    }

    /// <summary>
    /// Gives the Content-Length of a body read only to be discarded, or <see langword="null" />
    /// when it has none it stops at: framed as <see cref="HttpResponseBodyFraming" /> says when
    /// the response has a body, and for a response to HEAD, which has none, its Content-Length
    /// unless it is chunked or <c>--ignore-content-length</c> is given (measured, BL-449 Notes).
    /// </summary>
    private static long? IgnoredBodyLength(HttpRequestPlan plan, HttpResponseHead head)
    {
        HttpRequestOptions options = plan.Options;
        if (HttpResponseBodyReader.HasBody(head, plan.Context.NoBody))
        {
            return HttpResponseBodyFraming.Of(HttpResponseBodyReader.FramingHeadersOf(head), options.PassesTransferCoding, options.IgnoreContentLength, options.TransferEncoding).ContentLength;
        }

        return options.IgnoreContentLength || HttpTransferEncoding.ListsChunked(head.Headers) ? null : HttpContentLength.Find(head.Headers);
    }

    /// <summary>
    /// Decides whether a discarded body still counts in the progress meter: the body of a 3xx
    /// that <c>-L</c> follows does, as curl 8.22.0's meter row for the hop reads the body's
    /// length and bytes (measured, CGR-218); a body ignored for a retry does not.
    /// </summary>
    private static bool ReportsIgnoredBodyProgress(HttpRequestPlan plan, HttpResponseHead head) =>
        plan.Options.FollowRedirects && head.StatusLine.StatusCode is >= 300 and < 400;
}
