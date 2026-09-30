using System.Buffers.Binary;

namespace Curl.Authentication;

/// <summary>
/// Computes SHA-224 (FIPS 180-4, section 6.3), the hash a <c>sha224WithRSAEncryption</c>,
/// <c>ecdsa-with-SHA224</c> or <c>dsa-with-sha224</c> certificate's <c>tls-server-end-point</c>
/// channel bindings take (RFC 5929 section 4.1, BL-965), which the base class library does
/// not provide.
/// </summary>
/// <remarks>
/// SHA-224 is SHA-256 started from its own initial hash value and truncated to the first
/// 224 bits, so it is not SHA-256 truncated.
/// </remarks>
internal static class Sha224
{
    private const int BlockLength = 64;

    private static readonly uint[] InitialHashValue =
    [
        0xC1059ED8, 0x367CD507, 0x3070DD17, 0xF70E5939, 0xFFC00B31, 0x68581511, 0x64F98FA7, 0xBEFA4FA4,
    ];

    private static readonly uint[] RoundConstants =
    [
        0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
        0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
        0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
        0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
        0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13, 0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
        0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
        0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
        0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2,
    ];

    /// <summary>
    /// Computes the SHA-224 hash of <paramref name="message" />.
    /// </summary>
    /// <param name="message">The bytes to hash.</param>
    /// <returns>The 28-byte hash.</returns>
    internal static byte[] HashData(ReadOnlySpan<byte> message)
    {
        uint[] state = (uint[])InitialHashValue.Clone();
        byte[] padded = Pad(message);
        for (int offset = 0; offset < padded.Length; offset += BlockLength)
        {
            Compress(state, padded.AsSpan(offset, BlockLength));
        }

        byte[] hash = new byte[28];
        for (int word = 0; word < 7; word++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(hash.AsSpan(word * 4), state[word]);
        }

        return hash;
    }

    // The message, a 0x80 byte, zeros to 56 mod 64, and the length in bits as a 64-bit
    // big-endian number.
    private static byte[] Pad(ReadOnlySpan<byte> message)
    {
        int length = ((message.Length + 8) / BlockLength + 1) * BlockLength;
        byte[] padded = new byte[length];
        message.CopyTo(padded);
        padded[message.Length] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(padded.AsSpan(length - 8), (ulong)message.Length * 8);
        return padded;
    }

    private static void Compress(uint[] state, ReadOnlySpan<byte> block)
    {
        Span<uint> schedule = stackalloc uint[64];
        for (int t = 0; t < 16; t++)
        {
            schedule[t] = BinaryPrimitives.ReadUInt32BigEndian(block[(t * 4)..]);
        }

        for (int t = 16; t < 64; t++)
        {
            schedule[t] = SmallSigma1(schedule[t - 2]) + schedule[t - 7] + SmallSigma0(schedule[t - 15]) + schedule[t - 16];
        }

        Span<uint> working = stackalloc uint[8];
        state.CopyTo(working);
        for (int t = 0; t < 64; t++)
        {
            Round(working, RoundConstants[t] + schedule[t]);
        }

        for (int i = 0; i < 8; i++)
        {
            state[i] += working[i];
        }
    }

    // One SHA-256 round over a..h, held in working[0..7].
    private static void Round(Span<uint> working, uint constantPlusWord)
    {
        uint a = working[0], b = working[1], c = working[2], e = working[4], f = working[5], g = working[6];
        uint temporary1 = working[7] + BigSigma1(e) + ((e & f) ^ (~e & g)) + constantPlusWord;
        uint temporary2 = BigSigma0(a) + ((a & b) ^ (a & c) ^ (b & c));
        working[7] = g;
        working[6] = f;
        working[5] = e;
        working[4] = working[3] + temporary1;
        working[3] = c;
        working[2] = b;
        working[1] = a;
        working[0] = temporary1 + temporary2;
    }

    private static uint BigSigma0(uint x) => uint.RotateRight(x, 2) ^ uint.RotateRight(x, 13) ^ uint.RotateRight(x, 22);

    private static uint BigSigma1(uint x) => uint.RotateRight(x, 6) ^ uint.RotateRight(x, 11) ^ uint.RotateRight(x, 25);

    private static uint SmallSigma0(uint x) => uint.RotateRight(x, 7) ^ uint.RotateRight(x, 18) ^ (x >> 3);

    private static uint SmallSigma1(uint x) => uint.RotateRight(x, 17) ^ uint.RotateRight(x, 19) ^ (x >> 10);
}
