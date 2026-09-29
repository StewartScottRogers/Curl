using System.Formats.Asn1;

namespace Curl.Authentication;

/// <summary>The ASN.1 tags SPNEGO's tokens are built from (RFC 2743 section 3.1, RFC 4178 section 4.2).</summary>
internal static class SpnegoAsn1Tags
{
    /// <summary>Gets <c>[APPLICATION 0]</c>, the GSS-API initial-context-token framing.</summary>
    public static Asn1Tag InitialContextToken { get; } = new(TagClass.Application, 0, isConstructed: true);

    /// <summary>Gets <c>[0]</c>, the NegotiationToken choice that holds a NegTokenInit.</summary>
    public static Asn1Tag NegTokenInit { get; } = Field(0);

    /// <summary>Gets <c>[1]</c>, the NegotiationToken choice that holds a NegTokenResp.</summary>
    public static Asn1Tag NegTokenResp { get; } = Field(1);

    /// <summary>Gets the explicit context-specific tag of a SEQUENCE field.</summary>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The constructed <c>[number]</c> tag.</returns>
    public static Asn1Tag Field(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);
}
