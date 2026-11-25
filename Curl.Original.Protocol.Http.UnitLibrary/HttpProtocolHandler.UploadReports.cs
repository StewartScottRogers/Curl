using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that reports what became of the request body in the -v lines.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reports <c>Need to rewind upload for next request</c> after a <c>3xx</c> status line under
    /// <c>-L</c> when the request sent a body that is not empty, as curl 8.21.0 does whatever the
    /// redirect then does with the body: a <c>307</c> sends it again, a <c>302</c> drops it
    /// (measured, BL-1213 Notes). When the status line arrived while the body was being sent,
    /// <c>close instead of sending N more bytes</c> follows it (measured, BL-1527 Notes). Nothing
    /// while the body still waits for <c>100 Continue</c> (measured, BL-5960 Notes), or once a
    /// status on an HTTP/2 or HTTP/3 stream has left it unsent (measured, BL-5970 Notes).
    /// </summary>
    private static void ReportUploadRewind(HttpRequestPlan plan, HttpStatusLine statusLine, HttpRequestBodyWriter upload)
    {
        if (plan.Options.FollowRedirects
            && statusLine.StatusCode is >= 300 and < 400
            && SendsBodyBeforeTheRedirect(plan, upload))
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.NeedToRewindUpload);
            ReportUploadClosed(plan.Context.Events, plan.Framing.KnownLength, upload);
        }
    }

    /// <summary>
    /// Tells whether the request has a body, not empty, that is neither still waiting for
    /// <c>100 Continue</c> nor left unsent on a stream.
    /// </summary>
    private static bool SendsBodyBeforeTheRedirect(HttpRequestPlan plan, HttpRequestBodyWriter upload) =>
        plan.Framing.Body is not null
            && plan.Framing.KnownLength != 0
            && upload.EarlyResponseWatch is not ({ HoldsBody: true } or { StoppedStreamUpload: true });

    /// <summary>
    /// Reports <c>close instead of sending N more bytes</c> when the body was cut short, N being
    /// what was left of <paramref name="knownLength" />, or <c>unknown amount of</c> without one.
    /// </summary>
    private static void ReportUploadClosed(ITransferEvents events, long? knownLength, HttpRequestBodyWriter upload)
    {
        if (upload.CutShort)
        {
            events.ReportInfo(HttpConnectionInfoLines.CloseInsteadOfSending(knownLength, upload.BytesSent));
        }
    }

    /// <summary>
    /// Reports what curl 8.21.0 writes before the head's empty line when a final status arrived
    /// while the request body was being sent and cut it short (measured, BL-1527 Notes): nothing
    /// for one answered with a resend, which writes its own lines; <c>Keep sending data to get
    /// tossed away</c> for a redirect <c>-L</c> follows; and otherwise <c>HTTP error before end of
    /// send, stop sending</c> and <c>abort upload after having sent N bytes</c>. A body stopped
    /// while it still waited for <c>100 Continue</c> writes the last two, ending <c>abort
    /// upload</c>, for a followed redirect too (measured, BL-5960 Notes).
    /// </summary>
    private static void ReportUploadStopped(ITransferEvents events, HttpRequestBodyWriter upload, HttpRequestPlan? retry, bool discardsBody)
    {
        if (!upload.CutShort || retry is not null)
        {
            return;
        }

        if (discardsBody && !upload.StoppedBeforeSending)
        {
            events.ReportInfo(HttpConnectionInfoLines.KeepSendingToTossAway);
            return;
        }

        ReportStopBeforeEndOfSend(events, upload);
    }

    /// <summary>Reports <c>HTTP error before end of send, stop sending</c> and the <c>abort upload</c> line.</summary>
    private static void ReportStopBeforeEndOfSend(ITransferEvents events, HttpRequestBodyWriter upload)
    {
        events.ReportInfo(HttpConnectionInfoLines.StopSendingBeforeEndOfSend);
        events.ReportInfo(HttpConnectionInfoLines.AbortUpload(upload.BytesSent));
    }

    /// <summary>
    /// Reports that the request went out, as curl 8.21.0 does (measured, BL-407 Notes):
    /// <c>Request completely sent off</c> for a request without a body or whose body put no
    /// bytes on the wire (an empty <c>-d ''</c> or <c>-T</c> file, BL-1216 Notes), and
    /// <c>upload completely sent off: N bytes</c> once a whole body has been sent; nothing for
    /// a body a final status stopped.
    /// </summary>
    private static void ReportRequestSent(ITransferEvents events, HttpRequestFraming framing, HttpRequestBodyWriter upload, bool bodyLeftUnsent)
    {
        if (framing.Body is null)
        {
            events.ReportInfo(HttpConnectionInfoLines.RequestSent);
        }
        else if (!bodyLeftUnsent && !upload.CutShort)
        {
            events.ReportInfo(upload.BytesSent == 0 ? HttpConnectionInfoLines.RequestSent : HttpConnectionInfoLines.UploadSent(upload.BytesSent));
        }
    }

    /// <summary>
    /// Reports what curl writes after <c>Got HTTP failure 417 while ...</c>, before the 417's
    /// empty line. After a 417 that arrived while the body was being sent:
    /// <c>Need to rewind upload for next request</c> and <c>abort upload after having sent N
    /// bytes</c> (curl 8.21.0, measured, BL-1446 Notes). After one that arrived while the body
    /// still waited for <c>100 Continue</c>, so none of it was sent: only <c>abort upload</c>
    /// (curl 8.22.0, measured, BL-5952 Notes).
    /// </summary>
    private static void ReportUploadAbandoned(ITransferEvents events, HttpRequestBodyWriter upload, bool bodyLeftUnsent)
    {
        if (!bodyLeftUnsent)
        {
            events.ReportInfo(HttpConnectionInfoLines.NeedToRewindUpload);
        }

        events.ReportInfo(HttpConnectionInfoLines.AbortUpload(upload.BytesSent));
    }
}
