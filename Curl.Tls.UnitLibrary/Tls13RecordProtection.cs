using System.Buffers;
using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// One direction of the TLS 1.3 record layer under one traffic secret (RFC 8446 section
/// 5.2): the AEAD key and IV the secret derives, and the sequence number that starts at
/// zero with the secret. Each record's nonce is the IV XORed with the sequence number, its
/// additional data is its own header, and its plaintext is the content, the real content
/// type and any zero padding. Records are never padded here, as none of the curl builds
/// pads them; padding a peer sends is removed.
/// </summary>
public sealed class Tls13RecordProtection : IDisposable
{
    /// <summary>The longest content one record carries (RFC 8446 section 5.1).</summary>
    public const int MaximumPlaintextLength = 1 << 14;

    /// <summary>The longest fragment a protected record may carry (RFC 8446 section 5.2).</summary>
    public const int MaximumCiphertextLength = MaximumPlaintextLength + 256;

    /// <summary>The length of a record header: content type, legacy version and length.</summary>
    public const int RecordHeaderLength = 5;

    /// <summary>The <c>legacy_record_version</c> every protected record carries.</summary>
    public const ushort LegacyRecordVersion = 0x0303;

    private const int TagLength = 16;

    private readonly ITlsAead aead;

    private readonly byte[] iv;

    private Tls13RecordProtection(ITlsAead aead, byte[] iv)
    {
        this.aead = aead;
        this.iv = iv;
    }

    /// <summary>Gets the sequence number the next record is protected or checked with.</summary>
    public ulong SequenceNumber { get; private set; }

    /// <summary>
    /// Returns whether the record layer can protect records of <paramref name="cipherSuite" />:
    /// <c>TLS_AES_128_GCM_SHA256</c>, <c>TLS_AES_256_GCM_SHA384</c> and
    /// <c>TLS_CHACHA20_POLY1305_SHA256</c>. The CCM suites wait for the hand-built AES-CCM.
    /// </summary>
    /// <param name="cipherSuite">The cipher suite code point.</param>
    /// <returns>Whether <see cref="Create" /> accepts the suite.</returns>
    public static bool CanProtect(ushort cipherSuite) => cipherSuite is 0x1301 or 0x1302 or 0x1303;

    /// <summary>Creates the protection a traffic secret gives, with the sequence number at zero.</summary>
    /// <param name="cipherSuite">The negotiated suite.</param>
    /// <param name="trafficSecret">The traffic secret.</param>
    /// <returns>The protection.</returns>
    /// <exception cref="ArgumentException">The record layer cannot protect records of the suite (<see cref="CanProtect" />).</exception>
    public static Tls13RecordProtection Create(Tls13CipherSuite cipherSuite, byte[] trafficSecret)
    {
        ArgumentNullException.ThrowIfNull(cipherSuite);
        ArgumentNullException.ThrowIfNull(trafficSecret);
        if (!CanProtect(cipherSuite.Code))
        {
            throw new ArgumentException($"TLS 1.3 records of cipher suite 0x{cipherSuite.Code:x4} cannot be protected yet.", nameof(cipherSuite));
        }

        Tls13TrafficKeys keys = cipherSuite.KeySchedule.DeriveTrafficKeys(trafficSecret, cipherSuite.KeyLength);
        ITlsAead aead = cipherSuite.Code == Tls13CipherSuite.ChaCha20Poly1305Sha256.Code
            ? new ChaCha20Poly1305TlsAead(keys.Key)
            : new AesGcmTlsAead(keys.Key);
        return new Tls13RecordProtection(aead, keys.Iv);
    }

    /// <summary>
    /// Protects <paramref name="content" /> as records of <paramref name="contentType" />,
    /// each at most <see cref="MaximumPlaintextLength" /> bytes of content, headers
    /// included, ready to send. Empty content is one empty record.
    /// </summary>
    /// <param name="contentType">The content type the records carry inside their protection.</param>
    /// <param name="content">The bytes to send.</param>
    /// <returns>The records, back to back.</returns>
    public byte[] Protect(TlsContentType contentType, ReadOnlySpan<byte> content)
    {
        ArrayBufferWriter<byte> records = new();
        int offset = 0;
        do
        {
            int length = Math.Min(MaximumPlaintextLength, content.Length - offset);
            WriteRecord(records, contentType, content.Slice(offset, length));
            offset += length;
        }
        while (offset < content.Length);

        return records.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Checks and removes the protection from one whole record, header included. A fragment
    /// over <see cref="MaximumCiphertextLength" /> or content over
    /// <see cref="MaximumPlaintextLength" /> is <c>record_overflow</c>, a failed tag is
    /// <c>bad_record_mac</c>, and a plaintext of nothing but padding is
    /// <c>unexpected_message</c>.
    /// </summary>
    /// <param name="record">The record: its five-byte header, then its fragment.</param>
    /// <returns>The real content type and content, or the alert to send.</returns>
    public TlsDecodeResult<Tls13RecordContent> Unprotect(ReadOnlySpan<byte> record)
    {
        ReadOnlySpan<byte> fragment = record[RecordHeaderLength..];
        if (fragment.Length > MaximumCiphertextLength)
        {
            return TlsDecodeResult<Tls13RecordContent>.Failure(TlsAlertDescription.RecordOverflow);
        }

        byte[]? plaintext = Open(record[..RecordHeaderLength], fragment);
        SequenceNumber++;
        return plaintext is null
            ? TlsDecodeResult<Tls13RecordContent>.Failure(TlsAlertDescription.BadRecordMac)
            : ReadInnerPlaintext(plaintext);
    }

    /// <summary>Zeroes and releases the key.</summary>
    public void Dispose() => aead.Dispose();

    private static TlsDecodeResult<Tls13RecordContent> ReadInnerPlaintext(byte[] plaintext)
    {
        int typeIndex = Array.FindLastIndex(plaintext, octet => octet != 0);
        if (typeIndex < 0)
        {
            return TlsDecodeResult<Tls13RecordContent>.Failure(TlsAlertDescription.UnexpectedMessage);
        }

        return typeIndex > MaximumPlaintextLength
            ? TlsDecodeResult<Tls13RecordContent>.Failure(TlsAlertDescription.RecordOverflow)
            : TlsDecodeResult<Tls13RecordContent>.Success(new Tls13RecordContent((TlsContentType)plaintext[typeIndex], plaintext[..typeIndex]));
    }

    private byte[]? Open(ReadOnlySpan<byte> header, ReadOnlySpan<byte> fragment)
    {
        if (fragment.Length < TagLength)
        {
            return null;
        }

        Span<byte> nonce = stackalloc byte[12];
        WriteNonce(nonce);
        byte[] plaintext = new byte[fragment.Length - TagLength];
        return aead.TryDecrypt(nonce, fragment[..^TagLength], fragment[^TagLength..], plaintext, header) ? plaintext : null;
    }

    private void WriteRecord(ArrayBufferWriter<byte> records, TlsContentType contentType, ReadOnlySpan<byte> content)
    {
        int innerLength = content.Length + 1;
        Span<byte> record = records.GetSpan(RecordHeaderLength + innerLength + TagLength)[..(RecordHeaderLength + innerLength + TagLength)];
        record[0] = (byte)TlsContentType.ApplicationData;
        BinaryPrimitives.WriteUInt16BigEndian(record[1..], LegacyRecordVersion);
        BinaryPrimitives.WriteUInt16BigEndian(record[3..], (ushort)(innerLength + TagLength));
        byte[] inner = [.. content, (byte)contentType];
        Span<byte> nonce = stackalloc byte[12];
        WriteNonce(nonce);
        aead.Encrypt(nonce, inner, record.Slice(RecordHeaderLength, innerLength), record[(RecordHeaderLength + innerLength)..], record[..RecordHeaderLength]);
        records.Advance(record.Length);
        SequenceNumber++;
    }

    /// <summary>Writes the per-record nonce: the sequence number, big-endian and left-padded to the IV's length, XORed with the IV.</summary>
    private void WriteNonce(Span<byte> nonce)
    {
        nonce.Clear();
        BinaryPrimitives.WriteUInt64BigEndian(nonce[(nonce.Length - sizeof(ulong))..], SequenceNumber);
        for (int index = 0; index < nonce.Length; index++)
        {
            nonce[index] ^= iv[index];
        }
    }
}
