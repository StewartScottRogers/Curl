using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The SHA-256 compression function (FIPS 180-4 section 6.2.2): 64-byte blocks, eight
/// 32-bit words of state, a 32-byte digest. Constant-time: 64 rounds with fixed indices and
/// rotations. Exists so <see cref="FixedBlockHmac" /> can run the compression function over
/// a fixed number of blocks; the BCL's <see cref="SHA256" /> hides it.
/// </summary>
internal readonly struct Sha256 : IBigEndianCompressionFunction
{
    private const int Rounds = 64;

    /// <inheritdoc />
    public static int BlockSize => 64;

    /// <inheritdoc />
    public static int LengthFieldSize => sizeof(ulong);

    /// <inheritdoc />
    public static int StateWords => 8;

    /// <inheritdoc />
    public static int HashSize => 32;

    /// <summary>The round constants K0 to K63 (FIPS 180-4 section 4.2.2).</summary>
    private static ReadOnlySpan<uint> RoundConstants =>
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    /// <inheritdoc />
    public static void Initialize(Span<ulong> state)
    {
        state[0] = 0x6a09e667;
        state[1] = 0xbb67ae85;
        state[2] = 0x3c6ef372;
        state[3] = 0xa54ff53a;
        state[4] = 0x510e527f;
        state[5] = 0x9b05688c;
        state[6] = 0x1f83d9ab;
        state[7] = 0x5be0cd19;
    }

    /// <inheritdoc />
    public static void Compress(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        Span<uint> schedule = stackalloc uint[Rounds];
        Span<uint> working = stackalloc uint[8];
        try
        {
            for (int word = 0; word < 16; word++)
            {
                schedule[word] = BinaryPrimitives.ReadUInt32BigEndian(block[(word * sizeof(uint))..]);
            }

            for (int word = 16; word < Rounds; word++)
            {
                uint before15 = schedule[word - 15];
                uint before2 = schedule[word - 2];
                uint sigma0 = BitOperations.RotateRight(before15, 7) ^ BitOperations.RotateRight(before15, 18) ^ (before15 >> 3);
                uint sigma1 = BitOperations.RotateRight(before2, 17) ^ BitOperations.RotateRight(before2, 19) ^ (before2 >> 10);
                schedule[word] = sigma1 + schedule[word - 7] + sigma0 + schedule[word - 16];
            }

            for (int word = 0; word < working.Length; word++)
            {
                working[word] = (uint)state[word];
            }

            for (int round = 0; round < Rounds; round++)
            {
                Round(working, RoundConstants[round] + schedule[round]);
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

    /// <summary>One round on registers a to h (<paramref name="working" />), with K + W as <paramref name="constantPlusWord" />.</summary>
    private static void Round(Span<uint> working, uint constantPlusWord)
    {
        uint a = working[0];
        uint e = working[4];
        uint bigSigma1 = BitOperations.RotateRight(e, 6) ^ BitOperations.RotateRight(e, 11) ^ BitOperations.RotateRight(e, 25);
        uint choose = (e & working[5]) ^ (~e & working[6]);
        uint t1 = working[7] + bigSigma1 + choose + constantPlusWord;
        uint bigSigma0 = BitOperations.RotateRight(a, 2) ^ BitOperations.RotateRight(a, 13) ^ BitOperations.RotateRight(a, 22);
        uint majority = (a & working[1]) ^ (a & working[2]) ^ (working[1] & working[2]);
        working[7] = working[6];
        working[6] = working[5];
        working[5] = e;
        working[4] = working[3] + t1;
        working[3] = working[2];
        working[2] = working[1];
        working[1] = a;
        working[0] = t1 + bigSigma0 + majority;
    }
}
