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
/// lower-ranked scheme also offered; when Negotiate was picked only after the challenge, as
/// under <c>--anyauth</c>, that nothing is the empty value, so the request is sent once more
/// without a header, and its 401 steps a context without answering (ADR-0232). Negotiate and NTLM need I/O, so only
/// <see cref="CreateAuthorizationAsync" /> and <see cref="ContinueAuthorizationAsync" /> answer
/// them (ADR-0176, ADR-0181). As libcurl does, <c>--negotiate</c> alone tries it on the first
/// request, and after a challenge answers only when <c>-u</c> was given, even as <c>-u :</c>,
/// though without it the context is still stepped so <c>-v</c> shows its failure;
/// NTLM answers only when <c>-u</c> was given, and <c>--ntlm</c> alone sends its Type 1 message
/// on the first request. Only NTLM, and Negotiate when the 401 carries the acceptor's token
/// (ADR-0227), go on after a request that sent a credential. A proxy's request is answered on
/// the same terms, <c>--proxy-ntlm</c> and <c>--proxy-negotiate</c> for a <c>407</c> as
/// <c>--ntlm</c> and <c>--negotiate</c> for a 401 (ADR-0270).
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
            string? value = await negotiate.CreateAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);
            return value ?? (PicksNegotiateAfterTheChallenge(request, challenges) ? string.Empty : null);
        }

        if (StepsNegotiateWithoutAnswering(request, challenges))
        {
            await negotiate.StepWithoutAnsweringAsync(request, cancellationToken).ConfigureAwait(false);
            return null;
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

        if (challenges.Count == 0)
        {
            return null;
        }

        if (sentAuthorization.Length == 0)
        {
            if (AnswersWithNegotiate(request, challenges))
            {
                await negotiate.StepWithoutAnsweringAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        if (ContinuesNegotiate(request, sentAuthorization))
        {
            return await negotiate.ContinueAuthorizationAsync(request, sentAuthorization, challenges, cancellationToken).ConfigureAwait(false);
        }

        return AnswersWithNtlm(request, challenges)
            ? await ntlm.CreateAuthorizationAsync(request, sentAuthorization, sentBeforeAnyChallenge, challenges, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Sends an answer already sent once more: a Digest answer counted on to its next nonce
    /// count by <see cref="DigestAuthenticator.RepeatAuthorization" />, as curl 8.21.0 does
    /// (BL-869); every other scheme's value as sent.
    /// </summary>
    /// <param name="request">The request being authorised.</param>
    /// <param name="sentAuthorization">The header value the last request sent.</param>
    /// <returns>The header value to send again.</returns>
    public string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization) =>
        digest.RepeatAuthorization(request, sentAuthorization);

    /// <summary>
    /// Ends the handshake that sent <paramref name="sentAuthorization" />: a Negotiate context
    /// kept for its next leg is disposed of without being stepped (ADR-0248); the other schemes
    /// keep nothing between requests.
    /// </summary>
    /// <param name="sentAuthorization">The header value the request sent.</param>
    public void EndAuthorization(string sentAuthorization) =>
        negotiate.EndAuthorization(sentAuthorization);

    /// <summary>
    /// Decides whether the continuation is Negotiate's: when the request sent a Negotiate value,
    /// which only <see cref="NegotiateHttpAuthenticator" /> makes, and <c>-u</c> (or <c>-U</c>
    /// for a proxy) was given, as libcurl answers no 401 or 407 without a user (ADR-0227,
    /// ADR-0270).
    /// </summary>
    private static bool ContinuesNegotiate(HttpAuthRequest request, string sentAuthorization) =>
        request.Credential is not null
            && sentAuthorization.StartsWith(NegotiateHttpAuthenticator.SchemePrefix, StringComparison.Ordinal);

    private static HttpAuthSchemes PickOf(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        HttpAuthSchemeRanking.PickFirst(request.AllowedSchemes & HttpChallengeSchemes.Offered(challenges));

    /// <summary>
    /// Decides whether Negotiate answers, for the origin and for a proxy alike (ADR-0270):
    /// before a challenge when it is the one scheme allowed, and after one when it is the pick
    /// and a credential was given.
    /// </summary>
    private static bool AnswersWithNegotiate(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count == 0
            ? request.AllowedSchemes == HttpAuthSchemes.Negotiate
            : request.Credential is not null && PickOf(request, challenges) == HttpAuthSchemes.Negotiate;

    /// <summary>
    /// Decides whether Negotiate is picked only now, for the request that answers
    /// <paramref name="challenges" />: when it was not the one scheme allowed, so the request
    /// that drew them picked nothing, as <c>--anyauth</c> does. The request then goes out again
    /// even when the context makes no token, as curl 8.21.0's <c>Curl_http_auth_act</c> asks for
    /// it whatever the context will make (measured, ADR-0232).
    /// </summary>
    private static bool PicksNegotiateAfterTheChallenge(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count != 0 && request.AllowedSchemes != HttpAuthSchemes.Negotiate;

    /// <summary>
    /// Decides whether a challenge is Negotiate's to step but not to answer: when
    /// <c>--negotiate</c> (or <c>--proxy-negotiate</c>) is the one scheme allowed, the challenges
    /// offer it, and no credential was given, as libcurl's <c>Curl_input_negotiate</c> still
    /// steps a context for the 401 or 407 - so <c>-v</c> shows its failure - but sends nothing
    /// (measured, BL-843 and BL-604 Notes).
    /// </summary>
    private static bool StepsNegotiateWithoutAnswering(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count != 0
            && request.Credential is null
            && request.AllowedSchemes == HttpAuthSchemes.Negotiate
            && PickOf(request, challenges) == HttpAuthSchemes.Negotiate;

    /// <summary>
    /// Decides whether NTLM answers, for the origin and for a proxy alike (ADR-0270): when a
    /// credential was given, before a challenge when it is the one scheme allowed, and after one
    /// when it is the pick.
    /// </summary>
    private static bool AnswersWithNtlm(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        request.Credential is not null
            && (challenges.Count == 0
                ? request.AllowedSchemes == HttpAuthSchemes.Ntlm
                : PickOf(request, challenges) == HttpAuthSchemes.Ntlm);
}
