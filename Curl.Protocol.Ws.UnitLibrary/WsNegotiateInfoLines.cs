using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Where curl 8.21.0 writes the <c>-v</c> lines of a Negotiate upgrade (ADR-0231, BL-955):
/// <c>Server auth using Negotiate with user '...'</c> (<see cref="WsAuthUsingLines" />) before the
/// upgrade request when Negotiate is picked, after the context's failure line if any, and a 401's
/// failure line just before the <c>WWW-Authenticate</c> header that offers Negotiate, as the
/// HTTP handler writes them.
/// </summary>
internal static class WsNegotiateInfoLines
{
    /// <summary>What an <c>Authorization</c> value made by Negotiate starts with.</summary>
    private const string SchemePrefix = "Negotiate ";

    /// <summary>The challenge header's name and colon.</summary>
    private const string ChallengeHeader = "WWW-Authenticate:";

    /// <summary>
    /// Decides whether curl picks Negotiate for the upgrade request: one sending a Negotiate
    /// value, or, sending none, one with <c>--negotiate</c> the one scheme allowed, whether or
    /// not its context made a token.
    /// </summary>
    /// <param name="request">The request as the authenticator was asked about it.</param>
    /// <param name="authorization">The <c>Authorization</c> value the request sends, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the request is sent with Negotiate picked.</returns>
    internal static bool PicksNegotiate(HttpAuthRequest request, string? authorization) =>
        authorization is not null
            ? authorization.StartsWith(SchemePrefix, StringComparison.Ordinal)
            : request.AllowedSchemes == HttpAuthSchemes.Negotiate;

    /// <summary>
    /// Gets the value of every <c>WWW-Authenticate</c> header in <paramref name="head" />, in
    /// received order.
    /// </summary>
    /// <param name="head">The reply head, status line to blank line.</param>
    /// <returns>The challenges, trimmed.</returns>
    internal static string[] ChallengesOf(byte[] head) =>
        [.. Encoding.Latin1.GetString(head)
            .Split('\n')
            .Where(IsChallenge)
            .Select(line => line[ChallengeHeader.Length..].Trim())];

    /// <summary>
    /// Decides whether <paramref name="line" /> of a reply head is a <c>WWW-Authenticate</c>
    /// header offering Negotiate, in any case.
    /// </summary>
    /// <param name="line">The header line, its line ending included or not.</param>
    /// <returns><see langword="true" /> when the line offers Negotiate.</returns>
    internal static bool IsNegotiateChallenge(ReadOnlySpan<byte> line)
    {
        string text = Encoding.Latin1.GetString(line);
        return IsChallenge(text) && OffersNegotiate(text[ChallengeHeader.Length..].Trim());
    }

    /// <summary>Decides whether a head line is a <c>WWW-Authenticate</c> header, in any case.</summary>
    private static bool IsChallenge(string line) =>
        line.StartsWith(ChallengeHeader, StringComparison.OrdinalIgnoreCase);

    /// <summary>Decides whether a challenge's first token is the scheme <c>Negotiate</c>, in any case.</summary>
    /// <param name="challenge">A <c>WWW-Authenticate</c> value, trimmed.</param>
    /// <returns><see langword="true" /> when the challenge offers Negotiate.</returns>
    internal static bool OffersNegotiate(string challenge) =>
        challenge.StartsWith("Negotiate", StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == SchemePrefix.Length - 1 || challenge[SchemePrefix.Length - 1] == ' ');
}
