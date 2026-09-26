namespace Curl.Protocol.Abstractions;

/// <summary>
/// Answers an HTTP authentication challenge with the value of an <c>Authorization</c> or
/// <c>Proxy-Authorization</c> header (ADR-0014).
/// </summary>
/// <remarks>
/// Implemented in <c>Curl.Authentication.UnitLibrary</c> and handed to the HTTP handler by
/// <c>Curl.Console</c>, so the handler never references the implementation. The call is
/// synchronous, does no I/O and keeps no state between calls; NTLM and Negotiate, which
/// need state across a connection's handshake legs, need a later ADR.
/// </remarks>
public interface IHttpAuthenticator
{
    /// <summary>
    /// Creates the authorization header value for <paramref name="request" />.
    /// </summary>
    /// <param name="request">
    /// The request being authorised, and what it may be authorised with.
    /// </param>
    /// <param name="challenges">
    /// The value of every <c>WWW-Authenticate</c> header, or <c>Proxy-Authenticate</c> when
    /// <see cref="HttpAuthRequest.IsProxy" /> is <see langword="true" />, from the response
    /// being answered, verbatim and in the order received. Empty before the first response,
    /// which is how Basic and Bearer are sent pre-emptively.
    /// </param>
    /// <returns>
    /// The header value only, for example <c>Basic dTpw</c>, without the header name or line
    /// ending; or <see langword="null" /> to send no header, when there is no credential or
    /// token, no allowed scheme, or no challenge the authenticator can answer.
    /// </returns>
    string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges);
}
