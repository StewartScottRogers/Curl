namespace Curl.Protocol.Http;

/// <summary>
/// How an HTTP/1.x response body ends, as curl 8.21.0 decides it for <c>--raw</c> and
/// <c>--ignore-content-length</c> (measured, BL-180 Notes): decoded as chunked, at a
/// Content-Length, or when the peer closes.
/// </summary>
/// <remarks>
/// By default a chunked body wins over a Content-Length, which is still checked, and a body
/// with neither runs to close. <c>--raw</c> decodes no transfer coding: a body whose
/// Transfer-Encoding lists <c>chunked</c> runs to close and is written as it arrives, chunk
/// lines included, and no other coding is refused; one without still stops at its
/// Content-Length. <c>--ignore-content-length</c> never reads the Content-Length, so a body
/// that is not chunked runs to close, however short or long it is.
/// </remarks>
/// <param name="isChunked">Whether the body is decoded as chunked transfer coding.</param>
/// <param name="contentLength">
/// The Content-Length the body stops at, or <see langword="null" /> when it is chunked or runs
/// to close.
/// </param>
internal readonly struct HttpResponseBodyFraming(bool isChunked, long? contentLength)
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
    /// Gets a value indicating whether the body runs until the peer closes.
    /// </summary>
    internal bool RunsToClose => !IsChunked && ContentLength is null;

    /// <summary>
    /// Decides how the body framed by <paramref name="headers" /> ends.
    /// </summary>
    /// <param name="headers">The final response's headers.</param>
    /// <param name="passesTransferCoding"><see langword="true" /> for <c>--raw</c>.</param>
    /// <param name="ignoresContentLength"><see langword="true" /> for <c>--ignore-content-length</c>.</param>
    /// <returns>The framing.</returns>
    /// <exception cref="HttpTransferException">
    /// Without <c>--raw</c>, a Transfer-Encoding header lists a coding curl does not decode
    /// (exit 61); without <c>--ignore-content-length</c>, the Content-Length is invalid (exit 8).
    /// </exception>
    internal static HttpResponseBodyFraming Of(
        IReadOnlyList<HttpResponseHeader> headers,
        bool passesTransferCoding,
        bool ignoresContentLength)
    {
        bool isChunked = !passesTransferCoding && HttpTransferEncoding.IsChunked(headers);
        bool listsChunked = isChunked || (passesTransferCoding && HttpTransferEncoding.ListsChunked(headers));
        long? contentLength = ignoresContentLength ? null : HttpContentLength.Find(headers);
        return new HttpResponseBodyFraming(isChunked, listsChunked ? null : contentLength);
    }
}
