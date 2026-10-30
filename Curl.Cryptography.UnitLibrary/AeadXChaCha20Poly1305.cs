using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// AEAD_XCHACHA20_POLY1305 (draft-irtf-cfrg-xchacha-03 section 2.3): <see cref="HChaCha20" />
/// derives a subkey from the key and the first 16 bytes of the 24-byte nonce, and
/// <see cref="AeadChaCha20Poly1305" /> runs under that subkey with four zero bytes and the
/// last 8 nonce bytes as its nonce. WireGuard's cookie replies use it. The parameter order
/// follows <see cref="AeadChaCha20Poly1305" />.
/// </summary>
/// <remarks>
/// Constant-time: built on <see cref="HChaCha20" /> and <see cref="AeadChaCha20Poly1305" />,
/// which compares the tag in fixed time. The key is copied in the constructor and zeroed by
/// <see cref="Dispose" />; each subkey is zeroed before the call returns.
/// </remarks>
public sealed class AeadXChaCha20Poly1305 : IDisposable
{
    /// <summary>The length in bytes of a key.</summary>
    public const int KeySize = ChaCha20.KeySize;

    /// <summary>The length in bytes of a nonce.</summary>
    public const int NonceSize = 24;

    /// <summary>The length in bytes of a tag.</summary>
    public const int TagSize = Poly1305.TagSize;

    private readonly byte[] key = new byte[KeySize];

    private bool disposed;

    /// <summary>Copies <paramref name="key" /> for the instance's lifetime.</summary>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    public AeadXChaCha20Poly1305(ReadOnlySpan<byte> key)
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
        RequireUsable(nonce);
        Span<byte> subkey = stackalloc byte[HChaCha20.SubkeySize];
        Span<byte> innerNonce = stackalloc byte[ChaCha20.NonceSize];
        try
        {
            Derive(nonce, subkey, innerNonce);
            using var aead = new AeadChaCha20Poly1305(subkey);
            aead.Encrypt(innerNonce, plaintext, ciphertext, tag, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
    }

    /// <summary>
    /// Checks <paramref name="tag" /> over <paramref name="associatedData" /> and
    /// <paramref name="ciphertext" /> in fixed time and, only when it matches, decrypts
    /// into <paramref name="plaintext" />.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="plaintext" /> all zero, when the tag does not
    /// match; otherwise <c>true</c>.
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
        RequireUsable(nonce);
        Span<byte> subkey = stackalloc byte[HChaCha20.SubkeySize];
        Span<byte> innerNonce = stackalloc byte[ChaCha20.NonceSize];
        try
        {
            Derive(nonce, subkey, innerNonce);
            using var aead = new AeadChaCha20Poly1305(subkey);
            return aead.TryDecrypt(innerNonce, ciphertext, tag, plaintext, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(subkey);
        }
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
            throw new ArgumentException($"AEAD_XCHACHA20_POLY1305 needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private void RequireUsable(ReadOnlySpan<byte> nonce)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(nonce.Length, NonceSize, nameof(nonce));
    }

    /// <summary>
    /// The subkey from the first 16 nonce bytes, and the inner nonce: four zero bytes, then
    /// the last 8 nonce bytes (draft-irtf-cfrg-xchacha-03 section 2.3).
    /// </summary>
    private void Derive(ReadOnlySpan<byte> nonce, Span<byte> subkey, Span<byte> innerNonce)
    {
        HChaCha20.DeriveSubkey(key, nonce[..HChaCha20.NonceSize], subkey);
        innerNonce[..4].Clear();
        nonce[HChaCha20.NonceSize..].CopyTo(innerNonce[4..]);
    }
}
