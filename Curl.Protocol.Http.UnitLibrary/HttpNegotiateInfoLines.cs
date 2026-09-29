using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Where curl 8.21.0 writes the <c>-v</c> lines of a Negotiate request (ADR-0228):
/// <c>Server auth using Negotiate with user '...'</c> before each request Negotiate is picked
/// for, after the context's failure line if any, and a 401's failure line just before the
/// <c>WWW-Authenticate</c> header that offers Negotiate (measured, BL-843 Notes).
/// </summary>
internal static class HttpNegotiateInfoLines
{
    /// <summary>What an <c>Authorization</c> value made by Negotiate starts with.</summary>
    private const string SchemePrefix = "Negotiate ";

    /// <summary>
    /// Formats the line curl writes before a request it picked Negotiate for: the <c>-u</c>
    /// user name as given, a domain included, or nothing when there is no <c>-u</c>.
    /// </summary>
    /// <param name="credential">The request's credential, or <see langword="null" />.</param>
    /// <returns>The line.</returns>
    internal static string ServerAuthUsing(NetworkCredential? credential) =>
        $"Server auth using Negotiate with user '{credential?.UserName}'";

    /// <summary>
    /// Decides whether curl picks Negotiate for a request to the origin: one sending a
    /// Negotiate value, or the transfer's first request when <c>--negotiate</c> is the one
    /// scheme allowed, whether or not its context made a token.
    /// </summary>
    /// <param name="request">The request as the authenticator is asked about it.</param>
    /// <param name="authorization">The <c>Authorization</c> value the request sends, or <see langword="null" />.</param>
    /// <param name="answersChallenge"><see langword="true" /> when the request answers a 401.</param>
    /// <returns><see langword="true" /> when the request is sent with Negotiate picked.</returns>
    internal static bool PicksNegotiate(HttpAuthRequest request, string? authorization, bool answersChallenge) =>
        authorization is not null
            ? authorization.StartsWith(SchemePrefix, StringComparison.Ordinal)
            : !answersChallenge && request.AllowedSchemes == HttpAuthSchemes.Negotiate;

    /// <summary>
    /// Decides whether <paramref name="header" /> of a final head is where curl writes what
    /// answering the response's challenges reports: a 401's <c>WWW-Authenticate</c> offering
    /// Negotiate, when Negotiate is allowed.
    /// </summary>
    /// <param name="request">The request as the authenticator is asked about it.</param>
    /// <param name="statusLine">The final head's status line.</param>
    /// <param name="header">The header.</param>
    /// <returns><see langword="true" /> when the authenticator's lines belong before this header.</returns>
    internal static bool IsNegotiateChallenge(HttpAuthRequest request, HttpStatusLine statusLine, HttpResponseHeader header) =>
        statusLine.StatusCode == 401
            && (request.AllowedSchemes & HttpAuthSchemes.Negotiate) != 0
            && string.Equals(header.Name, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase)
            && OffersNegotiate(header.Value);

    /// <summary>Decides whether a challenge's first token is the scheme <c>Negotiate</c>, in any case.</summary>
    private static bool OffersNegotiate(string challenge) =>
        challenge.StartsWith("Negotiate", StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == SchemePrefix.Length - 1 || challenge[SchemePrefix.Length - 1] == ' ');
}
