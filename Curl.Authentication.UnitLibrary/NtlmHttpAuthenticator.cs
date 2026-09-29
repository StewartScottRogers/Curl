using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with <c>Authorization: NTLM &lt;base64&gt;</c> as curl 8.21.0 does (ADR-0142,
/// ADR-0181): the Type 1 message first, the Type 3 message in answer to the server's Type 2
/// challenge, and nothing once Type 3 has been sent, from NTLM contexts for the service
/// <c>HTTP</c> on the URL's host that <paramref name="securityContexts" /> makes.
/// </summary>
/// <param name="securityContexts">Makes the NTLM contexts; ADR-0142's router in production.</param>
/// <param name="refusedChallengeFailsTransfer">
/// <see langword="true" /> where curl's SSPI build is matched (Windows): a Type 2 message the
/// context cannot answer fails the transfer with exit 94. <see langword="false" /> where
/// curl's own NTLM is (elsewhere): a Type 2 message it cannot read ends the transfer on the
/// 401, exit 0, and a <see cref="SecurityContextStatus.Refused" /> answer, which is a Type 3
/// message past curl's 1024-byte buffer, fails it with exit 100 (BL-849).
/// </param>
/// <remarks>
/// It keeps no context between legs. Type 3 comes from a new context stepped through its
/// Type 1 message and then the challenge; both routes write the same Type 1 for the same
/// credential every time, so SSPI's message integrity code over the three messages still
/// covers the Type 1 that was sent.
/// </remarks>
public sealed class NtlmHttpAuthenticator(ISecurityContextFactory securityContexts, bool refusedChallengeFailsTransfer)
{
    /// <summary>The message curl prints for exit 94, <see cref="CurlExitCode.AuthError" />.</summary>
    public const string AuthErrorMessage = "An authentication function returned an error";

    /// <summary>
    /// The message curl 8.18.0's own NTLM prints for exit 100, <see cref="CurlExitCode.TooLarge" />,
    /// when the Type 3 message would not fit its 1024-byte buffer (measured on Ubuntu, BL-849).
    /// </summary>
    public const string Type3TooLargeMessage = "user + domain + hostname too big for NTLM";

    private const string SchemePrefix = "NTLM ";

    private const byte AuthenticateMessageType = 3;

    /// <summary>
    /// Makes the <c>NTLM</c> header value that goes on from what was sent, as curl's
    /// <c>Curl_input_ntlm</c> and <c>Curl_output_ntlm</c> do: nothing after Type 3; Type 3 for a
    /// challenge carrying a Type 2 message; Type 1 before any challenge, for a bare <c>NTLM</c>
    /// challenge to a request that sent none, and once more for one to the Type 1 sent before
    /// any challenge; after that, nothing.
    /// </summary>
    /// <param name="request">The request being authorised.</param>
    /// <param name="sentAuthorization">The <c>NTLM</c> value the request that drew the challenges sent, or <see langword="null" />.</param>
    /// <param name="sentBeforeAnyChallenge">Whether <paramref name="sentAuthorization" /> was sent before any challenge.</param>
    /// <param name="challenges">The response's challenges; empty before the first request.</param>
    /// <param name="cancellationToken">Cancels the context's steps.</param>
    /// <returns>The header value, or <see langword="null" /> to send none.</returns>
    /// <exception cref="HttpAuthenticationFailedException">
    /// The context cannot answer the Type 2 message and <c>refusedChallengeFailsTransfer</c> is set
    /// (exit 94), or it is not set and the context refuses the answer because the Type 3 message
    /// would not fit curl's buffer (exit 100).
    /// </exception>
    public async ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, string? sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);
        if (sentAuthorization is not null && CarriesAuthenticateMessage(sentAuthorization))
        {
            return null;
        }

        string? token = HttpChallengeSchemes.NtlmTokenOf(challenges);
        if (string.IsNullOrEmpty(token))
        {
            return sentAuthorization is null || sentBeforeAnyChallenge ? await CreateNegotiateAsync(request, cancellationToken).ConfigureAwait(false) : null;
        }

        return DecodeBase64(token) is { } challenge ? await CreateAuthenticateAsync(request, challenge, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <summary>Gets the security context request <paramref name="request" /> comes to.</summary>
    /// <param name="request">The request being authorised.</param>
    /// <returns>
    /// An NTLM request for <c>HTTP</c> on the URL's host, with the explicit credential split
    /// as curl splits <c>DOMAIN\user</c> (<see cref="NtlmUserName.SplitDomain" />), or the
    /// default credentials for <c>-u :</c>.
    /// </returns>
    internal static SecurityContextRequest ContextRequestFor(HttpAuthRequest request)
    {
        SecurityContextRequest defaults = new(SecurityMechanism.Ntlm, NegotiateHttpAuthenticator.HttpServiceName, request.Url.IdnHost);
        if (request.Credential is not { UserName.Length: > 0 } credential)
        {
            return defaults;
        }

        (string domain, string user) = NtlmUserName.SplitDomain(credential.UserName);
        string? credentialDomain = credential.Domain.Length == 0 ? null : credential.Domain;
        return defaults with { UserName = user, Password = credential.Password, Domain = domain.Length == 0 ? credentialDomain : domain };
    }

    private static bool CarriesAuthenticateMessage(string authorization)
    {
        byte[]? message = authorization.StartsWith(SchemePrefix, StringComparison.Ordinal) ? DecodeBase64(authorization[SchemePrefix.Length..]) : null;
        return message is { Length: > 8 } && message[8] == AuthenticateMessageType;
    }

    private static byte[]? DecodeBase64(string text)
    {
        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? HeaderOf(SecurityContextStep step) =>
        step.Status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed && step.Token.Length != 0
            ? SchemePrefix + Convert.ToBase64String(step.Token)
            : null;

    private async ValueTask<string?> CreateNegotiateAsync(HttpAuthRequest request, CancellationToken cancellationToken)
    {
        using ISecurityContext context = securityContexts.Create(ContextRequestFor(request));
        return HeaderOf(await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<string?> CreateAuthenticateAsync(HttpAuthRequest request, byte[] challenge, CancellationToken cancellationToken)
    {
        using ISecurityContext context = securityContexts.Create(ContextRequestFor(request));
        SecurityContextStep negotiate = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        SecurityContextStep authenticate = negotiate.Status == SecurityContextStatus.ContinueNeeded
            ? await context.NextTokenAsync(challenge, cancellationToken).ConfigureAwait(false)
            : negotiate;
        string? header = HeaderOf(authenticate);
        if (header is null && refusedChallengeFailsTransfer)
        {
            throw new HttpAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage);
        }

        if (header is null && authenticate.Status == SecurityContextStatus.Refused)
        {
            throw new HttpAuthenticationFailedException(CurlExitCode.TooLarge, Type3TooLargeMessage);
        }

        return header;
    }
}
