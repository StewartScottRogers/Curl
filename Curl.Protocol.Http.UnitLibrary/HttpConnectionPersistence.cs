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
    /// <returns><see langword="true" /> when the next request may be sent on the same connection.</returns>
    internal static bool KeepsAlive(HttpResponseHead head, bool noBody, bool passesTransferCoding = false, bool ignoresContentLength = false)
    {
        if (NamesConnectionOption(head, "close"))
        {
            return false;
        }

        bool persistentVersion = head.StatusLine.Version >= new Version(1, 1) || NamesConnectionOption(head, "keep-alive");
        return persistentVersion
            && !(HttpResponseBodyReader.HasBody(head, noBody)
                && HttpResponseBodyFraming.Of(head.Headers, passesTransferCoding, ignoresContentLength).RunsToClose);
    }

    private static bool NamesConnectionOption(HttpResponseHead head, string option) =>
        head.Headers
            .Where(header => string.Equals(header.Name, "Connection", StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value.Split(',', StringSplitOptions.TrimEntries))
            .Any(token => string.Equals(token, option, StringComparison.OrdinalIgnoreCase));
}
