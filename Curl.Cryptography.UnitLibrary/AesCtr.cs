using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// AES in counter mode (NIST SP 800-38A section 6.5) with a 128-bit big-endian counter
/// block that wraps from all ones to zero, as SSH's <c>aes128-ctr</c>, <c>aes192-ctr</c>
/// and <c>aes256-ctr</c> use it (RFC 4344 section 4) and as encrypted
/// <c>openssh-key-v1</c> keys use <c>aes256-ctr</c>. The block cipher is the BCL's
/// <see cref="Aes" />; this type adds the counter.
/// </summary>
/// <remarks>
/// A keyed stream: the counter and the unused part of the last keystream block carry over
/// from one <see cref="ApplyKeyStream" /> call to the next, so a message split across
/// calls gives the same bytes as one call, and an SSH session encrypts packet after packet
/// on one instance. Constant-time: the counter increment carries through all sixteen bytes
/// with no early exit, and the block cipher is the BCL's. The key, counter and keystream
/// are zeroed by <see cref="Dispose" />.
/// </remarks>
public sealed class AesCtr : IDisposable
{
    /// <summary>The length in bytes of a block and of the counter block.</summary>
    public const int BlockSize = 16;

    private const int KeyStreamBlocks = 16;

    private readonly Aes aes;

    private readonly byte[] counter = new byte[BlockSize];

    private readonly byte[] counterBlocks = new byte[KeyStreamBlocks * BlockSize];

    private readonly byte[] keyStream = new byte[KeyStreamBlocks * BlockSize];

    private int keyStreamOffset = KeyStreamBlocks * BlockSize;

    private bool disposed;

    /// <summary>
    /// Keys AES with <paramref name="key" /> and starts the counter at
    /// <paramref name="initialCounter" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="key" /> is not 16, 24 or 32 bytes, or
    /// <paramref name="initialCounter" /> is not <see cref="BlockSize" /> bytes.
    /// </exception>
    public AesCtr(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initialCounter)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException($"AES needs a key of 16, 24 or 32 bytes; this is {key.Length}.", nameof(key));
        }

        if (initialCounter.Length != BlockSize)
        {
            throw new ArgumentException($"AES-CTR needs a {BlockSize}-byte initial counter block; this is {initialCounter.Length}.", nameof(initialCounter));
        }

        aes = Aes.Create();
        aes.SetKey(key);
        initialCounter.CopyTo(counter);
    }

    /// <summary>
    /// Exclusive-ors <paramref name="source" /> with the next bytes of the keystream and
    /// writes the result to <paramref name="destination" />: encryption and decryption
    /// alike. <paramref name="destination" /> may be <paramref name="source" /> itself.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not as long as <paramref name="source" />.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void ApplyKeyStream(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (destination.Length != source.Length)
        {
            throw new ArgumentException($"AES-CTR needs a destination as long as the source, {source.Length} bytes; this is {destination.Length}.", nameof(destination));
        }

        for (int offset = 0; offset < source.Length;)
        {
            if (keyStreamOffset == keyStream.Length)
            {
                RefillKeyStream();
            }

            int count = Math.Min(keyStream.Length - keyStreamOffset, source.Length - offset);
            for (int index = 0; index < count; index++)
            {
                destination[offset + index] = (byte)(source[offset + index] ^ keyStream[keyStreamOffset + index]);
            }

            keyStreamOffset += count;
            offset += count;
        }
    }

    /// <summary>Zeroes the key, counter and keystream; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        aes.Dispose();
        CryptographicOperations.ZeroMemory(counter);
        CryptographicOperations.ZeroMemory(counterBlocks);
        CryptographicOperations.ZeroMemory(keyStream);
        disposed = true;
    }

    /// <summary>Adds one to <paramref name="counterBlock" />, a 128-bit big-endian integer, wrapping to zero.</summary>
    internal static void Increment(Span<byte> counterBlock)
    {
        int carry = 1;
        for (int index = counterBlock.Length - 1; index >= 0; index--)
        {
            int sum = counterBlock[index] + carry;
            counterBlock[index] = (byte)sum;
            carry = sum >> 8;
        }
    }

    /// <summary>Encrypts the next <see cref="KeyStreamBlocks" /> counter blocks into the keystream buffer.</summary>
    private void RefillKeyStream()
    {
        for (int offset = 0; offset < counterBlocks.Length; offset += BlockSize)
        {
            counter.CopyTo(counterBlocks, offset);
            Increment(counter);
        }

        aes.EncryptEcb(counterBlocks, keyStream, PaddingMode.None);
        keyStreamOffset = 0;
    }
}
