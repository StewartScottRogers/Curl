using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// How one request carries its body, as curl 8.21.0 decides it: the method, whether the body
/// is sent chunked, and whether the request waits for <c>100 Continue</c> before sending it.
/// Every rule was measured (BL-175 Notes).
/// </summary>
/// <remarks>
/// A request with a <c>-d</c> or <c>-F</c> body is a POST, and one with a <c>-T</c> upload a
/// PUT, unless <c>-X</c> names another method; one without is a GET, or a HEAD for <c>-I</c>. The body is sent
/// chunked when an <c>-H</c> value asks for <c>Transfer-Encoding: chunked</c>, or when its
/// length is unknown and no <c>-H</c> value names <c>Transfer-Encoding</c> - an emptied
/// <c>-H "Transfer-Encoding:"</c> sends it as it comes, and an <c>-H</c> <c>Content-Length</c>
/// changes nothing (upstream test60, test98) - and with <c>Content-Length</c> otherwise. curl adds
/// <c>Expect: 100-continue</c> when the length is unknown or above
/// <see cref="ExpectContinueThreshold" />, unless an <c>-H</c> value names <c>Expect</c>; the
/// request waits for <c>100 Continue</c> whenever it carries <c>Expect: 100-continue</c>,
/// its own or an <c>-H</c> one. An HTTP/1.0 request (<c>-0</c>) never gets curl's own
/// <c>Expect</c>, and one whose body length is unknown with no <c>-H</c> value naming
/// <c>Transfer-Encoding</c> is refused (<see cref="RefusesUnknownLength" />; measured, BL-180 Notes).
/// </remarks>
internal sealed class HttpRequestFraming
{
    /// <summary>
    /// The largest body length curl 8.21.0 sends without <c>Expect: 100-continue</c>: 1 MiB.
    /// </summary>
    internal const long ExpectContinueThreshold = 1048576;

    private const string TransferEncodingName = "Transfer-Encoding";

    private HttpRequestFraming(
        string method,
        HttpRequestBody? body,
        long? knownLength,
        bool isChunked,
        bool addsExpect,
        bool awaitsContinue,
        bool refusesUnknownLength = false,
        HttpUploadResume? upload = null,
        string? contentRange = null,
        bool isAuthProbe = false,
        bool chunksForUnknownLength = false)
    {
        Method = method;
        ChunksForUnknownLength = chunksForUnknownLength;
        IsAuthProbe = isAuthProbe;
        Body = body;
        KnownLength = knownLength;
        IsChunked = isChunked;
        AddsExpect = addsExpect;
        AwaitsContinue = awaitsContinue;
        RefusesUnknownLength = refusesUnknownLength;
        Upload = upload;
        ContentRange = contentRange;
    }

    /// <summary>
    /// Gets the request method: <c>-X</c>'s, or POST with a body, HEAD for <c>-I</c> and GET
    /// otherwise.
    /// </summary>
    internal string Method { get; }

    /// <summary>
    /// Gets the body to send, or <see langword="null" /> when there is none.
    /// </summary>
    internal HttpRequestBody? Body { get; }

    /// <summary>
    /// Gets the body's length when it is known, or <see langword="null" /> for a
    /// <see cref="StreamBody" /> of unknown length and when there is no body.
    /// </summary>
    internal long? KnownLength { get; }

    /// <summary>
    /// Gets a value indicating whether the body is sent with chunked transfer coding.
    /// </summary>
    internal bool IsChunked { get; }

    /// <summary>
    /// Gets a value indicating whether curl's own <c>Expect: 100-continue</c> line is sent.
    /// </summary>
    internal bool AddsExpect { get; }

    /// <summary>
    /// Gets a value indicating whether the request waits for <c>100 Continue</c> before
    /// sending the body.
    /// </summary>
    internal bool AwaitsContinue { get; }

    /// <summary>
    /// Gets a value indicating whether curl 8.21.0 refuses to send the request: an HTTP/1.0
    /// request whose body length is unknown, with no <c>-H</c> value naming
    /// <c>Transfer-Encoding</c>. curl connects, sends nothing and fails with exit 25
    /// <c>Chunky upload is not supported by HTTP 1.0</c> (measured, BL-180 Notes).
    /// </summary>
    internal bool RefusesUnknownLength { get; }

    /// <summary>
    /// Gets a value indicating whether the body is sent chunked because its length is unknown and
    /// no <c>-H</c> value names <c>Transfer-Encoding</c>: curl 8.21.0's own chunking, which it
    /// refuses with exit 25 for a request it would send as HTTP/1.0, as after an HTTP/1.0
    /// server's 401 (upstream test1072; <c>http_req_set_reader</c>).
    /// </summary>
    internal bool ChunksForUnknownLength { get; }

    /// <summary>
    /// Gets a value indicating whether the body is the <c>-T</c>/<c>--upload-file</c> source on
    /// <see cref="ITransferContext.Upload" />: sent as PUT with no <c>Content-Type</c>, and
    /// failing a short read with curl's <c>client read function</c> message rather than its
    /// <c>client mime read</c> one (measured, BL-184 Notes).
    /// </summary>
    internal bool IsUpload => Upload is not null;

    /// <summary>
    /// Gets the <c>Content-Range</c> value the request sends, or <see langword="null" /> to send
    /// none: a <c>-T</c> upload resumed with <c>-C</c> sends <see cref="HttpUploadResume" />'s, and
    /// otherwise a <c>-d</c> body or a <c>-T</c> upload with <c>-r</c> sends
    /// <see cref="HttpRangeHeader.ContentRangeFor" />'s. A <c>-F</c> form sends none, as curl sends
    /// none for a multipart post (measured, BL-306 Notes).
    /// </summary>
    internal string? ContentRange { get; }

    /// <summary>
    /// Gets a value indicating whether this is the Digest or NTLM probe <see cref="AsAuthProbe" /> makes:
    /// its <c>Content-Length: 0</c> replaces any <c>-H</c> <c>Content-Length</c> line, as curl
    /// 8.21.0 sends it (upstream test1284, BL-1835).
    /// </summary>
    internal bool IsAuthProbe { get; }

    /// <summary>
    /// Gets the failure a <c>-T</c> upload resumed with <c>-C</c> ends with once connected,
    /// before anything is sent, or <see langword="null" /> when it can be sent
    /// (<see cref="HttpUploadResume" />).
    /// </summary>
    internal HttpTransferException? ResumeFailure => Upload?.Failure;

    /// <summary>
    /// Gets the resumed <c>-T</c> source, or <see langword="null" /> when the body is not one.
    /// </summary>
    private HttpUploadResume? Upload { get; }

    /// <summary>
    /// Makes the same framing without curl's own <c>Expect: 100-continue</c> line: the request
    /// curl 8.21.0 resends after a <c>417 Expectation Failed</c>. An <c>-H</c> <c>Expect</c>
    /// line is still sent. After a 417 that arrived during the wait, the body follows the head
    /// at once (measured, BL-260 Notes); after one that arrived while the body was being sent,
    /// an <c>-H</c> <c>Expect: 100-continue</c> waits for <c>100 Continue</c> again (measured,
    /// BL-396 Notes).
    /// </summary>
    /// <param name="body">
    /// The body to resend: <see cref="Body" />, or the stream it rewinds to (BL-319 Notes).
    /// </param>
    /// <param name="keepsCustomWait">
    /// <see langword="true" /> to keep the wait an <c>-H</c> <c>Expect: 100-continue</c> asks
    /// for; curl's own line and its wait are dropped either way.
    /// </param>
    /// <returns>The framing of the resent request.</returns>
    internal HttpRequestFraming WithoutExpect(HttpRequestBody? body, bool keepsCustomWait = false) =>
        new(Method, body, KnownLength, IsChunked, addsExpect: false, awaitsContinue: keepsCustomWait && AwaitsContinue && !AddsExpect, RefusesUnknownLength, Upload, ContentRange, chunksForUnknownLength: ChunksForUnknownLength);

    /// <summary>
    /// Makes the same request with an empty body and <c>Content-Length: 0</c>: the probe curl
    /// 8.21.0 sends first when Digest is the one scheme allowed and no challenge has been
    /// answered yet, holding the body back for the authenticated request (upstream test88,
    /// test175, test1001; ADR-0441), or when the request starts an NTLM handshake (BL-2029). Its <c>Content-Type</c> stays, though the head leaves a <c>-F</c> form's out (<see cref="HttpRequestHeadFormatter" />); a <c>-T</c> upload still
    /// sends none. Its <c>Content-Range</c> stays too, as curl 8.21.0 sends a resumed <c>-T</c>
    /// upload's probe with it (measured, BL-2002 Notes; upstream test1001). Its
    /// <c>Content-Length: 0</c> replaces any <c>-H</c> one (<see cref="IsAuthProbe" />).
    /// </summary>
    /// <returns>The probe's framing.</returns>
    internal HttpRequestFraming AsAuthProbe() =>
        new(Method, new BytesBody(ReadOnlyMemory<byte>.Empty, Body!.ContentType), 0, isChunked: false, addsExpect: false, awaitsContinue: false, upload: Upload, contentRange: ContentRange, isAuthProbe: true);

    /// <summary>
    /// Makes the same framing for an HTTP/2 or HTTP/3 stream, where DATA frames carry the body and the
    /// stream's end ends it: never chunked, and with no <c>Expect: 100-continue</c> of curl's
    /// own and no wait for <c>100 Continue</c>, as curl 8.21.0 sends a 2 MB <c>-T</c> upload over
    /// HTTP/2 (measured, BL-658 Notes); HTTP/3 carries the body the same way (ADR-0172). An <c>-H</c>
    /// <c>Expect</c> line is still sent.
    /// </summary>
    /// <returns>The framing of the HTTP/2 or HTTP/3 request.</returns>
    internal HttpRequestFraming ForHttp2OrHttp3() =>
        new(Method, Body, KnownLength, isChunked: false, addsExpect: false, awaitsContinue: false, RefusesUnknownLength, Upload, ContentRange, IsAuthProbe);

    /// <summary>
    /// Decides the framing for a request with <paramref name="options" />.
    /// </summary>
    /// <param name="options">The HTTP options.</param>
    /// <param name="customHeaders">The parsed <c>-H</c> values, in command-line order.</param>
    /// <param name="noBody">
    /// <see langword="true" /> for <c>-I</c>/<c>--head</c>, which makes a request without a
    /// body a HEAD unless <c>-X</c> names another method. curl refuses <c>-I</c> with a body
    /// on the command line, so a body keeps its POST here.
    /// </param>
    /// <param name="upload">
    /// The <c>-T</c> source (<see cref="ITransferContext.Upload" />), or <see langword="null" />.
    /// It is sent as PUT unless <c>-X</c> names another method, and takes the place of any
    /// <see cref="HttpRequestOptions.Body" />: curl's command line refuses the two together.
    /// Its length is what is left of it from its position when it can seek, and unknown
    /// otherwise, as for standard input (measured, BL-184 Notes).
    /// </param>
    /// <param name="resumeFrom">
    /// The <c>-C</c> offset (<see cref="ITransferContext.ResumeFrom" />) the upload resumes from,
    /// or <see langword="null" />; <see cref="HttpUploadResume" /> applies it.
    /// </param>
    /// <param name="rangeText">
    /// The <c>-r</c> text as given (<see cref="ITransferContext.RangeText" />), or
    /// <see langword="null" />; a <c>-d</c> body or a <c>-T</c> upload sends it as
    /// <see cref="ContentRange" />.
    /// </param>
    /// <param name="resumeFromUnknownOffset">
    /// <see langword="true" /> for <c>-C -</c> (<see cref="ITransferContext.ResumeUploadFromUnknownOffset" />);
    /// <see cref="HttpUploadResume" /> applies it in place of <paramref name="resumeFrom" />.
    /// </param>
    /// <returns>The framing.</returns>
    /// <param name="convertLineEndings">
    /// <see langword="true" /> for <c>--crlf</c> (<see cref="ITransferContext.ConvertLineEndings" />):
    /// the body is read through <see cref="HttpCrlfUploadStream" /> and, its length unknown, sent
    /// chunked (measured, AF-0125, BL-1875 Notes).
    /// </param>
    internal static HttpRequestFraming Of(HttpRequestOptions options, HttpCustomHeader[] customHeaders, bool noBody = false, Stream? upload = null, long? resumeFrom = null, string? rangeText = null, bool resumeFromUnknownOffset = false, bool convertLineEndings = false)
    {
        if (upload is not null)
        {
            HttpUploadResume resume = HttpUploadResume.Of(upload, resumeFrom, resumeFromUnknownOffset);
            return OfUpload(options, resume, upload, customHeaders, rangeText, convertLineEndings);
        }

        if (options.Body is not { } body)
        {
            return new HttpRequestFraming(options.CustomMethod ?? (noBody ? "HEAD" : "GET"), null, null, false, false, false);
        }

        return OfBody(options.CustomMethod ?? "POST", body, customHeaders, options.Version == HttpVersionPreference.Http10, rangeText: DataRangeOf(body, rangeText), convertLineEndings: convertLineEndings);
    }

    /// <summary>
    /// Gives <paramref name="body" /> read through <see cref="HttpCrlfUploadStream" />, of unknown
    /// length, for <c>--crlf</c>.
    /// </summary>
    private static StreamBody WithCrlfLineEndings(HttpRequestBody body)
    {
        Stream content = body is BytesBody bytes ? new MemoryStream(bytes.Content.ToArray(), writable: false) : ((StreamBody)body).Content;
        return new StreamBody(new HttpCrlfUploadStream(content), null, body.ContentType);
    }

    /// <summary>
    /// Decides the framing for a request that sends the <c>-T</c> source
    /// <paramref name="upload" />: PUT unless <c>-X</c> names another method, of the length
    /// left to send after the <c>-C</c> offset (<paramref name="resume" />), with the <c>-r</c>
    /// <paramref name="rangeText" /> when there is one.
    /// </summary>
    private static HttpRequestFraming OfUpload(HttpRequestOptions options, HttpUploadResume resume, Stream upload, HttpCustomHeader[] customHeaders, string? rangeText, bool convertLineEndings)
    {
        StreamBody body = new(upload, resume.Length, string.Empty);
        return OfBody(options.CustomMethod ?? "PUT", body, customHeaders, options.Version == HttpVersionPreference.Http10, resume, rangeText, convertLineEndings);
    }

    /// <summary>
    /// Decides the framing for a request that sends <paramref name="body" />.
    /// </summary>
    /// <param name="method">The request method.</param>
    /// <param name="body">The body to send.</param>
    /// <param name="customHeaders">The parsed <c>-H</c> values, in command-line order.</param>
    /// <param name="isHttp10">
    /// <see langword="true" /> for <c>-0</c>: curl adds no <c>Expect</c> of its own and refuses a
    /// body of unknown length that no <c>-H</c> value asks to send chunked.
    /// </param>
    /// <param name="upload">
    /// The resumed <c>-T</c> source when <paramref name="body" /> is it, or <see langword="null" />.
    /// </param>
    /// <param name="rangeText">
    /// The <c>-r</c> text to send as <c>Content-Range</c>, or <see langword="null" /> for none;
    /// a resumed upload's own <c>Content-Range</c> takes its place.
    /// </param>
    /// <param name="convertLineEndings">
    /// <see langword="true" /> for <c>--crlf</c>: <paramref name="body" /> is sent through
    /// <see cref="WithCrlfLineEndings" />, chunked, while curl decides <c>Expect</c> by its
    /// length before conversion (measured, AF-0125: no <c>Expect</c> for a 10-byte file).
    /// </param>
    private static HttpRequestFraming OfBody(string method, HttpRequestBody body, HttpCustomHeader[] customHeaders, bool isHttp10, HttpUploadResume? upload = null, string? rangeText = null, bool convertLineEndings = false)
    {
        long? unconvertedLength = body is BytesBody bytes ? bytes.Content.Length : ((StreamBody)body).Length;
        long? length = convertLineEndings ? null : unconvertedLength;
        body = convertLineEndings ? WithCrlfLineEndings(body) : body;
        bool wantsExpect = WantsExpect(unconvertedLength, isHttp10);
        bool namesExpect = customHeaders.Any(header => header.Names("Expect"));
        bool namesTransferEncoding = customHeaders.Any(header => header.Names(TransferEncodingName));
        bool asksForChunked = AsksForChunked(customHeaders);
        bool isChunked = namesTransferEncoding ? asksForChunked : length is null;
        bool awaitsContinue = namesExpect ? AsksForContinue(customHeaders) : wantsExpect;
        return new HttpRequestFraming(
            method,
            body,
            length,
            isChunked,
            wantsExpect && !namesExpect,
            awaitsContinue,
            isHttp10 && length is null && !namesTransferEncoding,
            upload,
            ContentRangeOf(upload, rangeText, length),
            chunksForUnknownLength: length is null && !namesTransferEncoding);
    }

    /// <summary>
    /// Gives the <c>-r</c> <paramref name="rangeText" /> a request with <paramref name="body" /> on
    /// <see cref="HttpRequestOptions.Body" /> sends as <c>Content-Range</c>: the text for a
    /// <c>-d</c> body (a <see cref="BytesBody" />), and <see langword="null" /> for a <c>-F</c>
    /// form, for which curl sends none (measured, BL-306 Notes).
    /// </summary>
    private static string? DataRangeOf(HttpRequestBody body, string? rangeText) =>
        body is BytesBody ? rangeText : null;

    /// <summary>
    /// Formats the <c>Content-Range</c> value: the resumed <paramref name="upload" />'s when it
    /// has one, else the <c>-r</c> <paramref name="rangeText" /> over a body of
    /// <paramref name="length" />, else <see langword="null" />.
    /// </summary>
    private static string? ContentRangeOf(HttpUploadResume? upload, string? rangeText, long? length) =>
        upload?.ContentRange ?? (rangeText is null ? null : HttpRangeHeader.ContentRangeFor(rangeText, length));

    /// <summary>
    /// Tells whether curl wants <c>Expect: 100-continue</c> for a body of
    /// <paramref name="length" />: over HTTP/1.1, when it is unknown or above
    /// <see cref="ExpectContinueThreshold" />; never over HTTP/1.0.
    /// </summary>
    private static bool WantsExpect(long? length, bool isHttp10) =>
        !isHttp10 && length.GetValueOrDefault(long.MaxValue) > ExpectContinueThreshold;


    /// <summary>
    /// Tells whether the first <c>-H</c> value naming <c>Transfer-Encoding</c> lists
    /// <c>chunked</c>.
    /// </summary>
    private static bool AsksForChunked(HttpCustomHeader[] customHeaders) =>
        FirstValueOf(customHeaders, TransferEncodingName)?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Tells whether the first <c>-H</c> value naming <c>Expect</c> is
    /// <c>100-continue</c>.
    /// </summary>
    private static bool AsksForContinue(HttpCustomHeader[] customHeaders) =>
        string.Equals(FirstValueOf(customHeaders, "Expect"), "100-continue", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the trimmed value of the first <c>-H</c> value naming <paramref name="name" />
    /// that sends a line, or <see langword="null" /> when none does.
    /// </summary>
    private static string? FirstValueOf(HttpCustomHeader[] customHeaders, string name)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.Names(name) && header.SentLine is { } line)
            {
                return line[(name.Length + 1)..].Trim();
            }
        }

        return null;
    }
}
