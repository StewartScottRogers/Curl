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
    /// Returns the value of the <c>Cookie</c> header to send to <paramref name="uri" />.
    /// </summary>
    /// <param name="uri">The URL the request is sent to.</param>
    /// <param name="secure">
    /// <see langword="true" /> when the request travels over TLS, so the store can
    /// withhold <c>Secure</c> cookies otherwise.
    /// </param>
    /// <param name="now">The time that decides which cookies have expired.</param>
    /// <returns>
    /// The header value without the header name, or <see langword="null" /> when no stored
    /// cookie matches.
    /// </returns>
    string? GetCookieHeader(Uri uri, bool secure, DateTimeOffset now);

    /// <summary>
    /// Stores the cookies from a response to a request for <paramref name="uri" />.
    /// </summary>
    /// <param name="uri">The URL of the request the response answered.</param>
    /// <param name="setCookieHeaders">
    /// The value of each <c>Set-Cookie</c> header, verbatim and in the order received.
    /// </param>
    /// <param name="now">
    /// The receive time that relative expiry (<c>Max-Age</c>) counts from.
    /// </param>
    void StoreFromResponse(Uri uri, IReadOnlyList<string> setCookieHeaders, DateTimeOffset now);
}
