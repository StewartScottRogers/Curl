using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Record;

/// <summary>
/// Protects the records one side sends in one epoch with AES-128-GCM or AES-128-CCM_8 (RFC 5288, RFC 6655, RFC 6347
/// section 4.1.2): each record is the 13-byte header, the 8-byte explicit nonce, the ciphertext
/// and the 16-byte (CCM_8: 8-byte) tag over the additional data
/// <c>epoch || sequence_number || type || version || length</c>. Sequence numbers start at 0 and
/// count up by one a record.
/// </summary>
/// <param name="keys">The sending side's write keys, from <see cref="DtlsKeyBlock.Derive" />.</param>
/// <param name="epoch">The epoch the keys belong to, 1 after the first ChangeCipherSpec.</param>
/// <param name="bulkCipher">The suite's bulk cipher, AES-128-GCM unless given.</param>
internal sealed class DtlsRecordProtector(Tls12WriteKeys keys, ushort epoch, Tls12BulkCipher bulkCipher = Tls12BulkCipher.Aes128Gcm) : IDisposable
{
    /// <summary>The largest plaintext one record carries (RFC 5246 section 6.2.1).</summary>
    public const int MaximumPlaintextLength = 1 << 14;

    private readonly DtlsRecordAead aead = new(bulkCipher, keys.Key);

    private readonly byte[] writeIv = keys.Iv;

    /// <summary>Gets the sequence number the next record will carry.</summary>
    public ulong NextSequenceNumber { get; private set; }

    /// <summary>Protects <paramref name="plaintext" /> as the next record of <paramref name="contentType" />.</summary>
    /// <returns>The whole record, header first.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The plaintext is past 2^14 bytes, or the epoch's sequence numbers are spent.</exception>
    public byte[] Protect(TlsContentType contentType, ReadOnlySpan<byte> plaintext)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(plaintext.Length, MaximumPlaintextLength, nameof(plaintext));
        int fragmentLength = DtlsRecordNonce.ExplicitLength + plaintext.Length + aead.TagLength;
        DtlsRecordHeader header = new(contentType, DtlsRecordHeader.Dtls12Version, epoch, NextSequenceNumber, (ushort)fragmentLength);
        byte[] record = [.. header.Encode(), .. new byte[fragmentLength]];
        byte[] explicitPart = DtlsRecordNonce.ExplicitPart(header.EpochAndSequenceNumber);
        explicitPart.CopyTo(record, DtlsRecordHeader.HeaderLength);
        int ciphertextStart = DtlsRecordHeader.HeaderLength + DtlsRecordNonce.ExplicitLength;
        aead.Encrypt(
            DtlsRecordNonce.Combine(writeIv, explicitPart),
            plaintext,
            record.AsSpan(ciphertextStart, plaintext.Length),
            record.AsSpan(ciphertextStart + plaintext.Length),
            header.AdditionalData((ushort)plaintext.Length));
        NextSequenceNumber++;
        return record;
    }

    /// <inheritdoc />
    public void Dispose() => aead.Dispose();
}
