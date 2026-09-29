using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers with <c>Authorization: Negotiate &lt;base64&gt;</c> as curl 8.21.0 does
/// (RFC 4559, ADR-0142, ADR-0173): the first token of a Negotiate context for the service
/// <c>HTTP</c> on the URL's host, from <paramref name="securityContexts" />. When no token
/// can be made - no ticket, no logged-on user's credential, no mechanism - it answers
/// nothing, and the transfer ends on the 401 with exit 0, as both platform curls do.
/// </summary>
/// <param name="securityContexts">Makes the Negotiate context; ADR-0142's router in production.</param>
/// <remarks>
/// An explicit <c>-u user:password</c> is passed on (a <c>DOMAIN\user</c> or
/// <c>DOMAIN/user</c> name split into its domain and user, as curl's SSPI build splits it),
/// where only SSPI uses it; <c>-u :</c> and no <c>-u</c> mean the default credentials.
/// </remarks>
public sealed class NegotiateHttpAuthenticator(ISecurityContextFactory securityContexts)
{
    /// <summary>The service name of an HTTP acceptor's principal, before <c>--service-name</c> changes it.</summary>
    public const string HttpServiceName = "HTTP";

    /// <summary>Makes the <c>Negotiate</c> header value for <paramref name="request" />.</summary>
    /// <param name="request">The request being authorised.</param>
    /// <param name="cancellationToken">Cancels a KDC exchange.</param>
    /// <returns>The header value, or <see langword="null" /> when no token can be made.</returns>
    public async ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using ISecurityContext context = securityContexts.Create(ContextRequestFor(request));
        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        return step.Status is SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed && step.Token.Length != 0
            ? "Negotiate " + Convert.ToBase64String(step.Token)
            : null;
    }

    /// <summary>Gets the security context request <paramref name="request" /> comes to.</summary>
    /// <param name="request">The request being authorised.</param>
    /// <returns>A Negotiate request for <c>HTTP</c> on the URL's host, with the explicit credential if any.</returns>
    internal static SecurityContextRequest ContextRequestFor(HttpAuthRequest request)
    {
        SecurityContextRequest defaults = new(SecurityMechanism.Negotiate, HttpServiceName, request.Url.IdnHost);
        if (request.Credential is not { UserName.Length: > 0 } credential)
        {
            return defaults;
        }

        (string? domain, string user) = SplitDomain(credential);
        return defaults with { UserName = user, Password = credential.Password, Domain = domain };
    }

    private static (string? Domain, string User) SplitDomain(NetworkCredential credential)
    {
        string name = credential.UserName;
        int separator = name.IndexOfAny(['\\', '/']);
        return separator < 0
            ? (credential.Domain.Length == 0 ? null : credential.Domain, name)
            : (name[..separator], name[(separator + 1)..]);
    }
}
