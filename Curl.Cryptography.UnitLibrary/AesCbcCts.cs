using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// AES in cipher block chaining mode with ciphertext stealing, as RFC 3962 section 5
/// defines it for Kerberos's <c>aes*-cts-*</c> encryption types: a message of at least
/// one block and any length comes out exactly as long as it went in. A one-block message
/// is plain CBC; a longer one is CBC over the message zero-padded to whole blocks, with
/// the last two ciphertext blocks swapped and the result cut to the message's length,
/// always, even when the message is a whole number of blocks (NIST SP 800-38A addendum's
/// CBC-CS3). The block cipher is the BCL's <see cref="Aes" />.
/// </summary>
/// <remarks>
/// Constant-time: every branch and loop bound depends only on the message length, and the
/// block cipher is the BCL's. The key is zeroed by <see cref="Dispose" />, and the
/// working blocks before each call returns.
/// </remarks>
public sealed class AesCbcCts : IDisposable
{
    /// <summary>The length in bytes of a block, of the initialization vector, and of the shortest message.</summary>
    public const int BlockSize = 16;

    private readonly Aes aes;

    private bool disposed;

    /// <summary>Keys AES with <paramref name="key" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not 16, 24 or 32 bytes.</exception>
    public AesCbcCts(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException($"AES needs a key of 16, 24 or 32 bytes; this is {key.Length}.", nameof(key));
        }

        aes = Aes.Create();
        aes.SetKey(key);
    }

    /// <summary>
    /// Encrypts <paramref name="source" /> from <paramref name="initializationVector" />
    /// into <paramref name="destination" />, which is as long as <paramref name="source" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="initializationVector" /> is not <see cref="BlockSize" /> bytes,
    /// <paramref name="source" /> is shorter than one block, or
    /// <paramref name="destination" /> is not as long as it.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Encrypt(ReadOnlySpan<byte> initializationVector, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(initializationVector.Length, source.Length, destination.Length);
        int lastLength = LastBlockLength(source.Length);
        int headLength = source.Length - lastLength - BlockSize;
        if (headLength < 0)
        {
            aes.EncryptCbc(source, initializationVector, destination, PaddingMode.None);
            return;
        }

        Span<byte> tail = stackalloc byte[4 * BlockSize];
        Span<byte> plaintext = tail[..(2 * BlockSize)];
        Span<byte> ciphertext = tail[(2 * BlockSize)..];
        try
        {
            aes.EncryptCbc(source[..headLength], initializationVector, destination[..headLength], PaddingMode.None);
            ReadOnlySpan<byte> chain = headLength == 0 ? initializationVector : destination[(headLength - BlockSize)..headLength];
            plaintext.Clear();
            source[headLength..].CopyTo(plaintext);
            aes.EncryptCbc(plaintext, chain, ciphertext, PaddingMode.None);
            ciphertext[BlockSize..].CopyTo(destination[headLength..]);
            ciphertext[..lastLength].CopyTo(destination[(headLength + BlockSize)..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tail);
        }
    }

    /// <summary>
    /// Decrypts <paramref name="source" /> from <paramref name="initializationVector" />
    /// into <paramref name="destination" />, which is as long as <paramref name="source" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="initializationVector" /> is not <see cref="BlockSize" /> bytes,
    /// <paramref name="source" /> is shorter than one block, or
    /// <paramref name="destination" /> is not as long as it.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Decrypt(ReadOnlySpan<byte> initializationVector, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(initializationVector.Length, source.Length, destination.Length);
        int lastLength = LastBlockLength(source.Length);
        int headLength = source.Length - lastLength - BlockSize;
        if (headLength < 0)
        {
            aes.DecryptCbc(source, initializationVector, destination, PaddingMode.None);
            return;
        }

        Span<byte> tail = stackalloc byte[5 * BlockSize];
        Span<byte> chain = tail[..BlockSize];
        Span<byte> stolen = tail[BlockSize..(2 * BlockSize)];
        Span<byte> lastPlaintext = tail[(2 * BlockSize)..(3 * BlockSize)];
        Span<byte> nextToLastPlaintext = tail[(3 * BlockSize)..(4 * BlockSize)];
        Span<byte> nextToLastCiphertext = tail[(4 * BlockSize)..];
        try
        {
            (headLength == 0 ? initializationVector : source[(headLength - BlockSize)..headLength]).CopyTo(chain);

            // The block at headLength is the last CBC block; its decryption is the padded
            // last plaintext exclusive-ored with the next-to-last CBC block, whose missing
            // tail it therefore carries (RFC 3962 section 5).
            aes.DecryptEcb(source.Slice(headLength, BlockSize), stolen, PaddingMode.None);
            stolen.CopyTo(nextToLastCiphertext);
            source[(headLength + BlockSize)..].CopyTo(nextToLastCiphertext);
            for (int index = 0; index < BlockSize; index++)
            {
                lastPlaintext[index] = (byte)(stolen[index] ^ nextToLastCiphertext[index]);
            }

            aes.DecryptCbc(nextToLastCiphertext, chain, nextToLastPlaintext, PaddingMode.None);
            aes.DecryptCbc(source[..headLength], initializationVector, destination[..headLength], PaddingMode.None);
            nextToLastPlaintext.CopyTo(destination[headLength..]);
            lastPlaintext[..lastLength].CopyTo(destination[(headLength + BlockSize)..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tail);
        }
    }

    /// <summary>Zeroes the key; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        aes.Dispose();
        disposed = true;
    }

    /// <summary>The length of the last, possibly partial, block of a message of <paramref name="length" /> bytes: 1 to <see cref="BlockSize" />.</summary>
    private static int LastBlockLength(int length) => ((length - 1) % BlockSize) + 1;

    private void RequireUsable(int initializationVectorLength, int sourceLength, int destinationLength)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (initializationVectorLength != BlockSize)
        {
            throw new ArgumentException($"AES-CBC-CTS needs a {BlockSize}-byte initialization vector; this is {initializationVectorLength}.", "initializationVector");
        }

        if (sourceLength < BlockSize)
        {
            throw new ArgumentException($"AES-CBC-CTS needs a message of at least {BlockSize} bytes; this is {sourceLength}.", "source");
        }

        if (destinationLength != sourceLength)
        {
            throw new ArgumentException($"AES-CBC-CTS needs a destination as long as the source, {sourceLength} bytes; this is {destinationLength}.", "destination");
        }
    }
}
