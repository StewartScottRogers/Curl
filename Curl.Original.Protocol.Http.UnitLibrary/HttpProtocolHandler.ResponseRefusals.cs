using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that fails a transfer whose request framing, authorization or response head is refused.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Fails a request before any byte of it is sent, as curl 8.21.0 does once connected: a
    /// <c>-T</c> upload whose <c>-C</c> offset cannot be resumed from (exit 18 or 26, measured,
    /// BL-332 Notes), and an HTTP/1.0 request whose body length is unknown (exit 25, measured,
    /// BL-180 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// <see cref="HttpRequestFraming.ResumeFailure" /> or
    /// <see cref="HttpRequestFraming.RefusesUnknownLength" /> is set.
    /// </exception>
    private static void ThrowIfRefused(HttpRequestFraming framing)
    {
        if (framing.ResumeFailure is { } resumeFailure)
        {
            throw resumeFailure;
        }

        if (framing.RefusesUnknownLength)
        {
            throw new HttpTransferException(CurlExitCode.UploadFailed, HttpTransferMessages.ChunkedUploadNeedsHttp11);
        }
    }

    /// <summary>
    /// Fails the exchange, before the request is sent, with the authenticator's refusal to make
    /// the first request's value (<see cref="HttpRequestPlan.AuthorizationFailure" />), as
    /// curl 8.21.0 fails an <c>--aws-sigv4</c> it cannot sign with once connected (measured, BL-629 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator refused.</exception>
    private static void ThrowIfAuthorizationFailed(HttpRequestPlan plan)
    {
        if (plan.AuthorizationFailure is { } failure)
        {
            throw failure;
        }
    }

    /// <summary>
    /// Gives the part of <paramref name="head" /> curl 8.21.0 reads, writes and reports: all of
    /// it, or the part before the header it refused (<see cref="HttpResponseHead.Before" />).
    /// </summary>
    private static HttpResponseHead HeadCurlRead(HttpResponseHead head, HttpHeadRefusal? refusal) =>
        refusal is null ? head : head.Before(refusal.HeaderIndex);

    /// <summary>
    /// Fails the transfer with the failure of the header curl refused while reading the head
    /// (<see cref="HttpResponseBodyReader.FindHeadRefusal" />), once the head before it is
    /// written.
    /// </summary>
    /// <exception cref="HttpTransferException"><paramref name="refusal" /> is not <see langword="null" />.</exception>
    private static void ThrowIfHeaderRefused(HttpHeadRefusal? refusal)
    {
        if (refusal is not null)
        {
            throw refusal.Failure;
        }
    }

    /// <summary>
    /// Fails a head the peer closed among its headers when the part curl 8.21.0 acted on frames
    /// a body by a Content-Length above zero: exit 18, <c>transfer closed with N bytes remaining
    /// to read</c>, its <c>-v</c> line reported before the head's last header line and ahead of
    /// any <c>-f</c> failure (measured, BL-485 Notes). A body a response has none of -
    /// <c>-I</c>, 204, 304 - and <c>--ignore-content-length</c> fail nothing.
    /// </summary>
    private static void ThrowIfClosedBeforeContentLength(HttpRequestPlan plan, HttpResponseHead head, HttpResponseHeadReader headReader)
    {
        if (headReader.EndedAtEmptyLine || !HttpResponseBodyReader.HasBody(head, plan.Context.NoBody))
        {
            return;
        }

        HttpRequestOptions options = plan.Options;
        if (HttpResponseBodyFraming.Of(HttpResponseBodyReader.FramingHeadersOf(head), options.PassesTransferCoding, options.IgnoreContentLength, options.TransferEncoding).ContentLength is > 0 and { } remaining)
        {
            string message = HttpTransferMessages.TransferClosedWithBytesRemaining(remaining);
            plan.Context.Events.ReportInfo(message);
            throw new HttpTransferException(CurlExitCode.PartialFile, message);
        }
    }
}
