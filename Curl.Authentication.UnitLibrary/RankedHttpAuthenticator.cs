using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with the one scheme curl 8.21.0 picks among those offered and allowed, for
/// <c>--basic</c>, <c>--digest</c>, <c>--anyauth</c> and any mix of them (ADR-0028): the
/// first of Negotiate, Bearer, Digest, NTLM, Basic.
/// </summary>
/// <param name="basicAndBearer">Answers when the pick is Basic or Bearer, and before any challenge.</param>
/// <param name="digest">Answers when the pick is Digest.</param>
/// <remarks>
/// There is no fallback, as the reference build has none: when the pick is NTLM or
/// Negotiate, which are not built here, or a Digest challenge curl cannot read, it sends
/// nothing rather than answer a lower-ranked scheme also offered.
/// </remarks>
public sealed class RankedHttpAuthenticator(BasicAndBearerAuthenticator basicAndBearer, DigestAuthenticator digest) : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);

        IHttpAuthenticator answerer = challenges.Count != 0
            && HttpAuthSchemeRanking.PickFirst(request.AllowedSchemes & HttpChallengeSchemes.Offered(challenges)) == HttpAuthSchemes.Digest
                ? digest
                : basicAndBearer;
        return answerer.CreateAuthorization(request, challenges);
    }
}
