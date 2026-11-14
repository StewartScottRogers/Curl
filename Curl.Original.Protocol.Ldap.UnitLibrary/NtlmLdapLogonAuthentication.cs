using Curl.Ntlm;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// The complete NTLM authentication of an explicit-credential (<c>-u</c>) Sicily bind, as the
/// SASL security layer's keys: signs and seals each later LDAP message with
/// <see cref="NtlmSessionSecurity" /> from the exported session key the bind exchanged (BL-5142).
/// </summary>
/// <remarks>
/// The bind's tokens are already sent, so <see cref="NextToken" /> has nothing more to send.
/// A wrapped message is laid out as the current-user path's NTLM one: the 16-byte signature,
/// then the sealed bytes.
/// </remarks>
/// <param name="security">The client's session security, which this authentication now owns.</param>
internal sealed class NtlmLdapLogonAuthentication(NtlmSessionSecurity security) : ILdapLogonAuthentication
{
    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public byte[]? NextToken(ReadOnlySpan<byte> challenge) => null;

    /// <inheritdoc />
    public byte[] Wrap(ReadOnlySpan<byte> message)
    {
        byte[] sealedMessage = security.Seal(message, out byte[] signature);
        return [.. signature, .. sealedMessage];
    }

    /// <inheritdoc />
    public byte[]? Unwrap(ReadOnlySpan<byte> wrapped) =>
        wrapped.Length >= NtlmSessionSecurity.SignatureLength
        && security.TryUnseal(wrapped[NtlmSessionSecurity.SignatureLength..], wrapped[..NtlmSessionSecurity.SignatureLength], out byte[]? message)
            ? message
            : null;

    /// <inheritdoc />
    public void Dispose() => security.Dispose();
}
