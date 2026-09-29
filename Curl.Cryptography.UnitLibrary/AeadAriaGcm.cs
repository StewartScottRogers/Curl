namespace Curl.Cryptography;

/// <summary>
/// ARIA in Galois/Counter Mode (NIST SP 800-38D over RFC 5794's <see cref="Aria" />), the
/// AEAD of TLS's <c>*_WITH_ARIA_*_GCM_*</c> suites (RFC 6209): a 12-byte nonce and a
/// 16-byte tag. The parameter order follows the BCL's <c>AesGcm</c>, whose GCM is AES-only
/// (ADR-0118, ADR-0140).
/// </summary>
/// <remarks>
/// GHASH is constant-time and the received tag is compared in fixed time, but
/// <see cref="Aria" /> indexes its S-boxes and is not constant-time (ADR-0146). The key
/// schedule and the hash subkey are zeroed by <see cref="Dispose" />.
/// </remarks>
public sealed class AeadAriaGcm : IDisposable
{
    /// <summary>The length in bytes of a nonce.</summary>
    public const int NonceSize = GaloisCounterMode.NonceSize;

    /// <summary>The length in bytes of a tag.</summary>
    public const int TagSize = GaloisCounterMode.TagSize;

    private readonly Aria aria;

    private readonly GaloisCounterMode mode;

    private bool disposed;

    /// <summary>Runs ARIA's key schedule on <paramref name="key" /> and computes GCM's hash subkey.</summary>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not 16, 24 or 32 bytes.</exception>
    public AeadAriaGcm(ReadOnlySpan<byte> key)
    {
        aria = new Aria(key);
        mode = new GaloisCounterMode(aria);
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
        RequireUsable(nonce.Length, plaintext.Length, ciphertext.Length, tag.Length);
        mode.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
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
        RequireUsable(nonce.Length, ciphertext.Length, plaintext.Length, tag.Length);
        return mode.TryDecrypt(nonce, ciphertext, tag, plaintext, associatedData);
    }

    /// <summary>Zeroes the key schedule and the hash subkey; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        mode.Dispose();
        aria.Dispose();
        disposed = true;
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"ARIA-GCM needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private void RequireUsable(int nonceLength, int sourceLength, int destinationLength, int tagLength)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(nonceLength, NonceSize, "nonce");
        RequireLength(destinationLength, sourceLength, "destination");
        RequireLength(tagLength, TagSize, "tag");
    }
}
