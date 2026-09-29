using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// The plaintext of encrypted-timestamp pre-authentication (RFC 4120's <c>PA-ENC-TS-ENC</c>):
/// the client's time, encrypted in its long-term key with key usage 1 into a
/// <c>PA-ENC-TIMESTAMP</c>.
/// </summary>
/// <param name="Timestamp">The client's time, to the second.</param>
/// <param name="Microseconds">The microseconds past <paramref name="Timestamp" />, or <see langword="null" /> when not given.</param>
public sealed record KerberosEncryptedTimestamp(DateTimeOffset Timestamp, int? Microseconds)
{
    /// <summary>Encodes the <c>PA-ENC-TS-ENC</c>.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.EncodeValue(this, (writer, timestamp) => KerberosAsn1.WriteSequence(writer, inner =>
    {
        KerberosAsn1.WriteField(inner, 0, timestamp.Timestamp, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(inner, 1, timestamp.Microseconds, KerberosAsn1.Int32Writer);
    }));

    /// <summary>Decodes a <c>PA-ENC-TS-ENC</c>.</summary>
    /// <param name="bytes">The encoding, as decrypted from a <c>PA-ENC-TIMESTAMP</c>.</param>
    /// <returns>The timestamp.</returns>
    /// <exception cref="KerberosMessageException"><see cref="KerberosMessageError.Malformed" />.</exception>
    public static KerberosEncryptedTimestamp Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.DecodeValue(bytes, reader => KerberosAsn1.ReadSequence(reader, inner => new KerberosEncryptedTimestamp(
            KerberosAsn1.ReadField(inner, 0, KerberosAsn1.TimeReader),
            KerberosAsn1.ReadOptionalValueField(inner, 1, KerberosAsn1.Int32Reader))));
}
