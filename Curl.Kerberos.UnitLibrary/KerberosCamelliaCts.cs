using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// Camellia in CBC mode with ciphertext stealing from the initial, all-zero cipher state:
/// the <c>E</c> and <c>D</c> of RFC 6803, for inputs of at least one block (the confounder
/// guarantees it). The stealing is RFC 3962 section 5's, as <see cref="AesCbcCts" /> does
/// for AES: a one-block message is plain CBC; a longer one is CBC over the message
/// zero-padded to whole blocks, the last two ciphertext blocks swapped and the result cut
/// to the message's length.
/// </summary>
internal static class KerberosCamelliaCts
{
    private const int BlockSize = Camellia.BlockSize;

    private static readonly byte[] InitialCipherState = new byte[BlockSize];

    /// <summary>Encrypts <paramref name="source" /> under <paramref name="key" /> into <paramref name="destination" />, as long as it.</summary>
    public static void Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using Camellia cipher = new(key);
        int lastLength = LastBlockLength(source.Length);
        int headLength = source.Length - lastLength - BlockSize;
        if (headLength < 0)
        {
            cipher.EncryptCbc(InitialCipherState, source, destination);
            return;
        }

        Span<byte> tail = stackalloc byte[4 * BlockSize];
        Span<byte> plaintext = tail[..(2 * BlockSize)];
        Span<byte> ciphertext = tail[(2 * BlockSize)..];
        try
        {
            cipher.EncryptCbc(InitialCipherState, source[..headLength], destination[..headLength]);
            ReadOnlySpan<byte> chain = headLength == 0 ? InitialCipherState : destination[(headLength - BlockSize)..headLength];
            plaintext.Clear();
            source[headLength..].CopyTo(plaintext);
            cipher.EncryptCbc(chain, plaintext, ciphertext);
            ciphertext[BlockSize..].CopyTo(destination[headLength..]);
            ciphertext[..lastLength].CopyTo(destination[(headLength + BlockSize)..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tail);
        }
    }

    /// <summary>Decrypts <paramref name="source" /> under <paramref name="key" /> into <paramref name="destination" />, as long as it.</summary>
    public static void Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using Camellia cipher = new(key);
        int lastLength = LastBlockLength(source.Length);
        int headLength = source.Length - lastLength - BlockSize;
        if (headLength < 0)
        {
            cipher.DecryptCbc(InitialCipherState, source, destination);
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
            (headLength == 0 ? InitialCipherState : source[(headLength - BlockSize)..headLength]).CopyTo(chain);

            // The block at headLength is the last CBC block; its decryption is the padded
            // last plaintext exclusive-ored with the next-to-last CBC block, whose missing
            // tail it therefore carries (RFC 3962 section 5).
            cipher.DecryptBlock(source.Slice(headLength, BlockSize), stolen);
            stolen.CopyTo(nextToLastCiphertext);
            source[(headLength + BlockSize)..].CopyTo(nextToLastCiphertext);
            for (int index = 0; index < BlockSize; index++)
            {
                lastPlaintext[index] = (byte)(stolen[index] ^ nextToLastCiphertext[index]);
            }

            cipher.DecryptCbc(chain, nextToLastCiphertext, nextToLastPlaintext);
            cipher.DecryptCbc(InitialCipherState, source[..headLength], destination[..headLength]);
            nextToLastPlaintext.CopyTo(destination[headLength..]);
            lastPlaintext[..lastLength].CopyTo(destination[(headLength + BlockSize)..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tail);
        }
    }

    /// <summary>The length of the last, possibly partial, block of a message of <paramref name="length" /> bytes: 1 to <see cref="BlockSize" />.</summary>
    private static int LastBlockLength(int length) => ((length - 1) % BlockSize) + 1;
}
