using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The Blowfish block cipher (Schneier 1993): 64-bit blocks, 16 rounds, a key of 1 to 56
/// bytes. <see cref="EncryptBlock" /> and <see cref="DecryptBlock" /> are single blocks
/// (ECB); <see cref="EncryptCbc" /> and <see cref="DecryptCbc" /> are the cipher block
/// chaining mode SSH's <c>blowfish-cbc</c> uses (RFC 4253 section 6.3). Blocks are read
/// and written big-endian, as Schneier's test vectors and OpenSSH do.
/// </summary>
/// <remarks>
/// Not constant-time: Blowfish indexes its key-dependent S-boxes with data bytes by
/// design (ADR-0118); it exists because curl's SSH backends offer it. The key schedule is
/// copied into the instance and zeroed by <see cref="Dispose" />.
/// </remarks>
public sealed class Blowfish : IDisposable
{
    /// <summary>The length in bytes of a block.</summary>
    public const int BlockSize = 8;

    /// <summary>The shortest key in bytes.</summary>
    public const int MinimumKeySize = 1;

    /// <summary>The longest key in bytes: 448 bits, as Schneier specifies.</summary>
    public const int MaximumKeySize = 56;

    private readonly BlowfishState state = new();

    private bool disposed;

    /// <summary>Runs Blowfish's key schedule on <paramref name="key" />.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="key" /> is shorter than <see cref="MinimumKeySize" /> or longer than
    /// <see cref="MaximumKeySize" /> bytes.
    /// </exception>
    public Blowfish(ReadOnlySpan<byte> key)
    {
        if (key.Length < MinimumKeySize || key.Length > MaximumKeySize)
        {
            throw new ArgumentException($"Blowfish needs a key of {MinimumKeySize} to {MaximumKeySize} bytes; this is {key.Length}.", nameof(key));
        }

        state.Initialize();
        state.ExpandKey(key);
    }

    /// <summary>Encrypts the one block <paramref name="source" /> into <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException">A span is not <see cref="BlockSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void EncryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(source.Length, destination.Length, BlockSize);
        TransformBlock(source, destination, encrypt: true);
    }

    /// <summary>Decrypts the one block <paramref name="source" /> into <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException">A span is not <see cref="BlockSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void DecryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(source.Length, destination.Length, BlockSize);
        TransformBlock(source, destination, encrypt: false);
    }

    /// <summary>
    /// Encrypts <paramref name="source" />, a whole number of blocks, in cipher block
    /// chaining mode from <paramref name="initializationVector" /> into
    /// <paramref name="destination" />, which may be <paramref name="source" /> itself. To
    /// continue the chain, pass the last ciphertext block as the next vector.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="initializationVector" /> is not <see cref="BlockSize" /> bytes,
    /// <paramref name="source" /> is not a whole number of blocks, or
    /// <paramref name="destination" /> is not as long as it.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void EncryptCbc(ReadOnlySpan<byte> initializationVector, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsableChain(initializationVector.Length, source.Length, destination.Length);
        Span<byte> chain = stackalloc byte[BlockSize];
        try
        {
            initializationVector.CopyTo(chain);
            for (int offset = 0; offset < source.Length; offset += BlockSize)
            {
                for (int index = 0; index < BlockSize; index++)
                {
                    chain[index] ^= source[offset + index];
                }

                TransformBlock(chain, chain, encrypt: true);
                chain.CopyTo(destination[offset..]);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chain);
        }
    }

    /// <summary>
    /// Decrypts <paramref name="source" />, a whole number of blocks, in cipher block
    /// chaining mode from <paramref name="initializationVector" /> into
    /// <paramref name="destination" />, which may be <paramref name="source" /> itself. To
    /// continue the chain, pass the last ciphertext block as the next vector.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="initializationVector" /> is not <see cref="BlockSize" /> bytes,
    /// <paramref name="source" /> is not a whole number of blocks, or
    /// <paramref name="destination" /> is not as long as it.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void DecryptCbc(ReadOnlySpan<byte> initializationVector, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsableChain(initializationVector.Length, source.Length, destination.Length);
        Span<byte> buffers = stackalloc byte[3 * BlockSize];
        Span<byte> chain = buffers[..BlockSize];
        Span<byte> ciphertext = buffers[BlockSize..(2 * BlockSize)];
        Span<byte> plaintext = buffers[(2 * BlockSize)..];
        try
        {
            initializationVector.CopyTo(chain);
            for (int offset = 0; offset < source.Length; offset += BlockSize)
            {
                source.Slice(offset, BlockSize).CopyTo(ciphertext);
                TransformBlock(ciphertext, plaintext, encrypt: false);
                for (int index = 0; index < BlockSize; index++)
                {
                    destination[offset + index] = (byte)(plaintext[index] ^ chain[index]);
                }

                ciphertext.CopyTo(chain);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffers);
        }
    }

    /// <summary>Zeroes the key schedule; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        state.Clear();
        disposed = true;
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"Blowfish needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private void RequireUsable(int sourceLength, int destinationLength, int expectedSourceLength)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(sourceLength, expectedSourceLength, "source");
        RequireLength(destinationLength, sourceLength, "destination");
    }

    private void RequireUsableChain(int initializationVectorLength, int sourceLength, int destinationLength)
    {
        RequireUsable(sourceLength, destinationLength, sourceLength - (sourceLength % BlockSize));
        RequireLength(initializationVectorLength, BlockSize, "initializationVector");
    }

    private void TransformBlock(ReadOnlySpan<byte> source, Span<byte> destination, bool encrypt)
    {
        uint left = BinaryPrimitives.ReadUInt32BigEndian(source);
        uint right = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
        if (encrypt)
        {
            state.Encrypt(ref left, ref right);
        }
        else
        {
            state.Decrypt(ref left, ref right);
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, left);
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..], right);
    }
}
