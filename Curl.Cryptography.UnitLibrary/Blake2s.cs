using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// BLAKE2s (RFC 7693): the 32-bit BLAKE2 hash, unkeyed or keyed with up to
/// <see cref="MaxKeySize" /> bytes, with any digest length from 1 to
/// <see cref="HashSize" /> bytes. WireGuard's HASH and MAC are BLAKE2s.
/// </summary>
/// <remarks>
/// Constant-time: the compression function is additions, rotations and exclusive-ors on
/// fixed state words, and the message schedule depends only on the round number, never on
/// the key or the data. The key, the buffered block and the chaining state are zeroed by
/// <see cref="Dispose" />, and every temporary before returning.
/// </remarks>
public sealed class Blake2s : IDisposable
{
    /// <summary>The longest digest, and the default, in bytes.</summary>
    public const int HashSize = 32;

    /// <summary>The longest key in bytes.</summary>
    public const int MaxKeySize = 32;

    /// <summary>The length in bytes of one message block.</summary>
    public const int BlockSize = 64;

    private const int Rounds = 10;

    private static readonly uint[] InitializationVector =
    [
        0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A, 0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19,
    ];

    private readonly uint[] chainingState = new uint[8];

    private readonly byte[] buffer = new byte[BlockSize];

    private readonly byte[] key;

    private readonly int hashSize;

    private int bufferLength;

    private ulong byteCount;

    private bool disposed;

    /// <summary>
    /// Starts a hash of <paramref name="hashSize" /> bytes, keyed by <paramref name="key" />
    /// when it is not empty (RFC 7693 section 3.3); the key is copied for the instance's
    /// lifetime.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="hashSize" /> is not 1 to <see cref="HashSize" />, or
    /// <paramref name="key" /> is longer than <see cref="MaxKeySize" />.
    /// </exception>
    public Blake2s(int hashSize = HashSize, ReadOnlySpan<byte> key = default)
    {
        RequireHashSize(hashSize, nameof(hashSize));
        if (key.Length > MaxKeySize)
        {
            throw new ArgumentException($"A BLAKE2s key is at most {MaxKeySize} bytes; this one is {key.Length}.", nameof(key));
        }

        this.hashSize = hashSize;
        this.key = key.ToArray();
        Reset();
    }

    /// <summary>
    /// Writes the unkeyed BLAKE2s digest of <paramref name="source" /> to
    /// <paramref name="destination" />, whose length is the digest length.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not 1 to <see cref="HashSize" /> bytes.</exception>
    public static void HashData(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(default, source, destination);

    /// <summary>
    /// Writes the BLAKE2s digest of <paramref name="source" /> keyed by
    /// <paramref name="key" /> to <paramref name="destination" />, whose length is the
    /// digest length.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination" /> is not 1 to <see cref="HashSize" /> bytes, or
    /// <paramref name="key" /> is longer than <see cref="MaxKeySize" />.
    /// </exception>
    public static void HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireHashSize(destination.Length, nameof(destination));
        using var hash = new Blake2s(destination.Length, key);
        hash.AppendData(source);
        hash.GetHashAndReset(destination);
    }

    /// <summary>Adds <paramref name="data" /> to the message.</summary>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void AppendData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        while (!data.IsEmpty)
        {
            if (bufferLength == BlockSize)
            {
                byteCount += BlockSize;
                Compress(isLastBlock: false);
                bufferLength = 0;
            }

            int count = Math.Min(BlockSize - bufferLength, data.Length);
            data[..count].CopyTo(buffer.AsSpan(bufferLength));
            bufferLength += count;
            data = data[count..];
        }
    }

    /// <summary>
    /// Finishes the message, writes its digest to <paramref name="destination" />, and
    /// starts a new message with the same digest length and key.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not the digest length the instance was made with.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void GetHashAndReset(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (destination.Length != hashSize)
        {
            throw new ArgumentException($"This BLAKE2s digest is {hashSize} bytes; the destination is {destination.Length}.", nameof(destination));
        }

        byteCount += (ulong)bufferLength;
        buffer.AsSpan(bufferLength).Clear();
        Compress(isLastBlock: true);
        Span<byte> digest = stackalloc byte[HashSize];
        try
        {
            for (int word = 0; word < chainingState.Length; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(digest[(word * sizeof(uint))..], chainingState[word]);
            }

            digest[..hashSize].CopyTo(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }

        Reset();
    }

    /// <summary>Zeroes the key and the state; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(buffer);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(chainingState.AsSpan()));
        disposed = true;
    }

    private static void RequireHashSize(int length, string parameterName)
    {
        if (length is < 1 or > HashSize)
        {
            throw new ArgumentException($"A BLAKE2s digest is 1 to {HashSize} bytes; this is {length}.", parameterName);
        }
    }

    /// <summary>
    /// The message word schedule SIGMA of RFC 7693 section 2.7, one row of 16 indices per
    /// round.
    /// </summary>
    private static ReadOnlySpan<byte> Sigma =>
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
        11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
        7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
        9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
        2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
        12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
        13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
        6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
        10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
    ];

    /// <summary>
    /// The mixing function G of RFC 7693 section 3.1, with BLAKE2s's rotations 16, 12, 8
    /// and 7, on the words of <paramref name="v" /> at <paramref name="a" /> to
    /// <paramref name="d" />.
    /// </summary>
    private static void Mix(Span<uint> v, int a, int b, int c, int d, uint x, uint y)
    {
        v[a] = v[a] + v[b] + x;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] += v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 12);
        v[a] = v[a] + v[b] + y;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 8);
        v[c] += v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 7);
    }

    /// <summary>The ten rounds of RFC 7693 section 3.2 on the working vector <paramref name="v" />.</summary>
    private static void RunRounds(Span<uint> v, ReadOnlySpan<uint> m)
    {
        for (int round = 0; round < Rounds; round++)
        {
            ReadOnlySpan<byte> s = Sigma.Slice(round * 16, 16);
            Mix(v, 0, 4, 8, 12, m[s[0]], m[s[1]]);
            Mix(v, 1, 5, 9, 13, m[s[2]], m[s[3]]);
            Mix(v, 2, 6, 10, 14, m[s[4]], m[s[5]]);
            Mix(v, 3, 7, 11, 15, m[s[6]], m[s[7]]);
            Mix(v, 0, 5, 10, 15, m[s[8]], m[s[9]]);
            Mix(v, 1, 6, 11, 12, m[s[10]], m[s[11]]);
            Mix(v, 2, 7, 8, 13, m[s[12]], m[s[13]]);
            Mix(v, 3, 4, 9, 14, m[s[14]], m[s[15]]);
        }
    }

    /// <summary>
    /// Sets the chaining state from the parameter block (digest length, key length, fanout
    /// and depth 1) and, for a keyed hash, buffers the zero-padded key as the first block
    /// (RFC 7693 section 3.3).
    /// </summary>
    private void Reset()
    {
        InitializationVector.CopyTo(chainingState, 0);
        chainingState[0] ^= 0x01010000u ^ ((uint)key.Length << 8) ^ (uint)hashSize;
        byteCount = 0;
        buffer.AsSpan().Clear();
        key.CopyTo(buffer, 0);
        bufferLength = key.Length == 0 ? 0 : BlockSize;
    }

    /// <summary>
    /// The compression function F of RFC 7693 section 3.2 on the buffered block, with the
    /// byte counter so far and the final-block flag.
    /// </summary>
    private void Compress(bool isLastBlock)
    {
        Span<uint> m = stackalloc uint[16];
        Span<uint> v = stackalloc uint[16];
        try
        {
            for (int word = 0; word < m.Length; word++)
            {
                m[word] = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(word * sizeof(uint)));
            }

            chainingState.CopyTo(v);
            InitializationVector.CopyTo(v[8..]);
            v[12] ^= (uint)byteCount;
            v[13] ^= (uint)(byteCount >> 32);
            v[14] ^= isLastBlock ? uint.MaxValue : 0u;
            RunRounds(v, m);
            for (int word = 0; word < chainingState.Length; word++)
            {
                chainingState[word] ^= v[word] ^ v[word + 8];
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(m));
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(v));
        }
    }
}
