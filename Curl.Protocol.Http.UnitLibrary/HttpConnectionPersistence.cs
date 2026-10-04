namespace Curl.Protocol.Http;

/// <summary>
/// Decides whether a connection may carry another request once a response has been read to
/// its end, as HTTP/1.1 (RFC 9112 section 9.3) says.
/// </summary>
internal static class HttpConnectionPersistence
{
    /// <summary>
    /// Decides whether the connection that carried <paramref name="head" /> stays open after
    /// its body: not when a <c>Connection</c> header names <c>close</c>, not for HTTP/1.0
    /// unless a <c>Connection</c> header names <c>keep-alive</c>, and not when the body runs
    /// until the server closes (<see cref="HttpResponseBodyFraming.RunsToClose" />).
    /// </summary>
    /// <param name="head">The response head.</param>
    /// <param name="noBody">
    /// <see langword="true" /> when the request was HEAD (<c>-I</c>), whose response has no body.
    /// </param>
    /// <param name="passesTransferCoding">
    /// <see langword="true" /> for <c>--raw</c>, which reads a chunked body until the server closes.
    /// </param>
    /// <param name="ignoresContentLength">
    /// <see langword="true" /> for <c>--ignore-content-length</c>, which reads a body that is not
    /// chunked until the server closes.
    /// </param>
    /// <param name="decodesTransferCoding">
    /// <see langword="true" /> for <c>--tr-encoding</c>, which reads a body with a
    /// Transfer-Encoding that is not chunked until the server closes.
    /// </param>
    /// <returns><see langword="true" /> when the next request may be sent on the same connection.</returns>
    internal static bool KeepsAlive(
        HttpResponseHead head,
        bool noBody,
        bool passesTransferCoding = false,
        bool ignoresContentLength = false,
        bool decodesTransferCoding = false)
    {
        if (NamesConnectionOption(head, "close"))
        {
            return false;
        }

        bool persistentVersion = head.StatusLine.Version >= new Version(1, 1) || NamesConnectionOption(head, "keep-alive");
        return persistentVersion
            && !(HttpResponseBodyReader.HasBody(head, noBody)
                && HttpResponseBodyFraming.Of(head.Headers, passesTransferCoding, ignoresContentLength, decodesTransferCoding).RunsToClose);
    }

    /// <summary>
    /// Decides whether curl 8.21.0 leaves the connection that carried <paramref name="head" />
    /// intact although its body ran until the server closed: an HTTP/1.0 response kept alive by
    /// a <c>Connection: keep-alive</c> header, not named <c>close</c>, whose body has no
    /// Content-Length it stops at and no chunked coding (measured, BL-471 Notes). curl reports
    /// <c>Connection #N to host H:P left intact</c> for it and pools it, then finds it dead
    /// before reusing it (ADR-0324, ADR-0112).
    /// </summary>
    /// <param name="head">The response head.</param>
    /// <param name="noBody"><see langword="true" /> for a request made with HEAD (<c>-I</c>).</param>
    /// <param name="passesTransferCoding"><see langword="true" /> for <c>--raw</c>.</param>
    /// <param name="ignoresContentLength"><see langword="true" /> for <c>--ignore-content-length</c>.</param>
    /// <param name="decodesTransferCoding"><see langword="true" /> for <c>--tr-encoding</c>.</param>
    /// <returns><see langword="true" /> when curl reports the closed connection as left intact.</returns>
    internal static bool KeepsHttp10AliveUntilServerCloses(
        HttpResponseHead head,
        bool noBody,
        bool passesTransferCoding,
        bool ignoresContentLength,
        bool decodesTransferCoding) =>
        head.StatusLine.Version < new Version(1, 1)
            && NamesConnectionOption(head, "keep-alive")
            && !NamesConnectionOption(head, "close")
            && HttpResponseBodyReader.HasBody(head, noBody)
            && HttpResponseBodyFraming.Of(head.Headers, passesTransferCoding, ignoresContentLength, decodesTransferCoding).RunsToClose;

    /// <summary>
    /// Decides whether one header line is what makes curl 8.21.0 keep an HTTP/1.0 connection
    /// alive and print <see cref="HttpConnectionInfoLines.Http10KeepAlive" />: a
    /// <c>Connection</c> header naming <c>keep-alive</c> and not <c>close</c> (measured, BL-467
    /// Notes). Each such line counts, a second one included.
    /// </summary>
    /// <param name="headerLine">The header line's text, without its line end.</param>
    /// <returns><see langword="true" /> when the line keeps an HTTP/1.0 connection alive.</returns>
    internal static bool KeepsHttp10Alive(string headerLine)
    {
        int colon = headerLine.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0
            && string.Equals(headerLine[..colon], "Connection", StringComparison.OrdinalIgnoreCase)
            && NamesOption(headerLine[(colon + 1)..], "keep-alive")
            && !NamesOption(headerLine[(colon + 1)..], "close");
    }

    /// <summary>
    /// Decides whether curl 8.21.0 finds no end-of-message indicator in an HTTP/1.1 response
    /// with a body - no chunked coding, no <c>Connection: close</c> and no size - and so prints
    /// <see cref="HttpConnectionInfoLines.NoEndOfMessageIndicator" /> (measured, BL-467 Notes).
    /// Not for HTTP/1.0, a 204, a 304 or a response to HEAD; not when a Transfer-Encoding
    /// header is present, since a chunked one gives the body its end and, under
    /// <c>--tr-encoding</c>, any other already closes the connection; and not for a
    /// Content-Length unless <c>--ignore-content-length</c> is given, nor one too large to hold,
    /// which curl answers by closing the connection (BL-1387).
    /// </summary>
    /// <param name="head">The final response's head, already checked for a refused header.</param>
    /// <param name="noBody"><see langword="true" /> for a request made with HEAD (<c>-I</c>).</param>
    /// <param name="ignoresContentLength"><see langword="true" /> for <c>--ignore-content-length</c>.</param>
    /// <returns><see langword="true" /> when the body can only end when the server closes.</returns>
    internal static bool LacksEndOfMessageIndicator(HttpResponseHead head, bool noBody, bool ignoresContentLength) =>
        head.StatusLine.Version == new Version(1, 1)
            && HttpResponseBodyReader.HasBody(head, noBody)
            && !NamesConnectionOption(head, "close")
            && HasNoSizeOrChunk(head.Headers, ignoresContentLength);

    private static bool HasNoSizeOrChunk(IReadOnlyList<HttpResponseHeader> headers, bool ignoresContentLength) =>
        !headers.Any(header => string.Equals(header.Name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            && (ignoresContentLength || (HttpContentLength.Find(headers) is null && !HttpContentLength.Overflows(headers)));

    private static bool NamesConnectionOption(HttpResponseHead head, string option) =>
        head.Headers
            .Where(header => string.Equals(header.Name, "Connection", StringComparison.OrdinalIgnoreCase))
            .Any(header => NamesOption(header.Value, option));

    private static bool NamesOption(string value, string option) =>
        value.Split(',', StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, option, StringComparison.OrdinalIgnoreCase));
}
