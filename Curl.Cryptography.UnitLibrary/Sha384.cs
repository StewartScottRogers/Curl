using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// SHA-384 (FIPS 180-4 sections 5.3.4 and 6.5): the SHA-512 compression function - 128-byte
/// blocks, eight 64-bit words of state, a 16-byte bit length - from SHA-384's initial hash
/// value, its digest the first 48 bytes. Constant-time: 80 rounds with fixed indices and
/// rotations. Exists so <see cref="FixedBlockHmac" /> can run the compression function over
/// a fixed number of blocks; the BCL's <see cref="SHA384" /> hides it.
/// </summary>
internal readonly struct Sha384 : IBigEndianCompressionFunction
{
    private const int Rounds = 80;

    /// <inheritdoc />
    public static int BlockSize => 128;

    /// <inheritdoc />
    public static int LengthFieldSize => 2 * sizeof(ulong);

    /// <inheritdoc />
    public static int StateWords => 8;

    /// <inheritdoc />
    public static int HashSize => 48;

    /// <summary>The SHA-512 round constants K0 to K79 (FIPS 180-4 section 4.2.3).</summary>
    private static ReadOnlySpan<ulong> RoundConstants =>
    [
        0x428a2f98d728ae22, 0x7137449123ef65cd, 0xb5c0fbcfec4d3b2f, 0xe9b5dba58189dbbc,
        0x3956c25bf348b538, 0x59f111f1b605d019, 0x923f82a4af194f9b, 0xab1c5ed5da6d8118,
        0xd807aa98a3030242, 0x12835b0145706fbe, 0x243185be4ee4b28c, 0x550c7dc3d5ffb4e2,
        0x72be5d74f27b896f, 0x80deb1fe3b1696b1, 0x9bdc06a725c71235, 0xc19bf174cf692694,
        0xe49b69c19ef14ad2, 0xefbe4786384f25e3, 0x0fc19dc68b8cd5b5, 0x240ca1cc77ac9c65,
        0x2de92c6f592b0275, 0x4a7484aa6ea6e483, 0x5cb0a9dcbd41fbd4, 0x76f988da831153b5,
        0x983e5152ee66dfab, 0xa831c66d2db43210, 0xb00327c898fb213f, 0xbf597fc7beef0ee4,
        0xc6e00bf33da88fc2, 0xd5a79147930aa725, 0x06ca6351e003826f, 0x142929670a0e6e70,
        0x27b70a8546d22ffc, 0x2e1b21385c26c926, 0x4d2c6dfc5ac42aed, 0x53380d139d95b3df,
        0x650a73548baf63de, 0x766a0abb3c77b2a8, 0x81c2c92e47edaee6, 0x92722c851482353b,
        0xa2bfe8a14cf10364, 0xa81a664bbc423001, 0xc24b8b70d0f89791, 0xc76c51a30654be30,
        0xd192e819d6ef5218, 0xd69906245565a910, 0xf40e35855771202a, 0x106aa07032bbd1b8,
        0x19a4c116b8d2d0c8, 0x1e376c085141ab53, 0x2748774cdf8eeb99, 0x34b0bcb5e19b48a8,
        0x391c0cb3c5c95a63, 0x4ed8aa4ae3418acb, 0x5b9cca4f7763e373, 0x682e6ff3d6b2b8a3,
        0x748f82ee5defb2fc, 0x78a5636f43172f60, 0x84c87814a1f0ab72, 0x8cc702081a6439ec,
        0x90befffa23631e28, 0xa4506cebde82bde9, 0xbef9a3f7b2c67915, 0xc67178f2e372532b,
        0xca273eceea26619c, 0xd186b8c721c0c207, 0xeada7dd6cde0eb1e, 0xf57d4f7fee6ed178,
        0x06f067aa72176fba, 0x0a637dc5a2c898a6, 0x113f9804bef90dae, 0x1b710b35131c471b,
        0x28db77f523047d84, 0x32caab7b40c72493, 0x3c9ebe0a15c9bebc, 0x431d67c49c100d4c,
        0x4cc5d4becb3e42b6, 0x597f299cfc657e2a, 0x5fcb6fab3ad6faec, 0x6c44198c4a475817,
    ];

    /// <inheritdoc />
    public static void Initialize(Span<ulong> state)
    {
        state[0] = 0xcbbb9d5dc1059ed8;
        state[1] = 0x629a292a367cd507;
        state[2] = 0x9159015a3070dd17;
        state[3] = 0x152fecd8f70e5939;
        state[4] = 0x67332667ffc00b31;
        state[5] = 0x8eb44a8768581511;
        state[6] = 0xdb0c2e0d64f98fa7;
        state[7] = 0x47b5481dbefa4fa4;
    }

    /// <inheritdoc />
    public static void Compress(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        Span<ulong> schedule = stackalloc ulong[Rounds];
        Span<ulong> working = stackalloc ulong[8];
        try
        {
            for (int word = 0; word < 16; word++)
            {
                schedule[word] = BinaryPrimitives.ReadUInt64BigEndian(block[(word * sizeof(ulong))..]);
            }

            for (int word = 16; word < Rounds; word++)
            {
                ulong before15 = schedule[word - 15];
                ulong before2 = schedule[word - 2];
                ulong sigma0 = BitOperations.RotateRight(before15, 1) ^ BitOperations.RotateRight(before15, 8) ^ (before15 >> 7);
                ulong sigma1 = BitOperations.RotateRight(before2, 19) ^ BitOperations.RotateRight(before2, 61) ^ (before2 >> 6);
                schedule[word] = sigma1 + schedule[word - 7] + sigma0 + schedule[word - 16];
            }

            state.CopyTo(working);
            for (int round = 0; round < Rounds; round++)
            {
                Round(working, RoundConstants[round] + schedule[round]);
            }

            for (int word = 0; word < working.Length; word++)
            {
                state[word] += working[word];
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
        for (int word = 0; word < HashSize / sizeof(ulong); word++)
        {
            BinaryPrimitives.WriteUInt64BigEndian(destination[(word * sizeof(ulong))..], state[word]);
        }
    }

    /// <summary>One round on registers a to h (<paramref name="working" />), with K + W as <paramref name="constantPlusWord" />.</summary>
    private static void Round(Span<ulong> working, ulong constantPlusWord)
    {
        ulong a = working[0];
        ulong e = working[4];
        ulong bigSigma1 = BitOperations.RotateRight(e, 14) ^ BitOperations.RotateRight(e, 18) ^ BitOperations.RotateRight(e, 41);
        ulong choose = (e & working[5]) ^ (~e & working[6]);
        ulong t1 = working[7] + bigSigma1 + choose + constantPlusWord;
        ulong bigSigma0 = BitOperations.RotateRight(a, 28) ^ BitOperations.RotateRight(a, 34) ^ BitOperations.RotateRight(a, 39);
        ulong majority = (a & working[1]) ^ (a & working[2]) ^ (working[1] & working[2]);
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
