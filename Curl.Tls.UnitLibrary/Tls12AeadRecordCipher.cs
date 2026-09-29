using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// AEAD record protection in TLS 1.2 (RFC 5246 section 6.2.3.3). GCM and CCM (RFC 5288, RFC
/// 6209, RFC 6655) build the nonce from a 4-byte salt and an 8-byte explicit nonce sent at the
/// front of the record, here the sequence number as RFC 5288 section 3 allows;
/// ChaCha20-Poly1305 (RFC 7905) XORs the sequence number into a 12-byte IV and sends no
/// explicit nonce. The additional data is the sequence number, type, version and
/// plaintext length; the tag, 16 bytes or CCM8's 8, ends the record.
/// </summary>
internal sealed class Tls12AeadRecordCipher(ITlsAead aead, byte[] keyBlockIv, bool hasExplicitNonce, int tagLength) : Tls12RecordCipher
{
    private const int NonceLength = 12;

    private const int SequenceNumberOffset = NonceLength - sizeof(ulong);

    private readonly byte[] fixedIv = keyBlockIv.ToArray();

    private int ExplicitNonceLength => hasExplicitNonce ? sizeof(ulong) : 0;

    public override byte[] Seal(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> content)
    {
        int explicitLength = ExplicitNonceLength;
        byte[] fragment = new byte[explicitLength + content.Length + tagLength];
        Span<byte> nonce = stackalloc byte[NonceLength];
        WriteNonce(sequenceNumber, nonce);
        nonce[(NonceLength - explicitLength)..].CopyTo(fragment);
        Span<byte> additionalData = stackalloc byte[AdditionalDataLength];
        WriteAdditionalData(additionalData, sequenceNumber, contentType, version, content.Length);
        aead.Encrypt(nonce, content, fragment.AsSpan(explicitLength, content.Length), fragment.AsSpan(explicitLength + content.Length), additionalData);
        return fragment;
    }

    public override TlsDecodeResult<byte[]> Open(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment)
    {
        int explicitLength = ExplicitNonceLength;
        if (fragment.Length < explicitLength + tagLength)
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        Span<byte> nonce = stackalloc byte[NonceLength];
        WriteNonce(sequenceNumber, nonce);
        fragment[..explicitLength].CopyTo(nonce[(NonceLength - explicitLength)..]);
        ReadOnlySpan<byte> ciphertext = fragment[explicitLength..^tagLength];
        Span<byte> additionalData = stackalloc byte[AdditionalDataLength];
        WriteAdditionalData(additionalData, sequenceNumber, contentType, version, ciphertext.Length);
        byte[] plaintext = new byte[ciphertext.Length];
        return aead.TryDecrypt(nonce, ciphertext, fragment[^tagLength..], plaintext, additionalData)
            ? TlsDecodeResult<byte[]>.Success(plaintext)
            : TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
    }

    public override void Dispose() => aead.Dispose();

    /// <summary>
    /// Writes the sequence number into the nonce's last eight bytes and XORs the key
    /// block IV over its front: the GCM and CCM 4-byte salt lands before the sequence number,
    /// ChaCha20-Poly1305's 12-byte IV is XORed with it.
    /// </summary>
    private void WriteNonce(ulong sequenceNumber, Span<byte> nonce)
    {
        nonce.Clear();
        BinaryPrimitives.WriteUInt64BigEndian(nonce[SequenceNumberOffset..], sequenceNumber);
        for (int index = 0; index < fixedIv.Length; index++)
        {
            nonce[index] ^= fixedIv[index];
        }
    }
}
