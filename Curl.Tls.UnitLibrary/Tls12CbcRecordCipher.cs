using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// CBC record protection. MAC-then-encrypt (RFC 5246 section 6.2.3.2) encrypts the
/// content, its MAC and the padding; encrypt-then-MAC (RFC 7366) encrypts the content
/// and the padding and appends a MAC over the IV and ciphertext. TLS 1.1 and 1.2 open
/// each record with a fresh random IV; TLS 1.0 (an IV from the key block) chains the
/// last ciphertext block of one record into the next.
/// </summary>
/// <remarks>
/// Opening a MAC-then-encrypt record checks the padding with <see cref="Tls12CbcPadding.Check" />
/// and always computes and compares the MAC, and only the combined answer is branched on,
/// so bad padding and a bad MAC both end in <c>bad_record_mac</c> by the same path. The
/// MAC is computed over the content the padding implies, so its hashing time still follows
/// the padding length by up to a few hash blocks (the Lucky Thirteen residual, ADR-0148).
/// </remarks>
internal sealed class Tls12CbcRecordCipher(
    ITls12CbcBlockCipher blockCipher,
    Tls12RecordMac mac,
    byte[] keyBlockIv,
    bool encryptThenMac,
    ITlsRandomSource? randomSource) : Tls12RecordCipher
{
    // TLS 1.0's IV, replaced by each record's last ciphertext block; empty when records carry explicit IVs.
    private readonly byte[] chainedIv = keyBlockIv.ToArray();

    private bool HasExplicitIv => chainedIv.Length == 0;

    private int ExplicitIvLength => HasExplicitIv ? blockCipher.BlockSize : 0;

    public override byte[] Seal(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> content)
    {
        byte[] toEncrypt = Tls12CbcPadding.AppendPadding(
            encryptThenMac ? content : AppendMac(sequenceNumber, contentType, version, content),
            blockCipher.BlockSize);
        int ivLength = ExplicitIvLength;
        int encryptedLength = ivLength + toEncrypt.Length;
        byte[] fragment = new byte[encryptedLength + (encryptThenMac ? mac.Length : 0)];
        Span<byte> ciphertext = fragment.AsSpan(ivLength, toEncrypt.Length);
        blockCipher.Encrypt(NextIv(fragment.AsSpan(0, ivLength)), toEncrypt, ciphertext);
        Chain(ciphertext);
        if (encryptThenMac)
        {
            mac.Compute(sequenceNumber, contentType, version, fragment.AsSpan(0, encryptedLength), fragment.AsSpan(encryptedLength));
        }

        return fragment;
    }

    public override TlsDecodeResult<byte[]> Open(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment) =>
        encryptThenMac
            ? OpenEncryptThenMac(sequenceNumber, contentType, version, fragment)
            : OpenMacThenEncrypt(sequenceNumber, contentType, version, fragment);

    public override void Dispose()
    {
        blockCipher.Dispose();
        mac.Dispose();
        CryptographicOperations.ZeroMemory(chainedIv);
    }

    private static TlsDecodeResult<byte[]> Finish(byte[] plaintext, int contentLength, bool good)
    {
        if (!good)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        return TlsDecodeResult<byte[]>.Success(plaintext[..contentLength]);
    }

    private byte[] AppendMac(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> content)
    {
        byte[] withMac = new byte[content.Length + mac.Length];
        content.CopyTo(withMac);
        mac.Compute(sequenceNumber, contentType, version, content, withMac.AsSpan(content.Length));
        return withMac;
    }

    private TlsDecodeResult<byte[]> OpenMacThenEncrypt(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment)
    {
        int blockSize = blockCipher.BlockSize;
        int shortestCiphertext = (mac.Length + blockSize) / blockSize * blockSize;
        if (fragment.Length - ExplicitIvLength < shortestCiphertext || fragment.Length % blockSize != 0)
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        byte[] plaintext = Decrypt(fragment);
        uint paddingGood = Tls12CbcPadding.Check(plaintext, mac.Length, out int unpaddedLength);
        int contentLength = unpaddedLength - mac.Length;
        bool macGood = mac.Verify(sequenceNumber, contentType, version, plaintext.AsSpan(0, contentLength), plaintext.AsSpan(contentLength, mac.Length));
        return Finish(plaintext, contentLength, macGood & (paddingGood == uint.MaxValue));
    }

    private TlsDecodeResult<byte[]> OpenEncryptThenMac(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment)
    {
        int blockSize = blockCipher.BlockSize;
        int ciphertextLength = fragment.Length - mac.Length - ExplicitIvLength;
        if (ciphertextLength < blockSize || ciphertextLength % blockSize != 0)
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        ReadOnlySpan<byte> encrypted = fragment[..^mac.Length];
        if (!mac.Verify(sequenceNumber, contentType, version, encrypted, fragment[^mac.Length..]))
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        byte[] plaintext = Decrypt(encrypted);
        uint paddingGood = Tls12CbcPadding.Check(plaintext, 0, out int contentLength);
        return Finish(plaintext, contentLength, paddingGood == uint.MaxValue);
    }

    /// <summary>Fills <paramref name="explicitIv" /> with random bytes and returns it, or returns TLS 1.0's chained IV.</summary>
    private ReadOnlySpan<byte> NextIv(Span<byte> explicitIv)
    {
        if (!HasExplicitIv)
        {
            return chainedIv;
        }

        (randomSource ?? throw new InvalidOperationException("This record protection only opens records; it has no source of explicit IVs.")).Fill(explicitIv);
        return explicitIv;
    }

    /// <summary>Decrypts the IV (when explicit) and ciphertext of <paramref name="encrypted" />, and chains TLS 1.0's IV.</summary>
    private byte[] Decrypt(ReadOnlySpan<byte> encrypted)
    {
        int ivLength = ExplicitIvLength;
        ReadOnlySpan<byte> iv = HasExplicitIv ? encrypted[..ivLength] : chainedIv;
        ReadOnlySpan<byte> ciphertext = encrypted[ivLength..];
        byte[] plaintext = new byte[ciphertext.Length];
        blockCipher.Decrypt(iv, ciphertext, plaintext);
        Chain(ciphertext);
        return plaintext;
    }

    /// <summary>Keeps the last ciphertext block as TLS 1.0's next IV; does nothing when IVs are explicit.</summary>
    private void Chain(ReadOnlySpan<byte> ciphertext) => ciphertext[^chainedIv.Length..].CopyTo(chainedIv);
}
