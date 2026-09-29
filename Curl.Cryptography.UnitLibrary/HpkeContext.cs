using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// An HPKE encryption context (RFC 9180 section 5.2): the AEAD key, the base nonce, the
/// exporter secret and the sequence number one side of a base-mode setup derives.
/// <see cref="Hpke" /> creates it; the sender calls <see cref="Seal" />, the recipient
/// <see cref="TryOpen" />, and either <see cref="Export" /> (section 5.3).
/// </summary>
/// <remarks>
/// Message i uses the nonce <c>base_nonce XOR I2OSP(i, 12)</c>. The sequence number is a
/// 64-bit counter: an overflow throws <see cref="OverflowException" /> before any nonce is
/// reused, far below RFC 9180's 2^96 - 1 limit. <see cref="Dispose" /> zeroes every secret.
/// </remarks>
public sealed class HpkeContext : IDisposable
{
    /// <summary>Nt, the length in bytes of the tag every supported AEAD appends.</summary>
    public const int TagSize = 16;

    /// <summary>Nn, the length in bytes of a nonce for every supported AEAD.</summary>
    internal const int NonceSize = 12;

    private readonly HpkeAead aead;

    private readonly byte[] suiteId;

    private readonly byte[] key;

    private readonly byte[] baseNonce = new byte[NonceSize];

    private readonly byte[] exporterSecret = new byte[HpkeLabeledHkdf.HashSize];

    private bool disposed;

    /// <summary>
    /// RFC 9180 section 5.1's <c>KeySchedule</c> in base mode (no PSK): derives the
    /// context's secrets from <paramref name="sharedSecret" /> and <paramref name="info" />
    /// under the suite identifier <c>"HPKE" || kem_id || kdf_id || aead_id</c>.
    /// </summary>
    internal HpkeContext(HpkeKem kem, HpkeKdf kdf, HpkeAead aead, ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> info)
    {
        this.aead = aead;
        suiteId = new byte[10];
        "HPKE"u8.CopyTo(suiteId);
        BinaryPrimitives.WriteUInt16BigEndian(suiteId.AsSpan(4), (ushort)kem);
        BinaryPrimitives.WriteUInt16BigEndian(suiteId.AsSpan(6), (ushort)kdf);
        BinaryPrimitives.WriteUInt16BigEndian(suiteId.AsSpan(8), (ushort)aead);
        key = new byte[aead == HpkeAead.Aes128Gcm ? 16 : 32];
        Span<byte> keyScheduleContext = stackalloc byte[1 + (2 * HpkeLabeledHkdf.HashSize)];
        Span<byte> secret = stackalloc byte[HpkeLabeledHkdf.HashSize];
        try
        {
            keyScheduleContext[0] = 0x00;
            HpkeLabeledHkdf.Extract(suiteId, default, "psk_id_hash"u8, default, keyScheduleContext.Slice(1, HpkeLabeledHkdf.HashSize));
            HpkeLabeledHkdf.Extract(suiteId, default, "info_hash"u8, info, keyScheduleContext[(1 + HpkeLabeledHkdf.HashSize)..]);
            HpkeLabeledHkdf.Extract(suiteId, sharedSecret, "secret"u8, default, secret);
            HpkeLabeledHkdf.Expand(suiteId, secret, "key"u8, keyScheduleContext, key);
            HpkeLabeledHkdf.Expand(suiteId, secret, "base_nonce"u8, keyScheduleContext, baseNonce);
            HpkeLabeledHkdf.Expand(suiteId, secret, "exp"u8, keyScheduleContext, exporterSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>The number of messages sealed or opened so far, the next message's sequence number.</summary>
    public ulong SequenceNumber { get; private set; }

    /// <summary>The AEAD key, for tests that pin the key schedule.</summary>
    internal ReadOnlySpan<byte> Key => key;

    /// <summary>The base nonce, for tests that pin the key schedule.</summary>
    internal ReadOnlySpan<byte> BaseNonce => baseNonce;

    /// <summary>The exporter secret, for tests that pin the key schedule.</summary>
    internal ReadOnlySpan<byte> ExporterSecret => exporterSecret;

    /// <summary>
    /// <c>ContextS.Seal(aad, pt)</c>: encrypts <paramref name="plaintext" /> with the next
    /// nonce and writes the ciphertext, then the tag, into <paramref name="ciphertext" />.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="ciphertext" /> is not <see cref="TagSize" /> bytes longer than <paramref name="plaintext" />.</exception>
    /// <exception cref="ObjectDisposedException">The context has been disposed.</exception>
    public void Seal(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(ciphertext.Length, plaintext.Length + TagSize, nameof(ciphertext));
        ulong nextSequenceNumber = checked(SequenceNumber + 1);
        Span<byte> nonce = stackalloc byte[NonceSize];
        ComputeNonce(nonce);
        Span<byte> sealedText = ciphertext[..plaintext.Length];
        Span<byte> tag = ciphertext[plaintext.Length..];
        if (aead == HpkeAead.ChaCha20Poly1305)
        {
            using AeadChaCha20Poly1305 chaCha20Poly1305 = new(key);
            chaCha20Poly1305.Encrypt(nonce, plaintext, sealedText, tag, associatedData);
        }
        else
        {
            using AesGcm aesGcm = new(key, TagSize);
            aesGcm.Encrypt(nonce, plaintext, sealedText, tag, associatedData);
        }

        SequenceNumber = nextSequenceNumber;
    }

    /// <summary>
    /// <c>ContextR.Open(aad, ct)</c>: checks the tag at the end of
    /// <paramref name="ciphertext" /> and, only when it matches, decrypts into
    /// <paramref name="plaintext" /> and advances the sequence number.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="plaintext" /> all zero and the sequence number
    /// unchanged, when <paramref name="ciphertext" /> is shorter than a tag or the tag does
    /// not match (ADR-0118's typed failure); otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="plaintext" /> is not <see cref="TagSize" /> bytes shorter than a <paramref name="ciphertext" /> that holds a tag.</exception>
    /// <exception cref="ObjectDisposedException">The context has been disposed.</exception>
    public bool TryOpen(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ciphertext.Length < TagSize)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return false;
        }

        RequireLength(plaintext.Length, ciphertext.Length - TagSize, nameof(plaintext));
        ulong nextSequenceNumber = checked(SequenceNumber + 1);
        Span<byte> nonce = stackalloc byte[NonceSize];
        ComputeNonce(nonce);
        if (!TryDecrypt(nonce, associatedData, ciphertext[..plaintext.Length], ciphertext[plaintext.Length..], plaintext))
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return false;
        }

        SequenceNumber = nextSequenceNumber;
        return true;
    }

    /// <summary>
    /// <c>Context.Export(exporter_context, L)</c>: fills <paramref name="exportedValue" />,
    /// L bytes long, with a secret bound to <paramref name="exporterContext" />.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="exportedValue" /> is empty or longer than 8160 bytes.</exception>
    /// <exception cref="ObjectDisposedException">The context has been disposed.</exception>
    public void Export(ReadOnlySpan<byte> exporterContext, Span<byte> exportedValue)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        HpkeLabeledHkdf.Expand(suiteId, exporterSecret, "sec"u8, exporterContext, exportedValue);
    }

    /// <summary>Zeroes the key, base nonce and exporter secret; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(baseNonce);
        CryptographicOperations.ZeroMemory(exporterSecret);
        disposed = true;
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"HPKE needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private void ComputeNonce(Span<byte> nonce)
    {
        baseNonce.CopyTo(nonce);
        Span<byte> counterBytes = nonce[(NonceSize - sizeof(ulong))..];
        BinaryPrimitives.WriteUInt64BigEndian(counterBytes, BinaryPrimitives.ReadUInt64BigEndian(counterBytes) ^ SequenceNumber);
    }

    private bool TryDecrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> sealedText,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext)
    {
        if (aead == HpkeAead.ChaCha20Poly1305)
        {
            using AeadChaCha20Poly1305 chaCha20Poly1305 = new(key);
            return chaCha20Poly1305.TryDecrypt(nonce, sealedText, tag, plaintext, associatedData);
        }

        using AesGcm aesGcm = new(key, TagSize);
        try
        {
            aesGcm.Decrypt(nonce, sealedText, tag, plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }
}
