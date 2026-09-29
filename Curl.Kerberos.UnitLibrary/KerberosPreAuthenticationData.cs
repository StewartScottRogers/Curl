using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// One pre-authentication element (RFC 4120's <c>PA-DATA</c>): its type and its value, whose
/// encoding the type decides.
/// </summary>
/// <param name="DataType">The <c>padata-type</c>, e.g. <see cref="EncryptedTimestamp" />.</param>
/// <param name="Value">The <c>padata-value</c> bytes.</param>
public sealed record KerberosPreAuthenticationData(int DataType, byte[] Value)
{
    /// <summary><c>PA-TGS-REQ</c>: the value is an AP-REQ (<see cref="KerberosApRequest" />).</summary>
    public const int TgsRequest = 1;

    /// <summary><c>PA-ENC-TIMESTAMP</c>: the value is an <see cref="KerberosEncryptedData" /> of a <see cref="KerberosEncryptedTimestamp" />.</summary>
    public const int EncryptedTimestamp = 2;

    /// <summary><c>PA-ETYPE-INFO2</c>: the value is a list of <see cref="KerberosEncryptionTypeInfo2Entry" />.</summary>
    public const int EncryptionTypeInfo2 = 19;

    /// <summary>Reads a <c>PA-DATA</c>.</summary>
    internal static readonly Func<AsnReader, KerberosPreAuthenticationData> Reader = new Func<AsnReader, KerberosPreAuthenticationData>(Read);

    /// <summary>Writes a <c>PA-DATA</c>.</summary>
    internal static readonly Action<AsnWriter, KerberosPreAuthenticationData> Writer = new Action<AsnWriter, KerberosPreAuthenticationData>(Write);

    /// <summary>
    /// Encodes <c>METHOD-DATA</c>, the SEQUENCE OF <c>PA-DATA</c> a <c>KRB-ERROR</c>'s
    /// <c>e-data</c> carries.
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <returns>The DER encoding.</returns>
    public static byte[] EncodeMethodData(IReadOnlyList<KerberosPreAuthenticationData> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return KerberosAsn1.EncodeValue(elements, (writer, value) => KerberosAsn1.WriteSequenceOf(writer, value, Writer));
    }

    /// <summary>Decodes <c>METHOD-DATA</c>, e.g. a <c>KRB-ERROR</c>'s <c>e-data</c>.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The elements.</returns>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.Malformed" />.</exception>
    public static IReadOnlyList<KerberosPreAuthenticationData> DecodeMethodData(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.DecodeValue(bytes, reader => KerberosAsn1.ReadSequenceOf(reader, Reader));

    private static KerberosPreAuthenticationData Read(AsnReader reader) =>
        KerberosAsn1.ReadSequence(reader, inner => new KerberosPreAuthenticationData(
            KerberosAsn1.ReadField(inner, 1, KerberosAsn1.Int32Reader),
            KerberosAsn1.ReadField(inner, 2, KerberosAsn1.OctetsReader)));

    private static void Write(AsnWriter writer, KerberosPreAuthenticationData element) =>
        KerberosAsn1.WriteSequence(writer, inner =>
        {
            KerberosAsn1.WriteField(inner, 1, element.DataType, KerberosAsn1.Int32Writer);
            KerberosAsn1.WriteField(inner, 2, element.Value, KerberosAsn1.OctetsWriter);
        });
}
