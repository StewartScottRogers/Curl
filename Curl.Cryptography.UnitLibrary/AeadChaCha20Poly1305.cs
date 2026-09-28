using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// AEAD_CHACHA20_POLY1305 (RFC 8439 section 2.8): ChaCha20 from block 1 encrypts, the
/// first 32 bytes of block 0 are the Poly1305 key, and the tag covers the associated
/// data, the ciphertext and both lengths. The parameter order follows the BCL's
/// <c>ChaCha20Poly1305</c>, which this type replaces on every platform (ADR-0118).
/// </summary>
/// <remarks>
/// Constant-time: built on <see cref="ChaCha20" /> and <see cref="Poly1305" />, and the
/// received tag is compared with <see cref="CryptographicOperations.FixedTimeEquals" />.
/// The key is copied in the constructor and zeroed by <see cref="Dispose" />.
/// </remarks>
public sealed class AeadChaCha20Poly1305 : IDisposable
{
    /// <summary>The length in bytes of a key.</summary>
    public const int KeySize = ChaCha20.KeySize;

    /// <summary>The length in bytes of a nonce.</summary>
    public const int NonceSize = ChaCha20.NonceSize;

    /// <summary>The length in bytes of a tag.</summary>
    public const int TagSize = Poly1305.TagSize;

    private readonly byte[] key = new byte[KeySize];

    private bool disposed;

    /// <summary>Copies <paramref name="key" /> for the instance's lifetime.</summary>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    public AeadChaCha20Poly1305(ReadOnlySpan<byte> key)
    {
        RequireLength(key.Length, KeySize, nameof(key));
        key.CopyTo(this.key);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext" /> into <paramref name="ciphertext" /> and
    /// writes the tag over <paramref name="associatedData" /> and the ciphertext into
    /// <paramref name="tag" />.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag,
        ReadOnlySpan<byte> associatedData = default)
    {
        RequireUsable(nonce, plaintext.Length, ciphertext.Length, tag.Length);
        ChaCha20.ApplyKeyStream(key, nonce, 1, plaintext, ciphertext);
        ComputeTag(nonce, associatedData, ciphertext, tag);
    }

    /// <summary>
    /// Checks <paramref name="tag" /> over <paramref name="associatedData" /> and
    /// <paramref name="ciphertext" /> in fixed time and, only when it matches, decrypts
    /// into <paramref name="plaintext" />.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="plaintext" /> all zero and no byte of plaintext
    /// written, when the tag does not match (ADR-0118's typed failure); otherwise
    /// <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public bool TryDecrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext,
        ReadOnlySpan<byte> associatedData = default)
    {
        RequireUsable(nonce, ciphertext.Length, plaintext.Length, tag.Length);
        Span<byte> expectedTag = stackalloc byte[TagSize];
        try
        {
            ComputeTag(nonce, associatedData, ciphertext, expectedTag);
            if (!CryptographicOperations.FixedTimeEquals(expectedTag, tag))
            {
                CryptographicOperations.ZeroMemory(plaintext);
                return false;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedTag);
        }

        ChaCha20.ApplyKeyStream(key, nonce, 1, ciphertext, plaintext);
        return true;
    }

    /// <summary>Zeroes the key; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
        disposed = true;
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"AEAD_CHACHA20_POLY1305 needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private void RequireUsable(ReadOnlySpan<byte> nonce, int sourceLength, int destinationLength, int tagLength)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(nonce.Length, NonceSize, nameof(nonce));
        RequireLength(destinationLength, sourceLength, "destination");
        RequireLength(tagLength, TagSize, "tag");
    }

    /// <summary>
    /// The tag of RFC 8439 section 2.8: Poly1305, keyed by block 0, over the associated
    /// data and the ciphertext, each zero-padded to 16 bytes, then both lengths as 64-bit
    /// little-endian numbers.
    /// </summary>
    private void ComputeTag(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        Span<byte> block = stackalloc byte[ChaCha20.BlockSize];
        Span<uint> state = stackalloc uint[Poly1305.StateLength];
        try
        {
            ChaCha20.ComputeBlock(key, nonce, 0, block);
            Poly1305.Initialize(state, block[..Poly1305.KeySize]);
            Poly1305.Absorb(state, associatedData, zeroPadLastBlock: true);
            Poly1305.Absorb(state, ciphertext, zeroPadLastBlock: true);
            Span<byte> lengths = block[..16];
            BinaryPrimitives.WriteUInt64LittleEndian(lengths, (ulong)associatedData.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(lengths[8..], (ulong)ciphertext.Length);
            Poly1305.Absorb(state, lengths, zeroPadLastBlock: true);
            Poly1305.Finish(state, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(block);
            Poly1305.Clear(state);
        }
    }
}
