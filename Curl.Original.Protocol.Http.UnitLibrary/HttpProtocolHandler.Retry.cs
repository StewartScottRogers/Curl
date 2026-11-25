using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that decides whether a response calls for the request to be sent again.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Decides whether a response is answered with one more request, and which: the same
    /// request with the <c>Proxy-Authorization</c> value <see cref="RetryProxyAuthorizationAsync" />
    /// gives, or with the <c>Authorization</c> value <see cref="RetryAuthorizationAsync" /> gives, or
    /// else, for a 417 <see cref="RetriesWithoutExpect" /> accepts, the same request without
    /// <c>Expect</c> and with the body <paramref name="upload" /> rewinds; <see langword="null" />
    /// when the response is the result. A resend after a 417 that arrived while the body was
    /// being sent keeps an <c>-H</c> <c>Expect: 100-continue</c> wait, so it can draw another
    /// (measured, BL-396 Notes). A resend past <c>--max-redirs</c> is given too, and refused
    /// once the response is done (<see cref="RefusingResendPastRedirectLimit" />).
    /// </summary>
    private async ValueTask<HttpRequestPlan?> RetryOfAsync(HttpRequestPlan plan, HttpResponseHead head, bool bodyLeftUnsent, HttpRequestBodyWriter upload, CancellationToken cancellationToken)
    {
        EndUnchallengedAuthorizations(plan, head.StatusLine.StatusCode);
        if (await RetryProxyAuthorizationAsync(plan, head, cancellationToken).ConfigureAwait(false) is { } proxyAuthorization)
        {
            RewindForResend(plan, upload);
            return plan.WithProxyAuthorization(proxyAuthorization, KeptAuthorizationForProxyRetry(plan)).DowngradedAfter(head).FailingUploadRewind(upload.RewindFailure);
        }

        if (await RetryWithAuthorizationAsync(plan, head, cancellationToken).ConfigureAwait(false) is { } authorized)
        {
            RewindForResend(plan, upload);
            return authorized.DowngradedAfter(head).FailingUploadRewind(upload.RewindFailure);
        }

        if (plan.ProbedFraming is not null && head.StatusLine.StatusCode / 100 == 2)
        {
            return plan.WithProbedBody();
        }

        return RetryWithoutExpect(plan, head, bodyLeftUnsent, upload);
    }

    /// <summary>
    /// Gives the same request without <c>Expect</c> and with the body <paramref name="upload" />
    /// rewinds for a 417 <see cref="RetriesWithoutExpect" /> accepts; <see langword="null" />
    /// for any other response.
    /// </summary>
    private static HttpRequestPlan? RetryWithoutExpect(HttpRequestPlan plan, HttpResponseHead head, bool bodyLeftUnsent, HttpRequestBodyWriter upload)
    {
        if (!RetriesWithoutExpect(plan, head, bodyLeftUnsent || upload.CutShort))
        {
            return null;
        }

        plan.Context.Events.ReportInfo(bodyLeftUnsent ? HttpConnectionInfoLines.Got417WhileWaiting : HttpConnectionInfoLines.Got417WhileSending);
        ReportUploadAbandoned(plan.Context.Events, upload, bodyLeftUnsent);
        return plan.WithoutExpect(upload.Rewound(plan.Framing.Body!), keepsCustomWait: !bodyLeftUnsent).FailingUploadRewind(upload.RewindFailure);
    }

    /// <summary>
    /// Seeks a stream body that <paramref name="upload" /> began reading back to where it
    /// began, so an authentication retry sends it again whole, as curl 8.21.0 rewinds a
    /// <c>-T</c> file or a <c>-F</c> form before it answers a 401 or 407 (upstream test1030,
    /// test259), or one that implements <see cref="IUploadRewinder" /> through it, which keeps a
    /// failed rewind in <see cref="HttpRequestBodyWriter.RewindFailure" />. Any other stream that
    /// cannot seek reaches here only on its way to an HTTP/1.0 server
    /// (<see cref="MayRetry" />), and is left as it is, as that retry fails before sending it
    /// (<see cref="ThrowIfChunkedResendToHttp10" />); a body of bytes needs nothing.
    /// </summary>
    private static void RewindForResend(HttpRequestPlan plan, HttpRequestBodyWriter upload)
    {
        if (plan.Framing.Body is StreamBody stream)
        {
            upload.Rewound(stream);
        }
    }

    /// <summary>
    /// Tells the authenticator that the handshake behind each value <paramref name="plan" />
    /// sent is over when the response is not that value's challenge - not a 401 for the
    /// <c>Authorization</c> value, not a 407 for the <c>Proxy-Authorization</c> value - so a
    /// Negotiate context kept for its next leg is disposed of. The acceptor's final token in a
    /// 2xx is not checked, as curl 8.21.0 reads <c>WWW-Authenticate</c> only on a 401 and
    /// <c>Proxy-Authenticate</c> only on a 407, so the response is the result whatever the
    /// token (ADR-0248).
    /// </summary>
    private void EndUnchallengedAuthorizations(HttpRequestPlan plan, int statusCode)
    {
        if (plan.Authorization is { } sent && statusCode != 401)
        {
            Authenticator.EndAuthorization(sent);
        }

        if (plan.ProxyAuthorization is { } proxySent && statusCode != 407)
        {
            Authenticator.EndAuthorization(proxySent);
        }
    }

    /// <summary>
    /// Decides whether <paramref name="head" /> is answered by resending the request without
    /// <c>Expect</c>, as curl 8.21.0 does (measured, BL-260 and BL-319 Notes): a 417 that
    /// arrived before the whole body was sent - while it waited for <c>100 Continue</c>, or
    /// while it was being sent once the wait ran out - on a connection the 417 leaves open, and
    /// not under <c>-f</c>, which fails on the 417 itself.
    /// </summary>
    private static bool RetriesWithoutExpect(HttpRequestPlan plan, HttpResponseHead head, bool bodyStopped) =>
        bodyStopped
            && head.StatusLine.StatusCode == 417
            && plan.Options.Fail != HttpFailMode.Fail
            && HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, plan.Options.PassesTransferCoding, plan.Options.IgnoreContentLength, plan.Options.TransferEncoding, IsThroughHttpProxy(plan.Options));

    /// <summary>
    /// Decides whether the transfer goes through an HTTP proxy, where curl 8.21.0 reads
    /// <c>Proxy-Connection</c> headers (<c>conn->bits.httpproxy</c> in <c>lib/http.c</c>).
    /// </summary>
    private static bool IsThroughHttpProxy(HttpRequestOptions options) =>
        options.ForwardProxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https };

    /// <summary>
    /// Decides whether <paramref name="head" /> may be answered with a retry at all: a
    /// <paramref name="statusCode" /> response to a request whose body can be sent again: bytes,
    /// none, a stream that implements <see cref="IUploadRewinder" />, or a stream that can seek back
    /// to its start, as a <c>-T</c> file or a <c>-F</c> form
    /// of files can, which curl 8.21.0 rewinds and resends whole (upstream test1030, test259); not
    /// a stream that cannot, such as stdin (ADR-0034), unless the response came from an HTTP/1.0
    /// server, when the retry fails on its new connection (<see cref="ThrowIfChunkedResendToHttp10" />).
    /// </summary>
    private static bool MayRetry(HttpRequestPlan plan, HttpResponseHead head, int statusCode) =>
        head.StatusLine.StatusCode == statusCode
            && (plan.Framing.Body is not StreamBody stream || CanResend(stream.Content) || ResendsChunkedToHttp10(plan, head));

    /// <summary>
    /// Tells whether answering <paramref name="head" /> would resend a body curl chunks for its
    /// unknown length (<see cref="HttpRequestFraming.ChunksForUnknownLength" />) to a server that
    /// answered HTTP/1.0.
    /// </summary>
    private static bool ResendsChunkedToHttp10(HttpRequestPlan plan, HttpResponseHead head) =>
        plan.Framing.ChunksForUnknownLength && head.StatusLine.Version == HttpVersion.Version10;

    /// <summary>
    /// Fails, before any byte of it is sent, a retry that resends a body chunked for its unknown
    /// length to a server that answered HTTP/1.0 (<see cref="HttpRequestPlan.DowngradedAfter" />),
    /// as curl 8.21.0 reconnects, reports the retried request, and fails it while building it on
    /// the new connection with exit 25 <c>Chunky upload is not supported by HTTP 1.0</c>
    /// (BL-2130; upstream test1072).
    /// </summary>
    /// <exception cref="HttpTransferException">The request is such a resend (exit 25).</exception>
    private static void ThrowIfChunkedResendToHttp10(HttpRequestPlan plan)
    {
        if (IsChunkedResendToHttp10(plan))
        {
            throw new HttpTransferException(CurlExitCode.UploadFailed, HttpTransferMessages.ChunkedUploadNeedsHttp11);
        }
    }

    /// <summary>
    /// Tells whether <paramref name="plan" /> resends a body chunked for its unknown length, as
    /// stdin's is, after a response from an HTTP/1.0 server
    /// (<see cref="HttpRequestOptions.EarlierResponseWasHttp10" />): an authentication retry or a
    /// <c>-L</c> hop.
    /// </summary>
    private static bool IsChunkedResendToHttp10(HttpRequestPlan plan) =>
        plan.Framing.ChunksForUnknownLength && plan.Options.EarlierResponseWasHttp10;

    /// <summary>
    /// Gets the values of every <paramref name="name" /> header of <paramref name="head" />,
    /// in received order.
    /// </summary>
    private static string[] ValuesOf(HttpResponseHead head, string name) =>
        [.. head.Headers
            .Where(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)];
}
