using System.Buffers;
using System.Net;
using System.Net.Security;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Produces the logged-on user's tokens with the BCL's <see cref="NegotiateAuthentication" />
/// and <see cref="CredentialCache.DefaultNetworkCredentials" />, asking for signing and
/// sealing as WinLDAP does (its NTLM negotiate message carries both flags), and seals and
/// unseals the session after the bind with its <see cref="NegotiateAuthentication.Wrap" /> and
/// <see cref="NegotiateAuthentication.Unwrap" />.
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
        /// <remarks>
        /// Before the authentication is complete <see cref="NegotiateAuthentication" /> throws
        /// <see cref="InvalidOperationException" />; once it is, only a failing security package
        /// fails to seal, and its message is then empty, which the server refuses.
        /// </remarks>
        public byte[] Wrap(ReadOnlySpan<byte> message)
        {
            var sealedMessage = new ArrayBufferWriter<byte>();
            authentication.Wrap(message, sealedMessage, requestEncryption: true, out _);
            return sealedMessage.WrittenSpan.ToArray();
        }

        /// <inheritdoc />
        public byte[]? Unwrap(ReadOnlySpan<byte> wrapped)
        {
            var message = new ArrayBufferWriter<byte>();
            return authentication.Unwrap(wrapped, message, out _) == NegotiateAuthenticationStatusCode.Completed
                ? message.WrittenSpan.ToArray()
                : null;
        }

        /// <inheritdoc />
        public void Dispose() => authentication.Dispose();
    }
}
