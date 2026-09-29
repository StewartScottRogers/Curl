using System.Formats.Asn1;

namespace Curl.Authentication;

/// <summary>
/// Encodes the first token of a SPNEGO exchange: a NegTokenInit (RFC 4178 section 4.2.1)
/// inside the GSS-API initial-context-token framing (RFC 2743 section 3.1), the bytes an
/// HTTP <c>Authorization: Negotiate</c> header carries base64-encoded (RFC 4559).
/// </summary>
/// <remarks>
/// It writes <c>mechTypes</c> and <c>mechToken</c> only, as MIT's library does for curl:
/// no <c>reqFlags</c> and no <c>mechListMIC</c>.
/// </remarks>
internal static class SpnegoInitialToken
{
    /// <summary>Encodes the initial token.</summary>
    /// <param name="mechanismTypes">The mechanisms offered, most preferred first, as dotted object identifiers.</param>
    /// <param name="mechanismToken">The first mechanism's own initial token, such as a Kerberos AP-REQ.</param>
    /// <returns>The DER encoding.</returns>
    public static byte[] Encode(IReadOnlyList<string> mechanismTypes, ReadOnlySpan<byte> mechanismToken)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(SpnegoAsn1Tags.InitialContextToken))
        {
            writer.WriteObjectIdentifier(SpnegoMechanism.Spnego);
            using (writer.PushSequence(SpnegoAsn1Tags.NegTokenInit))
            using (writer.PushSequence())
            {
                using (writer.PushSequence(SpnegoAsn1Tags.Field(0)))
                using (writer.PushSequence())
                {
                    foreach (string mechanismType in mechanismTypes)
                    {
                        writer.WriteObjectIdentifier(mechanismType);
                    }
                }

                using (writer.PushSequence(SpnegoAsn1Tags.Field(2)))
                {
                    writer.WriteOctetString(mechanismToken);
                }
            }
        }

        return writer.Encode();
    }
}
