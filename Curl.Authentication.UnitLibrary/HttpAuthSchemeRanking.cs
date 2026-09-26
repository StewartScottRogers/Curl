using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Picks one scheme from several the way libcurl 8.21.0 does after a challenge: the first,
/// in the order Negotiate, Bearer, Digest, NTLM, Basic, that is both offered and allowed.
/// </summary>
internal static class HttpAuthSchemeRanking
{
    private static readonly HttpAuthSchemes[] LibcurlPickOrder =
    [
        HttpAuthSchemes.Negotiate,
        HttpAuthSchemes.Bearer,
        HttpAuthSchemes.Digest,
        HttpAuthSchemes.Ntlm,
        HttpAuthSchemes.Basic,
    ];

    /// <summary>
    /// Gets the scheme libcurl picks from <paramref name="available" />.
    /// </summary>
    /// <param name="available">The schemes both offered and allowed.</param>
    /// <returns>
    /// The highest-ranked scheme in <paramref name="available" />;
    /// <see cref="HttpAuthSchemes.None" /> when it is empty.
    /// </returns>
    internal static HttpAuthSchemes PickFirst(HttpAuthSchemes available) =>
        Array.Find(LibcurlPickOrder, scheme => (available & scheme) != 0);
}
