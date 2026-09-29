using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// A SASL exchange answered by one <see cref="ISecurityContext" /> (ADR-0184): NTLM, whose
/// initial response is the Type 1 message and whose one answer is the Type 3 message; and
/// GSSAPI (RFC 4752), whose tokens run until the context is established, after which the
/// server's wrapped security-layer offer is answered with a wrapped choice of no layer.
/// </summary>
/// <param name="mechanism">The SASL name of the mechanism.</param>
/// <param name="context">The context; the exchange disposes it once it has nothing more to send.</param>
/// <param name="securityLayerAuthorizationIdentity">
/// For GSSAPI, the authorization identity (<c>--sasl-authzid</c>) the security-layer message
/// carries, empty for none; <see langword="null" /> for NTLM, which has no security-layer
/// message.
/// </param>
internal sealed class SecurityContextSaslExchange(string mechanism, ISecurityContext context, byte[]? securityLayerAuthorizationIdentity) : ISaslExchange
{
    /// <summary>RFC 4752 section 3.3's bit for "no security layer".</summary>
    private const byte NoSecurityLayer = 0x01;

    private const string AuthErrorMessage = "An authentication function returned an error";

    private bool finished;

    /// <inheritdoc />
    public string Mechanism => mechanism;

    /// <summary>
    /// Gets the security context request for <paramref name="mechanism" /> to the SASL service on
    /// the server's host, as curl's <c>Curl_auth_build_spn</c> names it (<c>smtp/host</c>), with
    /// the explicit credential split as curl splits <c>DOMAIN\user</c>, or the default credentials
    /// for an empty user name.
    /// </summary>
    /// <param name="mechanism">The context's mechanism.</param>
    /// <param name="request">What the transfer may authenticate with.</param>
    /// <returns>The request.</returns>
    internal static SecurityContextRequest ContextRequestFor(SecurityMechanism mechanism, SaslRequest request)
    {
        SecurityContextRequest defaults = new(mechanism, request.ServiceName, request.Host);
        if (request.Credential is not { UserName.Length: > 0 } credential)
        {
            return defaults;
        }

        (string domain, string user) = NtlmUserName.SplitDomain(credential.UserName);
        string? credentialDomain = credential.Domain.Length == 0 ? null : credential.Domain;
        return defaults with { UserName = user, Password = credential.Password, Domain = domain.Length == 0 ? credentialDomain : domain };
    }

    /// <inheritdoc />
    /// <remarks>The context's first token.</remarks>
    /// <exception cref="SaslAuthenticationFailedException">
    /// The context cannot make its first token - no credentials, no KDC - and curl 8.21.0 fails
    /// the transfer with exit 94, sending nothing more (BL-856).
    /// </exception>
    public async ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) =>
        await StepAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false)
            ?? throw new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage);

    /// <inheritdoc />
    public async ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken)
    {
        if (finished)
        {
            return null;
        }

        if (!context.IsCompleted)
        {
            byte[]? token = await StepAsync(challenge, cancellationToken).ConfigureAwait(false);
            FinishUnless(token is not null && securityLayerAuthorizationIdentity is not null);
            return token;
        }

        byte[]? answer = securityLayerAuthorizationIdentity is null ? null : AnswerSecurityLayerOffer(challenge.Span, securityLayerAuthorizationIdentity);
        FinishUnless(false);
        return answer;
    }

    // RFC 4752 section 3.1: the offer unwraps to four bytes, the layers the server supports and
    // its largest buffer. curl takes no layer, which needs the server to offer it, and answers
    // the layer, a zero size and the authorization identity, wrapped without encryption.
    private byte[]? AnswerSecurityLayerOffer(ReadOnlySpan<byte> wrappedOffer, byte[] authorizationIdentity)
    {
        byte[]? offer = context.Unwrap(wrappedOffer);
        return offer is [var layers, _, _, _] && (layers & NoSecurityLayer) != 0
            ? context.Wrap([NoSecurityLayer, 0, 0, 0, .. authorizationIdentity], encrypt: false)
            : null;
    }

    private async ValueTask<byte[]?> StepAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
    {
        SecurityContextStep step = await context.NextTokenAsync(incomingToken, cancellationToken).ConfigureAwait(false);
        bool answered = step.Status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed;
        FinishUnless(answered);
        return answered ? step.Token : null;
    }

    private void FinishUnless(bool goesOn)
    {
        if (!goesOn && !finished)
        {
            finished = true;
            context.Dispose();
        }
    }
}
