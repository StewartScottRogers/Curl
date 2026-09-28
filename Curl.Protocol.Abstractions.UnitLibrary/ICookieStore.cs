namespace Curl.Protocol.Abstractions;

/// <summary>
/// Holds the cookies a transfer has received and supplies the <c>Cookie</c> header for the
/// next request (ADR-0014).
/// </summary>
/// <remarks>
/// Implemented in <c>Curl.Cookies.UnitLibrary</c> and handed to the HTTP handler by
/// <c>Curl.Console</c>. The store never reads a clock: the caller passes the time from
/// <see cref="ITransferContext.TimeProvider" />. Loading <c>-b</c> and writing <c>-c</c>
/// are not on this contract; <c>Curl.Console</c> does both around the transfers.
/// </remarks>
public interface ICookieStore
{
    /// <summary>
    /// Returns the value of the <c>Cookie</c> header to send to <paramref name="url" />.
    /// </summary>
    /// <param name="url">The URL the request is sent to.</param>
    /// <param name="secure">
    /// <see langword="true" /> when the request travels over TLS, so the store can
    /// withhold <c>Secure</c> cookies otherwise.
    /// </param>
    /// <param name="now">The time that decides which cookies have expired.</param>
    /// <returns>
    /// The header value without the header name, or <see langword="null" /> when no stored
    /// cookie matches.
    /// </returns>
    string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now);

    /// <summary>
    /// Stores the cookie from one <c>Set-Cookie</c> header of a response to a request for
    /// <paramref name="url" />, as the header arrives, so its <c>-v</c> line is reported before
    /// the header line is.
    /// </summary>
    /// <param name="url">The URL of the request the response answered.</param>
    /// <param name="setCookieHeader">The <c>Set-Cookie</c> header's value, verbatim.</param>
    /// <param name="storedFromResponse">
    /// How many cookies the store has already stored from this request's responses, as the
    /// previous call returned it; 0 for the first <c>Set-Cookie</c> header. The store ignores the
    /// header once this reaches its per-response limit.
    /// </param>
    /// <param name="now">
    /// The receive time that relative expiry (<c>Max-Age</c>) counts from.
    /// </param>
    /// <param name="events">
    /// Where the store reports, as curl's <c>-v</c> lines, the cookie it adds, replaces or
    /// drops; <see cref="NoTransferEvents.Instance" /> when nobody is listening.
    /// </param>
    /// <returns>
    /// <paramref name="storedFromResponse" />, plus one when this header's cookie was stored.
    /// </returns>
    int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events);
}
