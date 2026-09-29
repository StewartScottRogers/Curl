using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// The plaintext of a KRB-CRED (RFC 4120's <c>EncKrbCredPart</c>, <c>[APPLICATION 29]</c>):
/// the forwarded credentials' keys and details, with the sender's time. It writes and reads
/// no nonce and no addresses. <see cref="Dispose" /> zeroes every key.
/// </summary>
public sealed class KerberosEncryptedCredentialPart : IDisposable
{
    private const int ApplicationTag = 29;

    private static readonly Func<AsnReader, KerberosEncryptedCredentialPart> ContentsReader = new Func<AsnReader, KerberosEncryptedCredentialPart>(ReadContents);

    /// <summary>Gets one entry per ticket in the KRB-CRED, in the same order.</summary>
    public required IReadOnlyList<KerberosCredentialInfo> Credentials { get; init; }

    /// <summary>Gets the sender's time, to the second; <see langword="null" /> when not given.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Gets the microseconds past <see cref="Timestamp" />; <see langword="null" /> when not given.</summary>
    public int? Microseconds { get; init; }

    /// <summary>Encodes the part, ready to encrypt. The bytes hold the keys; the caller zeroes them.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode(ApplicationTag, writer =>
    {
        KerberosAsn1.WriteSequenceOfField(writer, 0, Credentials, KerberosCredentialInfo.Writer);
        KerberosAsn1.WriteOptionalValueField(writer, 2, Timestamp, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(writer, 3, Microseconds, KerberosAsn1.Int32Writer);
    });

    /// <summary>Decodes a part decrypted from a KRB-CRED.</summary>
    /// <param name="bytes">The plaintext.</param>
    /// <returns>The part; the caller disposes it.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" /> or <see cref="KerberosMessageError.UnexpectedMessage" />.
    /// </exception>
    public static KerberosEncryptedCredentialPart Decode(ReadOnlyMemory<byte> bytes) => KerberosAsn1.Decode(bytes, ApplicationTag, ContentsReader);

    /// <summary>Zeroes every credential's key.</summary>
    public void Dispose()
    {
        foreach (KerberosCredentialInfo credential in Credentials)
        {
            credential.Dispose();
        }
    }

    private static KerberosEncryptedCredentialPart ReadContents(AsnReader reader) => new()
    {
        Credentials = KerberosAsn1.ReadSequenceOfField(reader, 0, KerberosCredentialInfo.Reader),
        Timestamp = KerberosAsn1.ReadOptionalValueField(reader, 2, KerberosAsn1.TimeReader),
        Microseconds = KerberosAsn1.ReadOptionalValueField(reader, 3, KerberosAsn1.Int32Reader),
    };
}
