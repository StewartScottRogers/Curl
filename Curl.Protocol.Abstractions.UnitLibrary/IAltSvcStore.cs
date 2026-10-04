namespace Curl.Protocol.Abstractions;

/// <summary>
/// Learns the alternative services an HTTPS origin advertises in its <c>Alt-Svc</c> response
/// headers (<c>--alt-svc</c>).
/// </summary>
/// <remarks>
/// Implemented in <c>Curl.Core.UnitLibrary</c> (<c>AltSvcCache</c>, ADR-0175) and set on
/// <see cref="HttpRequestOptions.AltSvcStore" /> by <c>Curl.Console</c>, which also reads and
/// writes the cache file around the transfers and looks up the <see cref="AltSvcRoute" /> a
/// transfer uses. The store never reads a clock: the caller passes the time from
/// <see cref="ITransferContext.TimeProvider" />.
/// </remarks>
public interface IAltSvcStore
{
    /// <summary>
    /// Stores the alternatives one <c>Alt-Svc</c> header of a response from
    /// <paramref name="origin" /> advertises, as the header arrives.
    /// </summary>
    /// <param name="origin">
    /// The URL of the request the response answered: the origin, never the alternative the
    /// connection went to.
    /// </param>
    /// <param name="altSvcHeader">The <c>Alt-Svc</c> header's value, verbatim.</param>
    /// <param name="responseVersion">
    /// The HTTP version the response came over (<see cref="System.Net.HttpVersion.Version30" />,
    /// <see cref="System.Net.HttpVersion.Version20" />, or an HTTP/1.x version), which names the source
    /// ALPN the alternatives are stored under, as curl 8.21.0 passes <c>k->httpversion</c> to
    /// <c>Curl_altsvc_parse</c>.
    /// </param>
    /// <param name="now">The receive time that each alternative's <c>ma</c> counts from.</param>
    /// <returns>
    /// One outcome per alternative the header named and the store read, in header order: the
    /// <see cref="AltSvcAlternative" /> added, for the caller's <c>Added alt-svc</c> line, or the
    /// <see cref="AltSvcSkipReason" /> it was skipped for (ADR-0409); empty when it read none.
    /// </returns>
    IReadOnlyList<AltSvcHeaderOutcome> StoreFromResponse(CurlUrl origin, string altSvcHeader, Version responseVersion, DateTimeOffset now);
}
