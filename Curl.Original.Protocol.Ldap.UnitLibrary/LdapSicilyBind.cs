namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Builds the NTLM (Sicily) BindRequest curl's WinLDAP build sends for <c>--ntlm</c> and
/// <c>--negotiate</c> (MS-ADTS 5.1.1.1.3): name <c>NTLM</c>, authentication choice <c>[10]</c>
/// (tag <c>0x8a</c>) carrying an NTLMSSP negotiate message. No password is an input, so none can
/// reach the wire: the secret only ever enters the later NTLM response.
/// </summary>
internal static class LdapSicilyBind
{
    /// <summary>The BindRequest's name for a Sicily NTLM bind.</summary>
    private const string NtlmName = "NTLM";

    /// <summary>The authentication choice's tag number for <c>sicilyNegotiate</c>.</summary>
    private const int SicilyNegotiateChoice = 10;

    /// <summary>The NTLMSSP signature, then the little-endian message type 1 (negotiate).</summary>
    private static ReadOnlySpan<byte> NegotiateHeader => "NTLMSSP\0\x01\0\0\0"u8;

    /// <summary>Encodes the Sicily negotiate BindRequest as one whole LDAPMessage.</summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="negotiateMessage">The NTLMSSP negotiate (type 1) message.</param>
    /// <returns>The LDAPMessage.</returns>
    /// <exception cref="ArgumentException"><paramref name="negotiateMessage" /> is not an NTLMSSP negotiate message.</exception>
    public static byte[] NegotiateRequest(LdapBerWriter writer, int messageId, ReadOnlySpan<byte> negotiateMessage)
    {
        if (!negotiateMessage.StartsWith(NegotiateHeader))
        {
            throw new ArgumentException("The token is not an NTLMSSP negotiate message.", nameof(negotiateMessage));
        }

        return LdapRequests.SicilyBind(writer, messageId, NtlmName, SicilyNegotiateChoice, negotiateMessage);
    }
}
