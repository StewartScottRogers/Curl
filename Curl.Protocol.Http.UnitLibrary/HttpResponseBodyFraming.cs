namespace Curl.Protocol.Http;

/// <summary>
/// How an HTTP/1.x response body ends, as curl 8.21.0 decides it for <c>--raw</c>,
/// <c>--ignore-content-length</c> and <c>--tr-encoding</c> (measured, BL-180 and BL-315 Notes):
/// decoded as chunked, at a Content-Length, or when the peer closes; and, for
/// <c>--tr-encoding</c>, which transfer codings to decode.
/// </summary>
/// <remarks>
/// By default a chunked body wins over a Content-Length, which is still checked, and a body
/// with neither runs to close. <c>--raw</c> decodes no transfer coding: a body whose
/// Transfer-Encoding lists <c>chunked</c> runs to close and is written as it arrives, chunk
/// lines included, and no other coding is refused; one without still stops at its
/// Content-Length. <c>--ignore-content-length</c> never reads the Content-Length, so a body
/// that is not chunked runs to close, however short or long it is. Under
/// <c>--tr-encoding</c> any Transfer-Encoding header makes the Content-Length untrusted: the
/// body is chunked or runs to close, and a Content-Length header is checked only when it comes
/// before the first Transfer-Encoding header; <c>--raw</c> then passes only <c>chunked</c>
/// through (<see cref="HttpTransferEncoding.Requested" />).
/// </remarks>
/// <param name="isChunked">Whether the body is decoded as chunked transfer coding.</param>
/// <param name="contentLength">
/// The Content-Length the body stops at, or <see langword="null" /> when it is chunked or runs
/// to close.
/// </param>
/// <param name="transferCodings">
/// The transfer codings other than <c>chunked</c> that <c>--tr-encoding</c> decodes, in the
/// order the server applied them, or <see langword="null" /> for none.
/// </param>
internal readonly struct HttpResponseBodyFraming(bool isChunked, long? contentLength, IReadOnlyList<string>? transferCodings = null)
{
    /// <summary>
    /// Gets a value indicating whether the body is decoded as chunked transfer coding.
    /// </summary>
    internal bool IsChunked { get; } = isChunked;

    /// <summary>
    /// Gets the Content-Length the body stops at, or <see langword="null" /> when it is chunked
    /// or runs to close.
    /// </summary>
    internal long? ContentLength { get; } = contentLength;

    /// <summary>
    /// Gets the transfer codings other than <c>chunked</c> to decode, in the order the server
    /// applied them; empty unless <c>--tr-encoding</c> was given.
    /// </summary>
    internal IReadOnlyList<string> TransferCodings { get; } = transferCodings ?? [];

    /// <summary>
    /// Gets a value indicating whether the body runs until the peer closes.
    /// </summary>
    internal bool RunsToClose => !IsChunked && ContentLength is null;

    /// <summary>
    /// Decides how the body framed by <paramref name="headers" /> ends.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <param name="passesTransferCoding"><see langword="true" /> for <c>--raw</c>.</param>
    /// <param name="ignoresContentLength"><see langword="true" /> for <c>--ignore-content-length</c>.</param>
    /// <param name="decodesTransferCoding"><see langword="true" /> for <c>--tr-encoding</c>.</param>
    /// <returns>The framing.</returns>
    /// <exception cref="HttpTransferException">
    /// A Transfer-Encoding header lists a coding curl does not accept (exit 61); without
    /// <c>--ignore-content-length</c>, the Content-Length is invalid (exit 8).
    /// </exception>
    internal static HttpResponseBodyFraming Of(
        IReadOnlyList<HttpResponseHeader> headers,
        bool passesTransferCoding,
        bool ignoresContentLength,
        bool decodesTransferCoding = false)
    {
        return decodesTransferCoding
            ? OfRequested(headers, passesTransferCoding, ignoresContentLength)
            : OfUnrequested(headers, passesTransferCoding, ignoresContentLength);
    }

    /// <summary>
    /// Decides the framing without <c>--tr-encoding</c>.
    /// </summary>
    private static HttpResponseBodyFraming OfUnrequested(
        IReadOnlyList<HttpResponseHeader> headers,
        bool passesTransferCoding,
        bool ignoresContentLength)
    {
        bool isChunked = !passesTransferCoding && HttpTransferEncoding.IsChunked(headers);
        bool listsChunked = isChunked || (passesTransferCoding && HttpTransferEncoding.ListsChunked(headers));
        long? contentLength = ignoresContentLength ? null : HttpContentLength.Find(headers);
        return new HttpResponseBodyFraming(isChunked, listsChunked ? null : contentLength);
    }

    /// <summary>
    /// Decides the framing under <c>--tr-encoding</c>.
    /// </summary>
    private static HttpResponseBodyFraming OfRequested(
        IReadOnlyList<HttpResponseHeader> headers,
        bool passesTransferCoding,
        bool ignoresContentLength)
    {
        HttpTransferCodings codings = HttpTransferEncoding.Requested(headers, passesTransferCoding);
        IReadOnlyList<HttpResponseHeader> checkedHeaders = codings.FirstHeaderIndex is { } first ? [.. headers.Take(first)] : headers;
        long? contentLength = ignoresContentLength ? null : HttpContentLength.Find(checkedHeaders);
        return new HttpResponseBodyFraming(codings.IsChunked, codings.FirstHeaderIndex is null ? contentLength : null, codings.Codings);
    }
}
