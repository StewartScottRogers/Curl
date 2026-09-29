using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>One entry of a reply's <c>LastReq</c> (RFC 4120 section 5.4.2): when something last happened.</summary>
/// <param name="Type">The <c>lr-type</c>, e.g. 0 for no information, 6 for password expiry.</param>
/// <param name="Value">The <c>lr-value</c>.</param>
public sealed record KerberosLastRequest(int Type, DateTimeOffset Value)
{
    /// <summary>Reads one <c>LastReq</c> entry.</summary>
    internal static readonly Func<AsnReader, KerberosLastRequest> Reader = new Func<AsnReader, KerberosLastRequest>(Read);

    /// <summary>Writes one <c>LastReq</c> entry.</summary>
    internal static readonly Action<AsnWriter, KerberosLastRequest> Writer = new Action<AsnWriter, KerberosLastRequest>(Write);

    private static KerberosLastRequest Read(AsnReader reader) =>
        KerberosAsn1.ReadSequence(reader, inner => new KerberosLastRequest(
            KerberosAsn1.ReadField(inner, 0, KerberosAsn1.Int32Reader),
            KerberosAsn1.ReadField(inner, 1, KerberosAsn1.TimeReader)));

    private static void Write(AsnWriter writer, KerberosLastRequest entry) =>
        KerberosAsn1.WriteSequence(writer, inner =>
        {
            KerberosAsn1.WriteField(inner, 0, entry.Type, KerberosAsn1.Int32Writer);
            KerberosAsn1.WriteField(inner, 1, entry.Value, KerberosAsn1.TimeWriter);
        });
}
