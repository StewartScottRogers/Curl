namespace Curl.Protocol.Abstractions;

/// <summary>
/// Answers an HTTP authentication challenge with the value of an <c>Authorization</c> or
/// <c>Proxy-Authorization</c> header (ADR-0014).
/// </summary>
/// <remarks>
/// Implemented in <c>Curl.Authentication.UnitLibrary</c> and handed to the HTTP handler by
/// <c>Curl.Console</c>, so the handler never references the implementation.
/// <see cref="CreateAuthorization" /> does no I/O; <see cref="CreateAuthorizationAsync" />,
/// which the HTTP and WebSocket handlers call, may, as Negotiate asks a KDC (ADR-0176). A
/// handshake of more than one leg (NTLM, Negotiate) goes on through
/// <see cref="ContinueAuthorizationAsync" />, which is told what the last request sent
/// (ADR-0181). The only state kept between calls is a Negotiate context awaiting its next
/// leg, found again by the value its token made (ADR-0227).
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
    /// token, no allowed scheme, or no challenge the authenticator can answer. A value may go
    /// on with whole header lines, each after a CRLF, which are sent right after the
    /// <c>Authorization</c> line, as <c>--aws-sigv4</c> sends <c>X-Amz-Date</c> and
    /// <c>x-amz-content-sha256</c> there (BL-629, ADR-0243).
    /// </returns>
    string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges);

    /// <summary>
    /// Creates the authorization header value for <paramref name="request" />, doing whatever
    /// I/O the scheme needs, as Negotiate asks a KDC for a service ticket (ADR-0176). By
    /// default it returns <see cref="CreateAuthorization" />'s answer.
    /// </summary>
    /// <param name="request">The request being authorised, and what it may be authorised with.</param>
    /// <param name="challenges">As for <see cref="CreateAuthorization" />.</param>
    /// <param name="cancellationToken">Cancels the I/O.</param>
    /// <returns>
    /// As for <see cref="CreateAuthorization" />; or, answering a challenge, the empty string to
    /// send the request again without the header, as curl 8.21.0 does when <c>--anyauth</c>
    /// picks Negotiate and its context makes no token (ADR-0232).
    /// </returns>
    /// <exception cref="HttpAuthenticationFailedException">
    /// curl fails the transfer instead of sending the request, as it fails an
    /// <c>--aws-sigv4</c> it cannot sign with (BL-629).
    /// </exception>
    ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
        ValueTask.FromResult(CreateAuthorization(request, challenges));

    /// <summary>
    /// Creates the authorization header value that answers a challenge to a request that
    /// already sent one, as NTLM answers its Type 2 challenge to the Type 1 message it sent
    /// (ADR-0181), or a Negotiate context its acceptor's token (ADR-0227). By default it
    /// answers nothing, so a credential sent and refused ends the transfer on the response, as
    /// curl 8.21.0 does for Basic, Digest and a Negotiate context with nothing more to say.
    /// </summary>
    /// <param name="request">The request being authorised, and what it may be authorised with.</param>
    /// <param name="sentAuthorization">
    /// The header value the request that drew the challenges sent; empty when it was sent again
    /// without one on <see cref="CreateAuthorizationAsync" />'s empty answer (ADR-0232).
    /// </param>
    /// <param name="sentBeforeAnyChallenge">
    /// <see langword="true" /> when <paramref name="sentAuthorization" /> was sent on the
    /// transfer's first request, before any challenge; <see langword="false" /> when it answered one.
    /// </param>
    /// <param name="challenges">The response's challenges, as for <see cref="CreateAuthorization" />; never empty.</param>
    /// <param name="cancellationToken">Cancels the I/O.</param>
    /// <returns>
    /// The next header value, or <see langword="null" /> to take the response as the result;
    /// the HTTP handler takes an empty value as <see langword="null" />, so a request is never
    /// sent again without a header twice (ADR-0232).
    /// </returns>
    /// <exception cref="HttpAuthenticationFailedException">curl fails the transfer instead of taking the response.</exception>
    ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(null);

    /// <summary>
    /// Creates the header value that sends an answer already sent once more, on a request that
    /// keeps it while it answers the other party's challenge, as a proxy's answer is kept on
    /// the retry that answers the origin's 401. By default it is the value as sent; Digest
    /// counts its nonce on (<c>nc=00000002</c>) with the same <c>cnonce</c> and a new hash,
    /// as curl 8.21.0 does (BL-869).
    /// </summary>
    /// <param name="request">The request being authorised, and what it may be authorised with.</param>
    /// <param name="sentAuthorization">The header value the last request sent; empty when it was sent without one (ADR-0232).</param>
    /// <returns>The header value to send again.</returns>
    string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization) => sentAuthorization;
}
