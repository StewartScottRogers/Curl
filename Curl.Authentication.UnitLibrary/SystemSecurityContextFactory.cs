using System.Net;
using System.Net.Security;
using System.Security.Principal;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Makes contexts over the BCL's <see cref="NegotiateAuthentication" />: SSPI on Windows, the
/// system GSS-API library elsewhere (ADR-0142's W and G), with the package the mechanism
/// names, the target <c>service/host</c>, and the logged-on user's credentials unless the
/// request names a user.
/// </summary>
public sealed class SystemSecurityContextFactory : ISecurityContextFactory
{
    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SystemSecurityContext(OptionsFor(request));
    }

    /// <summary>Gets the client options <paramref name="request" /> asks for.</summary>
    /// <param name="request">The mechanism, acceptor and credential.</param>
    /// <returns>The options.</returns>
    internal static NegotiateAuthenticationClientOptions OptionsFor(SecurityContextRequest request) => new()
    {
        Package = PackageOf(request.Mechanism),
        TargetName = $"{request.ServiceName}/{request.HostName}",
        Credential = request.UserName is null
            ? CredentialCache.DefaultNetworkCredentials
            : new NetworkCredential(request.UserName, request.Password, request.Domain),
        AllowedImpersonationLevel = request.Delegation == SecurityDelegation.None
            ? TokenImpersonationLevel.None
            : TokenImpersonationLevel.Delegation,
        RequiredProtectionLevel = request.MessageProtection,
    };

    private static string PackageOf(SecurityMechanism mechanism) => mechanism switch
    {
        SecurityMechanism.Ntlm => "NTLM",
        SecurityMechanism.Kerberos => "Kerberos",
        _ => "Negotiate",
    };
}
