using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with the one scheme curl 8.21.0 picks among those offered and allowed, for
/// <c>--basic</c>, <c>--digest</c>, <c>--negotiate</c>, <c>--anyauth</c> and any mix of them
/// (ADR-0028): the first of Negotiate, Bearer, Digest, NTLM, Basic.
/// </summary>
/// <param name="basicAndBearer">Answers when the pick is Basic or Bearer, and before any challenge.</param>
/// <param name="digest">Answers when the pick is Digest.</param>
/// <param name="negotiate">Answers when the pick is Negotiate, and before any challenge when Negotiate is the one scheme allowed.</param>
/// <remarks>
/// There is no fallback, as the reference build has none: when the pick is NTLM, which is
/// not built here, a Negotiate context that makes no token, or a Digest challenge curl cannot
/// read, it sends nothing rather than answer a lower-ranked scheme also offered. Negotiate
/// needs I/O, so only <see cref="CreateAuthorizationAsync" /> answers it (ADR-0173). As
/// libcurl does, <c>--negotiate</c> alone tries it on the first request, and after a
/// challenge answers only when <c>-u</c> was given, even as <c>-u :</c>.
/// </remarks>
public sealed class RankedHttpAuthenticator(BasicAndBearerAuthenticator basicAndBearer, DigestAuthenticator digest, NegotiateHttpAuthenticator negotiate) : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);

        IHttpAuthenticator answerer = challenges.Count != 0 && PickOf(request, challenges) == HttpAuthSchemes.Digest
            ? digest
            : basicAndBearer;
        return answerer.CreateAuthorization(request, challenges);
    }

    /// <inheritdoc />
    public async ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);

        return AnswersWithNegotiate(request, challenges)
            ? await negotiate.CreateAuthorizationAsync(request, cancellationToken).ConfigureAwait(false)
            : CreateAuthorization(request, challenges);
    }

    private static HttpAuthSchemes PickOf(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        HttpAuthSchemeRanking.PickFirst(request.AllowedSchemes & HttpChallengeSchemes.Offered(challenges));

    /// <summary>
    /// Decides whether Negotiate answers: for the origin only (proxy Negotiate is another
    /// task's), before a challenge when it is the one scheme allowed, and after one when it is
    /// the pick and a credential was given.
    /// </summary>
    private static bool AnswersWithNegotiate(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        !request.IsProxy
            && (challenges.Count == 0
                ? request.AllowedSchemes == HttpAuthSchemes.Negotiate
                : request.Credential is not null && PickOf(request, challenges) == HttpAuthSchemes.Negotiate);
}
