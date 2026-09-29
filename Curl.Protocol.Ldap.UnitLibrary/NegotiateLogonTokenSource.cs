using System.Net;
using System.Net.Security;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Produces the logged-on user's tokens with the BCL's <see cref="NegotiateAuthentication" />
/// and <see cref="CredentialCache.DefaultNetworkCredentials" />, asking for signing and
/// sealing as WinLDAP does (its NTLM negotiate message carries both flags).
/// </summary>
public sealed class NegotiateLogonTokenSource : ILdapLogonTokenSource
{
    /// <inheritdoc />
    public ILdapLogonAuthentication Start(LdapLogonPackage package, string targetName) =>
        new NegotiateLogonAuthentication(new NegotiateAuthentication(new NegotiateAuthenticationClientOptions
        {
            Package = package == LdapLogonPackage.Ntlm ? "NTLM" : "Negotiate",
            Credential = CredentialCache.DefaultNetworkCredentials,
            TargetName = targetName,
            RequiredProtectionLevel = ProtectionLevel.EncryptAndSign,
        }));

    /// <summary>One <see cref="NegotiateAuthentication" />'s tokens.</summary>
    private sealed class NegotiateLogonAuthentication(NegotiateAuthentication authentication) : ILdapLogonAuthentication
    {
        /// <inheritdoc />
        public bool IsAuthenticated => authentication.IsAuthenticated;

        /// <inheritdoc />
        public byte[]? NextToken(ReadOnlySpan<byte> challenge) => authentication.GetOutgoingBlob(challenge, out _);

        /// <inheritdoc />
        public void Dispose() => authentication.Dispose();
    }
}
