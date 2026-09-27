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
/// chunked when its length is unknown or an <c>-H</c> value asks for
/// <c>Transfer-Encoding: chunked</c>, and with <c>Content-Length</c> otherwise. curl adds
/// <c>Expect: 100-continue</c> when the length is unknown or above
/// <see cref="ExpectContinueThreshold" />, unless an <c>-H</c> value names <c>Expect</c>; the
/// request waits for <c>100 Continue</c> whenever it carries <c>Expect: 100-continue</c>,
/// its own or an <c>-H</c> one. An HTTP/1.0 request (<c>-0</c>) never gets curl's own
/// <c>Expect</c>, and one whose body length is unknown with no <c>-H</c> value asking for
/// chunked is refused (<see cref="RefusesUnknownLength" />; measured, BL-180 Notes).
/// </remarks>
internal sealed class HttpRequestFraming
{
    /// <summary>
    /// The largest body length curl 8.21.0 sends without <c>Expect: 100-continue</c>: 1 MiB.
    /// </summary>
    internal const long ExpectContinueThreshold = 1048576;

    private HttpRequestFraming(
        string method,
        HttpRequestBody? body,
        long? knownLength,
        bool isChunked,
        bool addsExpect,
        bool awaitsContinue,
        bool refusesUnknownLength = false,
        bool isUpload = false)
    {
        Method = method;
        Body = body;
        KnownLength = knownLength;
        IsChunked = isChunked;
        AddsExpect = addsExpect;
        AwaitsContinue = awaitsContinue;
        RefusesUnknownLength = refusesUnknownLength;
        IsUpload = isUpload;
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
    /// request whose body length is unknown, with no <c>-H</c> value asking for
    /// <c>Transfer-Encoding: chunked</c>. curl connects, sends nothing and fails with exit 25
    /// <c>Chunky upload is not supported by HTTP 1.0</c> (measured, BL-180 Notes).
    /// </summary>
    internal bool RefusesUnknownLength { get; }

    /// <summary>
    /// Gets a value indicating whether the body is the <c>-T</c>/<c>--upload-file</c> source on
    /// <see cref="ITransferContext.Upload" />: sent as PUT with no <c>Content-Type</c>, and
    /// failing a short read with curl's <c>client read function</c> message rather than its
    /// <c>client mime read</c> one (measured, BL-184 Notes).
    /// </summary>
    internal bool IsUpload { get; }

    /// <summary>
    /// Makes the same framing without curl's own <c>Expect: 100-continue</c> line and without
    /// the wait for <c>100 Continue</c>: the request curl 8.21.0 resends after a
    /// <c>417 Expectation Failed</c>. An <c>-H</c> <c>Expect</c> line is still sent, but the
    /// body follows the head at once (measured, BL-260 Notes).
    /// </summary>
    /// <returns>The framing of the resent request.</returns>
    internal HttpRequestFraming WithoutExpect() =>
        new(Method, Body, KnownLength, IsChunked, addsExpect: false, awaitsContinue: false, RefusesUnknownLength, IsUpload);

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
    /// <returns>The framing.</returns>
    internal static HttpRequestFraming Of(HttpRequestOptions options, HttpCustomHeader[] customHeaders, bool noBody = false, Stream? upload = null)
    {
        if (upload is not null)
        {
            return OfUpload(options, upload, customHeaders);
        }

        if (options.Body is not { } body)
        {
            return new HttpRequestFraming(options.CustomMethod ?? (noBody ? "HEAD" : "GET"), null, null, false, false, false);
        }

        return OfBody(options.CustomMethod ?? "POST", body, customHeaders, options.Version == HttpVersionPreference.Http10);
    }

    /// <summary>
    /// Decides the framing for a request that sends the <c>-T</c> source
    /// <paramref name="upload" />: PUT unless <c>-X</c> names another method, of the length
    /// left from the stream's position when it can seek and of unknown length otherwise.
    /// </summary>
    private static HttpRequestFraming OfUpload(HttpRequestOptions options, Stream upload, HttpCustomHeader[] customHeaders)
    {
        StreamBody body = new(upload, upload.CanSeek ? upload.Length - upload.Position : null, string.Empty);
        return OfBody(options.CustomMethod ?? "PUT", body, customHeaders, options.Version == HttpVersionPreference.Http10, isUpload: true);
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
    /// <param name="isUpload">Whether <paramref name="body" /> is the <c>-T</c> source.</param>
    private static HttpRequestFraming OfBody(string method, HttpRequestBody body, HttpCustomHeader[] customHeaders, bool isHttp10, bool isUpload = false)
    {
        long? length = body is BytesBody bytes ? bytes.Content.Length : ((StreamBody)body).Length;
        bool wantsExpect = WantsExpect(length, isHttp10);
        bool namesExpect = customHeaders.Any(header => header.Names("Expect"));
        bool asksForChunked = AsksForChunked(customHeaders);
        bool isChunked = length is null || asksForChunked;
        bool awaitsContinue = namesExpect ? AsksForContinue(customHeaders) : wantsExpect;
        return new HttpRequestFraming(
            method,
            body,
            length,
            isChunked,
            wantsExpect && !namesExpect,
            awaitsContinue,
            RefusesUnknownLengthOf(length, asksForChunked, isHttp10),
            isUpload);
    }

    /// <summary>
    /// Tells whether curl wants <c>Expect: 100-continue</c> for a body of
    /// <paramref name="length" />: over HTTP/1.1, when it is unknown or above
    /// <see cref="ExpectContinueThreshold" />; never over HTTP/1.0.
    /// </summary>
    private static bool WantsExpect(long? length, bool isHttp10) =>
        !isHttp10 && length.GetValueOrDefault(long.MaxValue) > ExpectContinueThreshold;

    /// <summary>
    /// Tells whether curl refuses the request: over HTTP/1.0, for a body of unknown length that
    /// no <c>-H</c> value asks to send chunked.
    /// </summary>
    private static bool RefusesUnknownLengthOf(long? length, bool asksForChunked, bool isHttp10) =>
        isHttp10 && length is null && !asksForChunked;

    /// <summary>
    /// Tells whether the first <c>-H</c> value naming <c>Transfer-Encoding</c> lists
    /// <c>chunked</c>.
    /// </summary>
    private static bool AsksForChunked(HttpCustomHeader[] customHeaders) =>
        FirstValueOf(customHeaders, "Transfer-Encoding")?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true;

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
