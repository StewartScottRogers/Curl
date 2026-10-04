namespace Curl.Protocol.Abstractions;

/// <summary>
/// Learns the HTTP Strict Transport Security policy an HTTPS origin sends in each
/// <c>Strict-Transport-Security</c> response header (<c>--hsts</c>), one header at a time as it is
/// read (ADR-0409).
/// </summary>
/// <remarks>
/// Set on <see cref="HttpRequestOptions.HstsStore" />. The store never reads a clock: the caller
/// passes the time from <see cref="ITransferContext.TimeProvider" />.
/// </remarks>
public interface IHstsStore
{
    /// <summary>
    /// Applies one <c>Strict-Transport-Security</c> header of a response from
    /// <paramref name="origin" />.
    /// </summary>
    /// <param name="origin">The URL of the request the response answered.</param>
    /// <param name="headerValue">The header's value, verbatim.</param>
    /// <param name="now">The receive time the header's <c>max-age</c> counts from.</param>
    /// <returns>
    /// <see langword="false" /> only when the header is illegal and curl 8.21.0 writes
    /// <c>Illegal STS header skipped</c> (<c>lib/http.c</c>); <see langword="true" /> otherwise,
    /// including for an IP-address host, which stores nothing and for which curl writes no line.
    /// </returns>
    bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now);
}
