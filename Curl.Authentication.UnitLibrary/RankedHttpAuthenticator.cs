using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with the one scheme curl 8.21.0 picks among those offered and allowed, for
/// <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c>, <c>--anyauth</c> and any
/// mix of them (ADR-0028): the first of Negotiate, Bearer, Digest, NTLM, Basic.
/// </summary>
/// <param name="basicAndBearer">Answers when the pick is Basic or Bearer, and before any challenge.</param>
/// <param name="digest">Answers when the pick is Digest.</param>
/// <param name="negotiate">Answers when the pick is Negotiate, and before any challenge when Negotiate is the one scheme allowed.</param>
/// <param name="ntlm">Answers when the pick is NTLM, and before any challenge when NTLM is the one scheme allowed.</param>
/// <remarks>
/// There is no fallback, as the reference build has none: when a Negotiate context makes no
/// token, or a Digest or NTLM challenge cannot be read, it sends nothing rather than answer a
/// lower-ranked scheme also offered. Negotiate and NTLM need I/O, so only
/// <see cref="CreateAuthorizationAsync" /> and <see cref="ContinueAuthorizationAsync" /> answer
/// them (ADR-0176, ADR-0180). As libcurl does, <c>--negotiate</c> alone tries it on the first
/// request, and after a challenge answers only when <c>-u</c> was given, even as <c>-u :</c>;
/// NTLM answers only when <c>-u</c> was given, and <c>--ntlm</c> alone sends its Type 1 message
/// on the first request. Only NTLM goes on after a request that sent a credential.
/// </remarks>
public sealed class RankedHttpAuthenticator(BasicAndBearerAuthenticator basicAndBearer, DigestAuthenticator digest, NegotiateHttpAuthenticator negotiate, NtlmHttpAuthenticator ntlm) : IHttpAuthenticator
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

        if (AnswersWithNegotiate(request, challenges))
        {
            return await negotiate.CreateAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return AnswersWithNtlm(request, challenges)
            ? await ntlm.CreateAuthorizationAsync(request, null, sentBeforeAnyChallenge: false, challenges, cancellationToken).ConfigureAwait(false)
            : CreateAuthorization(request, challenges);
    }

    /// <inheritdoc />
    public async ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sentAuthorization);
        ArgumentNullException.ThrowIfNull(challenges);

        return challenges.Count != 0 && AnswersWithNtlm(request, challenges)
            ? await ntlm.CreateAuthorizationAsync(request, sentAuthorization, sentBeforeAnyChallenge, challenges, cancellationToken).ConfigureAwait(false)
            : null;
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

    /// <summary>
    /// Decides whether NTLM answers: for the origin only (proxy NTLM is another task's), when a
    /// credential was given, before a challenge when it is the one scheme allowed, and after one
    /// when it is the pick.
    /// </summary>
    private static bool AnswersWithNtlm(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        !request.IsProxy
            && request.Credential is not null
            && (challenges.Count == 0
                ? request.AllowedSchemes == HttpAuthSchemes.Ntlm
                : PickOf(request, challenges) == HttpAuthSchemes.Ntlm);
}
