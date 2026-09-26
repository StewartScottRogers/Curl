using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with <c>Basic</c> and <c>Bearer</c> authorization values as curl 8.21.0 builds
/// them (ADR-0014, ADR-0022).
/// </summary>
/// <param name="credentialEncoding">
/// The encoding the user name, password and token are sent in; see
/// <see cref="CredentialEncoding.ForPlatform" />.
/// </param>
/// <remarks>
/// Before the first response it answers only when exactly one scheme is allowed, Basic or
/// Bearer, as libcurl does: <c>-u u:p</c> alone sends <c>Basic dTpw</c>, and
/// <c>--anyauth</c> sends nothing until challenged. After a challenge it picks among the
/// allowed schemes offered in libcurl's order - Negotiate, Bearer, Digest, NTLM, Basic -
/// and answers only when the pick is Basic or Bearer; Digest, NTLM and Negotiate are not
/// built here, so a pick of one of them sends nothing.
/// </remarks>
public sealed class BasicAndBearerAuthenticator(Encoding credentialEncoding) : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        HttpAuthSchemes scheme = challenges.Count == 0
            ? request.AllowedSchemes
            : HttpAuthSchemeRanking.PickFirst(request.AllowedSchemes & HttpChallengeSchemes.Offered(challenges));

        return scheme switch
        {
            HttpAuthSchemes.Basic => CreateBasic(request),
            HttpAuthSchemes.Bearer => CreateBearer(request),
            _ => null,
        };
    }

    private string? CreateBasic(HttpAuthRequest request) =>
        request.Credential is { } credential
            ? "Basic " + Convert.ToBase64String(credentialEncoding.GetBytes(credential.UserName + ":" + credential.Password))
            : null;

    // The value is returned as one char per byte, which is how the HTTP handler writes a
    // header (Latin-1), so the token's bytes reach the wire unchanged.
    private string? CreateBearer(HttpAuthRequest request) =>
        request.BearerToken is { } token
            ? "Bearer " + Encoding.Latin1.GetString(credentialEncoding.GetBytes(token))
            : null;
}
