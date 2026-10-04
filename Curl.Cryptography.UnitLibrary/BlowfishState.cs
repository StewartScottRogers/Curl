using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Blowfish's keyed state - the 18-word P-array and the four 256-word S-boxes - with the
/// cipher's 16 rounds and the key expansions of OpenBSD's <c>blf.c</c>: the standard key
/// schedule (<see cref="ExpandKey(ReadOnlySpan{byte})" />, <c>Blowfish_expand0state</c>)
/// and the salted one bcrypt adds (<see cref="ExpandKey(ReadOnlySpan{byte}, ReadOnlySpan{byte})" />,
/// <c>Blowfish_expandstate</c>).
/// </summary>
/// <remarks>
/// Constant-time in the key and the data: the F function (<see cref="Mix" />) reads every
/// entry of all four S-boxes and keeps the ones it needs by mask, so no memory address
/// depends on the key, the password or the block (ADR-0400). <see cref="Clear" /> zeroes
/// the state.
/// </remarks>
internal sealed class BlowfishState
{
    private const int Rounds = 16;

    private readonly uint[] subkeys = new uint[Rounds + 2];

    private readonly uint[] boxes = new uint[4 * 256];

    /// <summary>Loads the digits of pi into the P-array and S-boxes, before any key.</summary>
    internal void Initialize()
    {
        BlowfishPiDigits.Subkeys.CopyTo(subkeys);
        BlowfishPiDigits.SubstitutionBoxes.CopyTo(boxes);
    }

    /// <summary>
    /// Blowfish's key schedule (<c>Blowfish_expand0state</c>): exclusive-ors
    /// <paramref name="key" />, cycled, into the P-array, then replaces the P-array and
    /// S-boxes with successive encryptions of an all-zero block.
    /// </summary>
    internal void ExpandKey(ReadOnlySpan<byte> key) => ExpandKey(default, key);

    /// <summary>
    /// bcrypt's salted key schedule (<c>Blowfish_expandstate</c>): as
    /// <see cref="ExpandKey(ReadOnlySpan{byte})" />, but each block is exclusive-ored with
    /// the next eight bytes of <paramref name="data" />, cycled, before it is encrypted.
    /// Empty <paramref name="data" /> is the unsalted schedule.
    /// </summary>
    internal void ExpandKey(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        int keyPosition = 0;
        for (int index = 0; index < subkeys.Length; index++)
        {
            subkeys[index] ^= ReadWord(key, ref keyPosition);
        }

        uint left = 0;
        uint right = 0;
        int dataPosition = 0;
        ReplaceWithEncryptions(subkeys, data, ref dataPosition, ref left, ref right);
        ReplaceWithEncryptions(boxes, data, ref dataPosition, ref left, ref right);
    }

    /// <summary>Encrypts the block (<paramref name="left" />, <paramref name="right" />) in place.</summary>
    internal void Encrypt(ref uint left, ref uint right)
    {
        uint x = left ^ subkeys[0];
        uint y = right;
        for (int round = 1; round <= Rounds; round += 2)
        {
            y ^= Mix(x) ^ subkeys[round];
            x ^= Mix(y) ^ subkeys[round + 1];
        }

        left = y ^ subkeys[Rounds + 1];
        right = x;
    }

    /// <summary>Decrypts the block (<paramref name="left" />, <paramref name="right" />) in place.</summary>
    internal void Decrypt(ref uint left, ref uint right)
    {
        uint x = left ^ subkeys[Rounds + 1];
        uint y = right;
        for (int round = Rounds; round >= 1; round -= 2)
        {
            y ^= Mix(x) ^ subkeys[round];
            x ^= Mix(y) ^ subkeys[round - 1];
        }

        left = y ^ subkeys[0];
        right = x;
    }

    /// <summary>Zeroes the P-array and S-boxes.</summary>
    internal void Clear()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subkeys.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(boxes.AsSpan()));
    }

    /// <summary>
    /// The next four bytes of <paramref name="data" /> as a big-endian word, wrapping to
    /// its start when they run out (<c>Blowfish_stream2word</c>). Empty data reads as zero.
    /// </summary>
    internal static uint ReadWord(ReadOnlySpan<byte> data, ref int position)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        uint word = 0;
        for (int count = 0; count < 4; count++)
        {
            position %= data.Length;
            word = (word << 8) | data[position];
            position++;
        }

        return word;
    }

    private void ReplaceWithEncryptions(uint[] words, ReadOnlySpan<byte> data, ref int dataPosition, ref uint left, ref uint right)
    {
        for (int index = 0; index < words.Length; index += 2)
        {
            left ^= ReadWord(data, ref dataPosition);
            right ^= ReadWord(data, ref dataPosition);
            Encrypt(ref left, ref right);
            words[index] = left;
            words[index + 1] = right;
        }
    }

    /// <summary>
    /// Blowfish's F function: ((S1[a] + S2[b]) ^ S3[c]) + S4[d] over the bytes of
    /// <paramref name="half" />, without a secret-dependent address (ADR-0400): one pass
    /// reads every entry of all four S-boxes, in order, a vector at a time, and keeps the
    /// entry at each box's index by an equality mask.
    /// </summary>
    internal uint Mix(uint half)
    {
        nuint width = (nuint)Vector<uint>.Count;
        Vector<uint> step = new((uint)width);
        Vector<uint> position = Vector<uint>.Indices;
        Vector<uint> first = new(half >> 24);
        Vector<uint> second = new((half >> 16) & 0xFF);
        Vector<uint> third = new((half >> 8) & 0xFF);
        Vector<uint> fourth = new(half & 0xFF);
        Vector<uint> fromFirst = Vector<uint>.Zero;
        Vector<uint> fromSecond = Vector<uint>.Zero;
        Vector<uint> fromThird = Vector<uint>.Zero;
        Vector<uint> fromFourth = Vector<uint>.Zero;
        // The loop bound is fixed and 256 is a multiple of every vector width, so no load leaves the boxes.
        ref uint table = ref MemoryMarshal.GetArrayDataReference(boxes);
        for (nuint offset = 0; offset < 256; offset += width)
        {
            fromFirst |= Vector.Equals(position, first) & Vector.LoadUnsafe(ref table, offset);
            fromSecond |= Vector.Equals(position, second) & Vector.LoadUnsafe(ref table, 256 + offset);
            fromThird |= Vector.Equals(position, third) & Vector.LoadUnsafe(ref table, 512 + offset);
            fromFourth |= Vector.Equals(position, fourth) & Vector.LoadUnsafe(ref table, 768 + offset);
            position += step;
        }

        // One lane of each accumulator holds the entry and the rest are zero, so the sum is the entry.
        return ((Vector.Sum(fromFirst) + Vector.Sum(fromSecond)) ^ Vector.Sum(fromThird)) + Vector.Sum(fromFourth);
    }
}
