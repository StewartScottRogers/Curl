using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// One direction's AES-128-GCM record protection (RFC 5288 section 3, RFC 6347 section 4.1.2.1): the nonce is the
/// 4-byte write IV and the 8-byte explicit nonce the record carries, here its epoch and sequence number; the
/// additional data is epoch, sequence number, content type, version and plaintext length.
/// </summary>
/// <param name="key">The 16-byte write key.</param>
/// <param name="salt">The 4-byte write IV.</param>
public sealed class DtlsReferenceRecordCipher(byte[] key, byte[] salt)
{
    private const int ExplicitNonceLength = 8;

    private const int TagLength = 16;

    /// <summary>Protects <paramref name="plaintext" /> as the fragment of a record.</summary>
    /// <param name="contentType">The record's content type.</param>
    /// <param name="epoch">The record's epoch.</param>
    /// <param name="sequenceNumber">The record's sequence number.</param>
    /// <param name="plaintext">The plaintext.</param>
    /// <returns>The protected record.</returns>
    public DtlsReferenceRecord Seal(byte contentType, ushort epoch, ulong sequenceNumber, byte[] plaintext)
    {
        byte[] explicitNonce = new byte[ExplicitNonceLength];
        BinaryPrimitives.WriteUInt64BigEndian(explicitNonce, ((ulong)epoch << 48) | sequenceNumber);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagLength];
        using AesGcm aes = new(key, TagLength);
        byte[] nonce = [.. salt, .. explicitNonce];
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AdditionalData(contentType, epoch, sequenceNumber, plaintext.Length));
        return new DtlsReferenceRecord(contentType, epoch, sequenceNumber, [.. explicitNonce, .. ciphertext, .. tag]);
    }

    /// <summary>Gives the plaintext of a protected record.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="AuthenticationTagMismatchException">The record does not authenticate.</exception>
    public byte[] Open(DtlsReferenceRecord record)
    {
        byte[] fragment = record.Fragment;
        int plaintextLength = fragment.Length - ExplicitNonceLength - TagLength;
        byte[] plaintext = new byte[plaintextLength];
        using AesGcm aes = new(key, TagLength);
        aes.Decrypt(
            [.. salt, .. fragment.AsSpan(0, ExplicitNonceLength)],
            fragment.AsSpan(ExplicitNonceLength, plaintextLength),
            fragment.AsSpan(ExplicitNonceLength + plaintextLength),
            plaintext,
            AdditionalData(record.ContentType, record.Epoch, record.SequenceNumber, plaintextLength));
        return plaintext;
    }

    private static byte[] AdditionalData(byte contentType, ushort epoch, ulong sequenceNumber, int plaintextLength)
    {
        byte[] data = new byte[13];
        BinaryPrimitives.WriteUInt64BigEndian(data, ((ulong)epoch << 48) | sequenceNumber);
        data[8] = contentType;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(9), DtlsReferenceRecord.Dtls12Version);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(11), (ushort)plaintextLength);
        return data;
    }
}
