using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>A principal's name without its realm (RFC 4120's <c>PrincipalName</c>).</summary>
/// <param name="NameType">The name type, e.g. 1 for <c>KRB_NT_PRINCIPAL</c>, 2 for <c>KRB_NT_SRV_INST</c>.</param>
/// <param name="Components">The name's components, e.g. <c>krbtgt</c> and <c>EXAMPLE.TEST</c>.</param>
public sealed record KerberosPrincipalName(int NameType, IReadOnlyList<string> Components)
{
    /// <summary>Reads a <c>PrincipalName</c>.</summary>
    internal static readonly Func<AsnReader, KerberosPrincipalName> Reader = new Func<AsnReader, KerberosPrincipalName>(Read);

    /// <summary>Writes a <c>PrincipalName</c>.</summary>
    internal static readonly Action<AsnWriter, KerberosPrincipalName> Writer = new Action<AsnWriter, KerberosPrincipalName>(Write);

    private static KerberosPrincipalName Read(AsnReader reader) =>
        KerberosAsn1.ReadSequence(reader, inner => new KerberosPrincipalName(
            KerberosAsn1.ReadField(inner, 0, KerberosAsn1.Int32Reader),
            KerberosAsn1.ReadSequenceOfField(inner, 1, KerberosAsn1.StringReader)));

    private static void Write(AsnWriter writer, KerberosPrincipalName name) =>
        KerberosAsn1.WriteSequence(writer, inner =>
        {
            KerberosAsn1.WriteField(inner, 0, name.NameType, KerberosAsn1.Int32Writer);
            KerberosAsn1.WriteSequenceOfField(inner, 1, name.Components, KerberosAsn1.StringWriter);
        });
}
