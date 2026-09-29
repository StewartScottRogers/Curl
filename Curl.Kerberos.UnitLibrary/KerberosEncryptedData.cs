using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>Ciphertext and what it was encrypted with (RFC 4120's <c>EncryptedData</c>).</summary>
/// <param name="EncryptionType">The encryption type, e.g. 18 for <c>aes256-cts-hmac-sha1-96</c>.</param>
/// <param name="KeyVersionNumber">The key's version number, or <see langword="null" /> when the sender gave none.</param>
/// <param name="Cipher">The ciphertext, as <see cref="KerberosEncryption.Encrypt" /> makes it.</param>
public sealed record KerberosEncryptedData(int EncryptionType, uint? KeyVersionNumber, byte[] Cipher)
{
    /// <summary>Reads an <c>EncryptedData</c>.</summary>
    internal static readonly Func<AsnReader, KerberosEncryptedData> Reader = new Func<AsnReader, KerberosEncryptedData>(Read);

    /// <summary>Writes an <c>EncryptedData</c>.</summary>
    internal static readonly Action<AsnWriter, KerberosEncryptedData> Writer = new Action<AsnWriter, KerberosEncryptedData>(Write);

    /// <summary>Encodes the <c>EncryptedData</c> alone, as <c>PA-ENC-TIMESTAMP</c>'s value carries it.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.EncodeValue(this, Writer);

    /// <summary>Decodes an <c>EncryptedData</c> that fills <paramref name="bytes" />, e.g. a <c>PA-ENC-TIMESTAMP</c> value.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The encrypted data.</returns>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.Malformed" />.</exception>
    public static KerberosEncryptedData Decode(ReadOnlyMemory<byte> bytes) => KerberosAsn1.DecodeValue(bytes, Reader);

    private static KerberosEncryptedData Read(AsnReader reader) =>
        KerberosAsn1.ReadSequence(reader, inner => new KerberosEncryptedData(
            KerberosAsn1.ReadField(inner, 0, KerberosAsn1.Int32Reader),
            KerberosAsn1.ReadOptionalValueField(inner, 1, KerberosAsn1.UInt32Reader),
            KerberosAsn1.ReadField(inner, 2, KerberosAsn1.OctetsReader)));

    private static void Write(AsnWriter writer, KerberosEncryptedData data) =>
        KerberosAsn1.WriteSequence(writer, inner =>
        {
            KerberosAsn1.WriteField(inner, 0, data.EncryptionType, KerberosAsn1.Int32Writer);
            KerberosAsn1.WriteOptionalValueField(inner, 1, data.KeyVersionNumber, KerberosAsn1.UInt32Writer);
            KerberosAsn1.WriteField(inner, 2, data.Cipher, KerberosAsn1.OctetsWriter);
        });
}
