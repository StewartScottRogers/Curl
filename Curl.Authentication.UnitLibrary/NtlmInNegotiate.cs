using System.Formats.Asn1;

namespace Curl.Authentication;

/// <summary>
/// Tells whether a first Negotiate token carries NTLM: a bare NTLMSSP message, as SSPI sends
/// for an IP address, or a SPNEGO NegTokenInit whose optimistic <c>mechToken</c> is one, as
/// it sends for a name (ADR-0173).
/// </summary>
internal static class NtlmInNegotiate
{
    /// <summary>The signature every NTLM message starts with (MS-NLMP section 2.2).</summary>
    private static ReadOnlySpan<byte> NtlmSignature => "NTLMSSP\0"u8;

    /// <summary>Gets whether <paramref name="token" /> carries an NTLM message rather than a Kerberos one.</summary>
    /// <param name="token">The initiator's first Negotiate token.</param>
    /// <returns><see langword="true" /> when the token, or its SPNEGO <c>mechToken</c>, is an NTLM message.</returns>
    public static bool Carries(byte[] token) =>
        token.AsSpan().StartsWith(NtlmSignature) || MechanismTokenOf(token).AsSpan().StartsWith(NtlmSignature);

    private static byte[] MechanismTokenOf(byte[] token)
    {
        try
        {
            AsnReader contents = new AsnReader(token, AsnEncodingRules.BER).ReadSequence(SpnegoAsn1Tags.InitialContextToken);
            if (contents.ReadObjectIdentifier() != SpnegoMechanism.Spnego)
            {
                return [];
            }

            AsnReader fields = contents.ReadSequence(SpnegoAsn1Tags.NegTokenInit).ReadSequence();
            while (fields.HasData && !fields.PeekTag().HasSameClassAndValue(SpnegoAsn1Tags.Field(2)))
            {
                fields.ReadEncodedValue();
            }

            return fields.HasData ? fields.ReadSequence(SpnegoAsn1Tags.Field(2)).ReadOctetString() : [];
        }
        catch (AsnContentException)
        {
            return [];
        }
    }
}
