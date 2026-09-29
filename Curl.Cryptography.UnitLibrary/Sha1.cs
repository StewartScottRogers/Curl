using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The SHA-1 compression function (FIPS 180-4 section 6.1.2): 64-byte blocks, five 32-bit
/// words of state, a 20-byte digest. Constant-time: the 80 steps use fixed word indices and
/// rotations. The BCL's <see cref="SHA1" /> hashes whole messages; this exists so
/// <see cref="FixedBlockHmac" /> can run the compression function over a fixed number of
/// blocks.
/// </summary>
internal readonly struct Sha1 : IBigEndianCompressionFunction
{
    private const int Steps = 80;

    /// <inheritdoc />
    public static int BlockSize => 64;

    /// <inheritdoc />
    public static int LengthFieldSize => sizeof(ulong);

    /// <inheritdoc />
    public static int StateWords => 5;

    /// <inheritdoc />
    public static int HashSize => 20;

    /// <inheritdoc />
    public static void Initialize(Span<ulong> state)
    {
        state[0] = 0x67452301;
        state[1] = 0xefcdab89;
        state[2] = 0x98badcfe;
        state[3] = 0x10325476;
        state[4] = 0xc3d2e1f0;
    }

    /// <inheritdoc />
    public static void Compress(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        Span<uint> schedule = stackalloc uint[Steps];
        Span<uint> working = stackalloc uint[5];
        try
        {
            for (int word = 0; word < 16; word++)
            {
                schedule[word] = BinaryPrimitives.ReadUInt32BigEndian(block[(word * sizeof(uint))..]);
            }

            for (int word = 16; word < Steps; word++)
            {
                schedule[word] = BitOperations.RotateLeft(schedule[word - 3] ^ schedule[word - 8] ^ schedule[word - 14] ^ schedule[word - 16], 1);
            }

            for (int word = 0; word < working.Length; word++)
            {
                working[word] = (uint)state[word];
            }

            for (int step = 0; step < Steps; step++)
            {
                Step(working, step, schedule[step]);
            }

            for (int word = 0; word < working.Length; word++)
            {
                state[word] = (uint)(state[word] + working[word]);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(schedule));
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(working));
        }
    }

    /// <inheritdoc />
    public static void WriteDigest(ReadOnlySpan<ulong> state, Span<byte> destination)
    {
        for (int word = 0; word < StateWords; word++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination[(word * sizeof(uint))..], (uint)state[word]);
        }
    }

    /// <summary>
    /// One step on registers a to e (<paramref name="working" />): T = rol(a, 5) + f(b, c, d)
    /// + e + K + W, then e = d, d = c, c = rol(b, 30), b = a, a = T.
    /// </summary>
    private static void Step(Span<uint> working, int step, uint scheduledWord)
    {
        uint t = BitOperations.RotateLeft(working[0], 5) + RoundFunction(step / 20, working[1], working[2], working[3]) + working[4] + scheduledWord;
        working[4] = working[3];
        working[3] = working[2];
        working[2] = BitOperations.RotateLeft(working[1], 30);
        working[1] = working[0];
        working[0] = t;
    }

    /// <summary>The round's boolean function plus its constant: Ch, Parity, Maj, Parity.</summary>
    private static uint RoundFunction(int round, uint x, uint y, uint z) => round switch
    {
        0 => ((x & y) ^ (~x & z)) + 0x5a827999,
        1 => (x ^ y ^ z) + 0x6ed9eba1,
        2 => ((x & y) ^ (x & z) ^ (y & z)) + 0x8f1bbcdc,
        _ => (x ^ y ^ z) + 0xca62c1d6,
    };
}
