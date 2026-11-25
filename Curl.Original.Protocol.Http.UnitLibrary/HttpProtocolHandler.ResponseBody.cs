using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that reads, decodes and delivers the response body and its trailers.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Applies <c>--max-filesize</c> to the head's Content-Length of a body that is kept, then
    /// decides what becomes of it (<see cref="HttpDownloadConditions" />). A discarded body - a
    /// redirect <c>-L</c> follows, a 401 before its retry - is not held to the limit, as curl
    /// 8.21.0 holds only the body it keeps to it (upstream test477, BL-1807).
    /// </summary>
    /// <exception cref="HttpTransferException">
    /// The Content-Length is over the limit (exit 63), or a resume was not honoured (exit 33).
    /// </exception>
    private static HttpBodyDelivery DeliveryOf(HttpRequestPlan plan, HttpResponseHead head, bool discardsBody)
    {
        HttpDownloadConditions.ThrowIfContentLengthExceeds(discardsBody ? null : plan.Context.MaxFileSize, head);
        return discardsBody ? HttpBodyDelivery.Deliver : HttpDownloadConditions.Decide(plan.Context, plan.Framing.Body is not null, head);
    }

    /// <summary>
    /// Reads the body into the transfer's output, or into nothing when it is discarded, as
    /// <see cref="SetBodyLimitAndSinks" /> sets it up, then writes a chunked body's trailers; or reads
    /// nothing when <paramref name="delivery" /> says there is no body to deliver and it is not
    /// being discarded (a 416's is, <see cref="IgnoresBody" />). Over HTTP/2 and HTTP/3
    /// the trailers are the stream's trailing field section, read once the stream has ended. An
    /// HTTP/2 stream's discarded body is not delivered through the body reader: the stream is given
    /// up (<see cref="Http2StreamConnection.AbandonResponseAsync" />), which reads it to its end
    /// or resets it, as curl does (BL-970, BL-5906).
    /// </summary>
    private static ValueTask ReadBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpResponseBodyReader body,
        IHttpStreamConnection? requestStream,
        HttpBodyDelivery delivery,
        bool discardsBody,
        CancellationToken cancellationToken)
    {
        if ((delivery != HttpBodyDelivery.Deliver && !discardsBody) || AbandonsBody(plan, head, requestStream, delivery, discardsBody))
        {
            return ValueTask.CompletedTask;
        }

        return discardsBody && requestStream is Http2StreamConnection http2Stream
            ? http2Stream.AbandonResponseAsync(cancellationToken)
            : CopyBodyAsync(plan, head, body, requestStream, discardsBody, cancellationToken);
    }

    /// <summary>
    /// Decides whether an HTTP/1.x body discarded for a retry or a followed redirect is not read
    /// at all because the connection closes after it, as curl 8.21.0 stops reading once the head
    /// is in when it has a new request to make and the connection is to close anyway - so a 401
    /// with <c>Connection: close</c> and no length is answered at once, not when the server
    /// closes (measured, BL-2001 Notes). A 416's ignored body (<paramref name="delivery" /> not
    /// <see cref="HttpBodyDelivery.Deliver" />) is still read.
    /// </summary>
    private static bool AbandonsBody(HttpRequestPlan plan, HttpResponseHead head, IHttpStreamConnection? requestStream, HttpBodyDelivery delivery, bool discardsBody)
    {
        HttpRequestOptions options = plan.Options;
        return discardsBody
            && delivery == HttpBodyDelivery.Deliver
            && requestStream is null
            && !HttpConnectionPersistence.KeepsAlive(head, plan.Context.NoBody, options.PassesTransferCoding, options.IgnoreContentLength, options.TransferEncoding, IsThroughHttpProxy(options));
    }

    /// <summary>
    /// Copies the body into the transfer's output, or into nothing when it is discarded, then
    /// writes its trailers (<see cref="ReadBodyAsync" />).
    /// </summary>
    private static async ValueTask CopyBodyAsync(
        HttpRequestPlan plan,
        HttpResponseHead head,
        HttpResponseBodyReader body,
        IHttpStreamConnection? requestStream,
        bool discardsBody,
        CancellationToken cancellationToken)
    {
        ITransferContext context = plan.Context;
        Stream bodyOutput = discardsBody ? Stream.Null : context.Output;
        SetBodyLimitAndSinks(plan, body, discardsBody, ReportsIgnoredBodyProgress(plan, head));
        body.Http2Stream = requestStream as Http2StreamConnection;
        HttpTransferException? failedTransfer = null;
        try
        {
            await body.CopyAsync(head, context.NoBody, bodyOutput, DecodesContent(plan.Options, discardsBody), plan.Options.TransferEncoding && !discardsBody, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpTransferException failure)
        {
            failedTransfer = failure;
        }

        if (failedTransfer is not null)
        {
            // curl writes the trailers it stored before the one that failed: the one past the limit
            // (8.21.0, BL-1448 Notes) or a malformed one (8.22.0, CGR-202).
            await WriteHeadersAsync(context.HeaderOutput, body.TrailerBytes, cancellationToken).ConfigureAwait(false);
            throw failedTransfer;
        }

        ReadOnlyMemory<byte> trailers = await TrailersOfAsync(body, requestStream, cancellationToken).ConfigureAwait(false);
        int storedLength = StoredLengthOf(trailers.Span, requestStream is null ? int.MaxValue : HttpResponseHeadReader.MaximumHeaderCount - body.HeadersStored);
        await WriteHeadersAsync(context.HeaderOutput, trailers[..storedLength], cancellationToken).ConfigureAwait(false);
        ReportStreamTrailers(body, requestStream, context.Events, trailers.Span[..storedLength]);
        ReportEmptyStreamData(body.Http2Stream, context.Events);
        if (storedLength < trailers.Length)
        {
            throw new HttpTransferException(CurlExitCode.TooLarge, HttpTransferMessages.TooManyResponseHeaders)
            {
                InfoLines = [HttpTransferMessages.TooManyResponseHeaders],
            };
        }
    }

    /// <summary>
    /// Reports each line of an HTTP/2 or HTTP/3 stream's stored trailers as a received header, as curl does for
    /// the headers its HTTP/2 layer hands on (CGR-273), and keeps them as header pairs for <c>%{num_headers}</c>;
    /// does nothing for a body that is not a stream's.
    /// </summary>
    private static void ReportStreamTrailers(HttpResponseBodyReader body, IHttpStreamConnection? requestStream, ITransferEvents events, ReadOnlySpan<byte> trailers)
    {
        if (requestStream is null)
        {
            return;
        }

        List<KeyValuePair<string, string>> pairs = [];
        foreach (string line in System.Text.Encoding.Latin1.GetString(trailers).Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            events.ReportResponseHeader(System.Text.Encoding.Latin1.GetBytes(line + "\r\n"));
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            pairs.Add(KeyValuePair.Create(line[..colon], line[(colon + 2)..]));
        }

        body.StreamTrailers = pairs;
    }

    /// <summary>
    /// Reports the empty data that ends an HTTP/2 response, after its trailers, which <c>-v</c>
    /// shows as <c>{ [0 bytes data]</c> (<see cref="Http2StreamConnection.EndedWithEmptyData" />):
    /// a response with no body, or one a trailer ends (measured, curl 8.18.0, BL-5946 Notes).
    /// The body reader leaves it to this method (<see cref="HttpResponseBodyReader.Http2Stream" />),
    /// so it is reported once. A discarded body never comes here, and nothing is reported for a
    /// head that ended the stream while the request body was held
    /// (<see cref="Http2StreamConnection.LeavesEmptyDataUnreported" />).
    /// </summary>
    private static void ReportEmptyStreamData(Http2StreamConnection? http2Stream, ITransferEvents events)
    {
        if (http2Stream is { EndedWithEmptyData: true, LeavesEmptyDataUnreported: false })
        {
            events.ReportDataReceived([]);
        }
    }

    /// <summary>
    /// Gives the length of the first <paramref name="allowed" /> lines of <paramref name="trailers" />, all of
    /// them when there are no more: the HTTP/2 or HTTP/3 trailers curl 8.21.0 stores before the one past its
    /// limit of 5000 response headers, which it counts as it counts chunked trailers (BL-1609 Notes, ADR).
    /// A chunked body's trailers were already cut by <see cref="HttpChunkedDecoder.TrailerLimit" />.
    /// </summary>
    internal static int StoredLengthOf(ReadOnlySpan<byte> trailers, int allowed)
    {
        int length = 0;
        for (int line = 0; line < allowed && length < trailers.Length; line++)
        {
            int end = trailers[length..].IndexOf((byte)'\n');
            length = end < 0 ? trailers.Length : length + end + 1;
        }

        return length;
    }

    /// <summary>
    /// Gives the trailers of a body just read: a chunked body's, or an HTTP/2 or HTTP/3 stream's once it
    /// has been read to its end (<see cref="IHttpStreamConnection.ReadToEndAsync" />).
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>> TrailersOfAsync(HttpResponseBodyReader body, IHttpStreamConnection? requestStream, CancellationToken cancellationToken)
    {
        if (requestStream is null)
        {
            return body.TrailerBytes;
        }

        await requestStream.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return requestStream.TrailerBytes;
    }

    /// <summary>
    /// Sets where a body's bytes are held and reported: to the <c>--max-filesize</c> limit, the
    /// progress meter and the transfer's events, or, for a discarded body, to none of them -
    /// curl 8.21.0 reports no data for a body it ignores (measured, BL-407 Notes).
    /// </summary>
    private static void SetBodyLimitAndSinks(HttpRequestPlan plan, HttpResponseBodyReader body, bool discardsBody, bool reportsProgress)
    {
        body.MaximumBodySize = discardsBody ? null : HttpDownloadConditions.LimitOf(plan.Context.MaxFileSize);
        body.Progress = discardsBody && !reportsProgress ? HttpTransferProgress.Silent : plan.Progress;
        body.Events = discardsBody ? NoTransferEvents.Instance : plan.Context.Events;
    }

    /// <summary>
    /// Builds a successful exchange's result, carrying the <c>Last-Modified</c> time for
    /// <c>-R</c>: an unmet <c>-z</c> condition is marked so and reported as a 304, as curl
    /// 8.21.0's <c>%{response_code}</c> does for a response whose <c>Last-Modified</c> fails it.
    /// </summary>
    private static TransferResult Succeeded(HttpBodyDelivery delivery, HttpResponseHead head, TransferReport report)
    {
        long? lastModified = HttpLastModified.FindFileTime(head);
        return delivery == HttpBodyDelivery.TimeConditionUnmet
            ? TransferResult.TimeConditionNotMet() with { SourceLastWriteUnixSeconds = lastModified, Report = report with { ResponseCode = 304 } }
            : TransferResult.Success(report.DownloadSize) with { SourceLastWriteUnixSeconds = lastModified, Report = report };
    }

    /// <summary>
    /// Decides whether the body is decoded: for <c>--compressed</c> without <see cref="HttpRequestOptions.PassesContentCoding" /> (<c>--raw</c>, <c>CURLOPT_HTTP_CONTENT_DECODING</c> 0), and
    /// not for a body that is read and discarded - a 3xx body <c>-L</c> follows past, which
    /// curl 8.21.0 does not decode (measured, BL-177 Notes), or a 401 body an authentication
    /// retry follows.
    /// </summary>
    private static bool DecodesContent(HttpRequestOptions options, bool discardsBody) =>
        DecodesContent(options) && !discardsBody;

    /// <summary>
    /// Decides whether Content-Encoding is decoded at all: for <c>--compressed</c> without
    /// <see cref="HttpRequestOptions.PassesContentCoding" /> (<c>--raw</c>, <c>CURLOPT_HTTP_CONTENT_DECODING</c> 0), which is also when curl 8.21.0 holds the response to
    /// <see cref="HttpContentDecoder.MaximumCodings" /> (measured, BL-364 Notes).
    /// </summary>
    private static bool DecodesContent(HttpRequestOptions options) =>
        options.Compressed && !options.PassesContentCoding;
}
