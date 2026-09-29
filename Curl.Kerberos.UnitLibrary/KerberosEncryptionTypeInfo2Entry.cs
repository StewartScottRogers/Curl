using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// One entry of <c>PA-ETYPE-INFO2</c> (RFC 4120's <c>ETYPE-INFO2-ENTRY</c>): an encryption
/// type the KDC holds a key in for the client, with the salt and string-to-key parameters
/// that make that key from the password.
/// </summary>
/// <param name="EncryptionType">The encryption type, e.g. 18 for <c>aes256-cts-hmac-sha1-96</c>.</param>
/// <param name="Salt">The salt, or <see langword="null" /> for the default (the realm followed by the name components).</param>
/// <param name="StringToKeyParameters">The string-to-key parameters, or <see langword="null" /> for the type's default.</param>
public sealed record KerberosEncryptionTypeInfo2Entry(int EncryptionType, string? Salt, byte[]? StringToKeyParameters)
{
    private static readonly Func<AsnReader, KerberosEncryptionTypeInfo2Entry> Reader = new Func<AsnReader, KerberosEncryptionTypeInfo2Entry>(Read);

    private static readonly Action<AsnWriter, KerberosEncryptionTypeInfo2Entry> Writer = new Action<AsnWriter, KerberosEncryptionTypeInfo2Entry>(Write);

    /// <summary>Encodes a <c>PA-ETYPE-INFO2</c> value: the SEQUENCE OF entries.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The DER encoding.</returns>
    public static byte[] EncodeList(IReadOnlyList<KerberosEncryptionTypeInfo2Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return KerberosAsn1.EncodeValue(entries, (writer, value) => KerberosAsn1.WriteSequenceOf(writer, value, Writer));
    }

    /// <summary>Decodes a <c>PA-ETYPE-INFO2</c> value.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.Malformed" />.</exception>
    public static IReadOnlyList<KerberosEncryptionTypeInfo2Entry> DecodeList(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.DecodeValue(bytes, reader => KerberosAsn1.ReadSequenceOf(reader, Reader));

    private static KerberosEncryptionTypeInfo2Entry Read(AsnReader reader) =>
        KerberosAsn1.ReadSequence(reader, inner => new KerberosEncryptionTypeInfo2Entry(
            KerberosAsn1.ReadField(inner, 0, KerberosAsn1.Int32Reader),
            KerberosAsn1.ReadOptionalField(inner, 1, KerberosAsn1.StringReader),
            KerberosAsn1.ReadOptionalField(inner, 2, KerberosAsn1.OctetsReader)));

    private static void Write(AsnWriter writer, KerberosEncryptionTypeInfo2Entry entry) =>
        KerberosAsn1.WriteSequence(writer, inner =>
        {
            KerberosAsn1.WriteField(inner, 0, entry.EncryptionType, KerberosAsn1.Int32Writer);
            KerberosAsn1.WriteOptionalField(inner, 1, entry.Salt, KerberosAsn1.StringWriter);
            KerberosAsn1.WriteOptionalField(inner, 2, entry.StringToKeyParameters, KerberosAsn1.OctetsWriter);
        });
}
