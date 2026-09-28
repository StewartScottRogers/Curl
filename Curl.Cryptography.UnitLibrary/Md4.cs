using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The MD4 message digest (RFC 1320): any message gives a 16-byte hash. NTLM's
/// <c>NTOWFv1</c> and Kerberos' <c>rc4-hmac</c> string-to-key hash a password with it.
/// Broken as a collision-resistant hash; it is here only because those protocols fix it.
/// </summary>
/// <remarks>
/// Hash a whole message with <see cref="HashData" />, or feed it in pieces with
/// <see cref="AppendData" /> and end it with <see cref="GetHashAndReset" />; both give the
/// same bytes. Constant-time: the 48 steps use fixed word orders and rotations, so no
/// branch or index depends on the message, only on its length. The state, the buffered
/// bytes and the message words are zeroed after use and by <see cref="Dispose" />.
/// </remarks>
public sealed class Md4 : IDisposable, ILittleEndianCompressionFunction
{
    /// <summary>The length in bytes of a hash.</summary>
    public const int HashSize = 16;

    private const int Steps = 48;

    /// <summary>The message word each step adds (RFC 1320 section 3.4, rounds 1 to 3).</summary>
    private static ReadOnlySpan<byte> WordOrder =>
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15,
        0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15,
    ];

    /// <summary>The left rotations of each round, used in turn by its steps.</summary>
    private static ReadOnlySpan<byte> Rotations => [3, 7, 11, 19, 3, 5, 9, 13, 3, 9, 11, 15];

    private readonly LittleEndianMerkleDamgard<Md4> hash = new();

    private bool disposed;

    /// <inheritdoc />
    static int ILittleEndianCompressionFunction.StateWords => HashSize / sizeof(uint);

    /// <summary>Writes the MD4 hash of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    public static void HashData(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using Md4 md4 = new();
        md4.AppendData(source);
        md4.GetHashAndReset(destination);
    }

    /// <summary>Adds <paramref name="data" /> to the message being hashed.</summary>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void AppendData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        hash.Append(data);
    }

    /// <summary>
    /// Writes the hash of the data appended so far to <paramref name="destination" /> and
    /// starts a new, empty message.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void GetHashAndReset(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (destination.Length != HashSize)
        {
            throw new ArgumentException($"MD4 needs a {HashSize}-byte destination; this is {destination.Length}.", nameof(destination));
        }

        hash.Finish(destination);
    }

    /// <summary>Zeroes the state and any buffered data.</summary>
    public void Dispose()
    {
        hash.Clear();
        disposed = true;
    }

    /// <inheritdoc />
    static void ILittleEndianCompressionFunction.Initialize(Span<uint> state)
    {
        state[0] = 0x67452301;
        state[1] = 0xefcdab89;
        state[2] = 0x98badcfe;
        state[3] = 0x10325476;
    }

    /// <inheritdoc />
    static void ILittleEndianCompressionFunction.Compress(Span<uint> state, ReadOnlySpan<byte> block)
    {
        Span<uint> words = stackalloc uint[16];
        try
        {
            for (int word = 0; word < words.Length; word++)
            {
                words[word] = BinaryPrimitives.ReadUInt32LittleEndian(block[(word * sizeof(uint))..]);
            }

            uint a = state[0], b = state[1], c = state[2], d = state[3];
            for (int step = 0; step < Steps; step++)
            {
                int round = step >> 4;
                uint sum = a + RoundFunction(round, b, c, d) + words[WordOrder[step]] + RoundConstant(round);
                (a, b, c, d) = (d, BitOperations.RotateLeft(sum, Rotations[(round << 2) + (step & 3)]), b, c);
            }

            state[0] += a;
            state[1] += b;
            state[2] += c;
            state[3] += d;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
        }
    }

    /// <summary>RFC 1320's F (round 1), G (round 2) and H (round 3).</summary>
    private static uint RoundFunction(int round, uint x, uint y, uint z) => round switch
    {
        0 => (x & y) | (~x & z),
        1 => (x & y) | (x & z) | (y & z),
        _ => x ^ y ^ z,
    };

    /// <summary>The constant each round's steps add: none, then the square roots of 2 and 3.</summary>
    private static uint RoundConstant(int round) => round switch
    {
        0 => 0,
        1 => 0x5a827999,
        _ => 0x6ed9eba1,
    };
}
