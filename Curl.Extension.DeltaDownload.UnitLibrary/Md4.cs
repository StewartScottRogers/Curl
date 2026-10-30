using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Extension.DeltaDownload;

/// <summary>
/// The MD4 message digest (RFC 1320), written here because the base class library has none: zsync 0.6's block
/// checksums are the leading bytes of each block's MD4 digest.
/// </summary>
internal static class Md4
{
    private static readonly int[] Shifts = [3, 7, 11, 19, 3, 5, 9, 13, 3, 9, 11, 15];

    private static readonly int[] ThirdRoundOrder = [0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15];

    /// <summary>Gives the 16-byte MD4 digest of <paramref name="data" /> (RFC 1320 §3).</summary>
    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        // §3.1 and §3.2: one 0x80 byte, zeros to 56 bytes modulo 64, then the bit length, little-endian.
        var padded = new byte[((data.Length + 8) / 64 + 1) * 64];
        data.CopyTo(padded);
        padded[data.Length] = 0x80;
        BinaryPrimitives.WriteUInt64LittleEndian(padded.AsSpan(padded.Length - 8), (ulong)data.Length * 8);

        uint[] state = [0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476];
        for (var offset = 0; offset < padded.Length; offset += 64)
        {
            ProcessBlock(state, padded.AsSpan(offset, 64));
        }

        var digest = new byte[16];
        for (var index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(digest.AsSpan(index * 4), state[index]);
        }

        return digest;
    }

    // §3.4: three rounds of 16 operations; operation i updates A, D, C, B in turn from the other three words.
    private static void ProcessBlock(uint[] state, ReadOnlySpan<byte> block)
    {
        Span<uint> words = stackalloc uint[16];
        for (var index = 0; index < 16; index++)
        {
            words[index] = BinaryPrimitives.ReadUInt32LittleEndian(block[(index * 4)..]);
        }

        uint[] working = [.. state];
        for (var operation = 0; operation < 48; operation++)
        {
            var round = operation / 16;
            var step = operation % 16;
            var target = (4 - step % 4) % 4;
            var mixed = Mix(round, working[(target + 1) % 4], working[(target + 2) % 4], working[(target + 3) % 4]);
            working[target] = BitOperations.RotateLeft(working[target] + mixed + words[WordIndex(round, step)], Shifts[round * 4 + step % 4]);
        }

        for (var index = 0; index < 4; index++)
        {
            state[index] += working[index];
        }
    }

    private static uint Mix(int round, uint x, uint y, uint z) => round switch
    {
        0 => (x & y) | (~x & z),
        1 => ((x & y) | (x & z) | (y & z)) + 0x5A827999,
        _ => (x ^ y ^ z) + 0x6ED9EBA1,
    };

    private static int WordIndex(int round, int step) => round switch
    {
        0 => step,
        1 => step % 4 * 4 + step / 4,
        _ => ThirdRoundOrder[step],
    };
}
