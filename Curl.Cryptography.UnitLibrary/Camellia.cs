using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The Camellia block cipher (RFC 3713): 128-bit blocks, a key of 128, 192 or 256 bits,
/// 18 rounds for a 128-bit key and 24 for the longer two. <see cref="EncryptBlock" /> and
/// <see cref="DecryptBlock" /> are single blocks (ECB); <see cref="EncryptCbc" /> and
/// <see cref="DecryptCbc" /> are the cipher block chaining mode of TLS's
/// <c>*_WITH_CAMELLIA_*_CBC_*</c> suites (RFC 5932). Blocks are read and written
/// big-endian, as RFC 3713 specifies.
/// </summary>
/// <remarks>
/// Not constant-time: the F-function indexes Camellia's fixed S-boxes with key-mixed data
/// bytes, as OpenSSL's and LibreSSL's own Camellia do (ADR-0145); it exists because curl's
/// LibreSSL and OpenSSL builds offer the Camellia suites (ADR-0140). The key schedule is
/// held in the instance and zeroed by <see cref="Dispose" />.
/// </remarks>
public sealed class Camellia : IDisposable
{
    /// <summary>The length in bytes of a block.</summary>
    public const int BlockSize = 16;

    private const int KeyLeft = 0;
    private const int KeyRight = 1;
    private const int KeyA = 2;
    private const int KeyB = 3;

    private static readonly ulong[] Sigmas =
    [
        0xA09E667F3BCC908BUL, 0xB67AE8584CAA73B2UL, 0xC6EF372FE94F82BEUL,
        0x54FF53A5F1D36F1CUL, 0x10E527FADE682D1DUL, 0xB05688C2B3E6C1FDUL,
    ];

    // RFC 3713 section 2.4.4, SBOX1; SBOX2, SBOX3 and SBOX4 are derived from it.
    private static ReadOnlySpan<byte> SubstitutionBox1 =>
    [
        112, 130, 44, 236, 179, 39, 192, 229, 228, 133, 87, 53, 234, 12, 174, 65,
        35, 239, 107, 147, 69, 25, 165, 33, 237, 14, 79, 78, 29, 101, 146, 189,
        134, 184, 175, 143, 124, 235, 31, 206, 62, 48, 220, 95, 94, 197, 11, 26,
        166, 225, 57, 202, 213, 71, 93, 61, 217, 1, 90, 214, 81, 86, 108, 77,
        139, 13, 154, 102, 251, 204, 176, 45, 116, 18, 43, 32, 240, 177, 132, 153,
        223, 76, 203, 194, 52, 126, 118, 5, 109, 183, 169, 49, 209, 23, 4, 215,
        20, 88, 58, 97, 222, 27, 17, 28, 50, 15, 156, 22, 83, 24, 242, 34,
        254, 68, 207, 178, 195, 181, 122, 145, 36, 8, 232, 168, 96, 252, 105, 80,
        170, 208, 160, 125, 161, 137, 98, 151, 84, 91, 30, 149, 224, 255, 100, 210,
        16, 196, 0, 72, 163, 247, 117, 219, 138, 3, 230, 218, 9, 63, 221, 148,
        135, 92, 131, 2, 205, 74, 144, 51, 115, 103, 246, 243, 157, 127, 191, 226,
        82, 155, 216, 38, 200, 55, 198, 59, 129, 150, 111, 75, 19, 190, 99, 46,
        233, 121, 167, 140, 159, 110, 188, 142, 41, 245, 249, 182, 47, 253, 180, 89,
        120, 152, 6, 106, 231, 70, 113, 186, 212, 37, 171, 66, 136, 162, 141, 250,
        114, 7, 185, 85, 248, 238, 172, 10, 54, 73, 42, 104, 60, 56, 241, 164,
        64, 40, 211, 123, 187, 201, 67, 193, 21, 227, 173, 244, 119, 199, 128, 158,
    ];

    // RFC 3713 section 2.2, the 128-bit key's subkeys in the order encryption uses them:
    // kw1, kw2, k1 to k6, ke1, ke2, k7 to k12, ke3, ke4, k13 to k18, kw3, kw4. Each is
    // the high or low half of KL or KA rotated left.
    private static readonly (int Part, int Rotation, bool High)[] ShortKeySubkeys =
    [
        (KeyLeft, 0, true), (KeyLeft, 0, false),
        (KeyA, 0, true), (KeyA, 0, false), (KeyLeft, 15, true), (KeyLeft, 15, false), (KeyA, 15, true), (KeyA, 15, false),
        (KeyA, 30, true), (KeyA, 30, false),
        (KeyLeft, 45, true), (KeyLeft, 45, false), (KeyA, 45, true), (KeyLeft, 60, false), (KeyA, 60, true), (KeyA, 60, false),
        (KeyLeft, 77, true), (KeyLeft, 77, false),
        (KeyLeft, 94, true), (KeyLeft, 94, false), (KeyA, 94, true), (KeyA, 94, false), (KeyLeft, 111, true), (KeyLeft, 111, false),
        (KeyA, 111, true), (KeyA, 111, false),
    ];

    // RFC 3713 section 2.2, the 192- and 256-bit keys' subkeys in the order encryption
    // uses them: kw1, kw2, k1 to k6, ke1, ke2, k7 to k12, ke3, ke4, k13 to k18, ke5, ke6,
    // k19 to k24, kw3, kw4.
    private static readonly (int Part, int Rotation, bool High)[] LongKeySubkeys =
    [
        (KeyLeft, 0, true), (KeyLeft, 0, false),
        (KeyB, 0, true), (KeyB, 0, false), (KeyRight, 15, true), (KeyRight, 15, false), (KeyA, 15, true), (KeyA, 15, false),
        (KeyRight, 30, true), (KeyRight, 30, false),
        (KeyB, 30, true), (KeyB, 30, false), (KeyLeft, 45, true), (KeyLeft, 45, false), (KeyA, 45, true), (KeyA, 45, false),
        (KeyLeft, 60, true), (KeyLeft, 60, false),
        (KeyRight, 60, true), (KeyRight, 60, false), (KeyB, 60, true), (KeyB, 60, false), (KeyLeft, 77, true), (KeyLeft, 77, false),
        (KeyA, 77, true), (KeyA, 77, false),
        (KeyRight, 94, true), (KeyRight, 94, false), (KeyA, 94, true), (KeyA, 94, false), (KeyLeft, 111, true), (KeyLeft, 111, false),
        (KeyB, 111, true), (KeyB, 111, false),
    ];

    private readonly ulong[] encryptionSubkeys;

    private readonly ulong[] decryptionSubkeys;

    private readonly int roundGroups;

    private bool disposed;

    /// <summary>Runs Camellia's key schedule on <paramref name="key" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not 16, 24 or 32 bytes.</exception>
    public Camellia(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException($"Camellia needs a key of 16, 24 or 32 bytes; this is {key.Length}.", nameof(key));
        }

        (int Part, int Rotation, bool High)[] layout = key.Length == 16 ? ShortKeySubkeys : LongKeySubkeys;
        // Four whitening subkeys, six per group of rounds and two per FL layer between
        // groups: 26 = 4 + 18 + 4 for three groups, 34 = 4 + 24 + 6 for four.
        roundGroups = (layout.Length - 2) / 8;
        encryptionSubkeys = new ulong[layout.Length];
        decryptionSubkeys = new ulong[layout.Length];
        ExpandEncryptionSubkeys(key, layout, encryptionSubkeys);
        ReverseForDecryption(encryptionSubkeys, decryptionSubkeys);
    }

    /// <summary>The subkeys in the order encryption uses them; all zero after <see cref="Dispose" />.</summary>
    internal ReadOnlySpan<ulong> EncryptionSubkeys => encryptionSubkeys;

    /// <summary>The subkeys in the order decryption uses them; all zero after <see cref="Dispose" />.</summary>
    internal ReadOnlySpan<ulong> DecryptionSubkeys => decryptionSubkeys;

    /// <summary>Encrypts the one block <paramref name="source" /> into <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException">A span is not <see cref="BlockSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void EncryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(source.Length, destination.Length, BlockSize);
        TransformBlock(source, destination, encryptionSubkeys);
    }

    /// <summary>Decrypts the one block <paramref name="source" /> into <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException">A span is not <see cref="BlockSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void DecryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(source.Length, destination.Length, BlockSize);
        TransformBlock(source, destination, decryptionSubkeys);
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

                TransformBlock(chain, chain, encryptionSubkeys);
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
                TransformBlock(ciphertext, plaintext, decryptionSubkeys);
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
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(encryptionSubkeys.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(decryptionSubkeys.AsSpan()));
        disposed = true;
    }

    /// <summary>
    /// RFC 3713 section 2.2: each subkey in <paramref name="layout" />, the high or low half
    /// of a rotated key part, into <paramref name="subkeys" />.
    /// </summary>
    private static void ExpandEncryptionSubkeys(ReadOnlySpan<byte> key, (int Part, int Rotation, bool High)[] layout, Span<ulong> subkeys)
    {
        Span<UInt128> parts = stackalloc UInt128[4];
        try
        {
            DeriveKeyParts(key, parts);
            for (int index = 0; index < layout.Length; index++)
            {
                UInt128 rotated = UInt128.RotateLeft(parts[layout[index].Part], layout[index].Rotation);
                subkeys[index] = (ulong)(layout[index].High ? rotated >> 64 : rotated);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(parts));
        }
    }

    /// <summary>
    /// RFC 3713 section 2.2: KL and KR from the key, then KA from KL and KR through four
    /// F-function rounds, and KB from KA and KR through two more, into
    /// <paramref name="parts" /> at <see cref="KeyLeft" />, <see cref="KeyRight" />,
    /// <see cref="KeyA" /> and <see cref="KeyB" />.
    /// </summary>
    private static void DeriveKeyParts(ReadOnlySpan<byte> key, Span<UInt128> parts)
    {
        parts[KeyLeft] = BinaryPrimitives.ReadUInt128BigEndian(key);
        parts[KeyRight] = ReadKeyRight(key);
        UInt128 mixed = parts[KeyLeft] ^ parts[KeyRight];
        ulong high = (ulong)(mixed >> 64);
        ulong low = (ulong)mixed;
        low ^= Function(high, Sigmas[0]);
        high ^= Function(low, Sigmas[1]);
        high ^= (ulong)(parts[KeyLeft] >> 64);
        low ^= (ulong)parts[KeyLeft];
        low ^= Function(high, Sigmas[2]);
        high ^= Function(low, Sigmas[3]);
        parts[KeyA] = new UInt128(high, low);
        mixed = parts[KeyA] ^ parts[KeyRight];
        high = (ulong)(mixed >> 64);
        low = (ulong)mixed;
        low ^= Function(high, Sigmas[4]);
        high ^= Function(low, Sigmas[5]);
        parts[KeyB] = new UInt128(high, low);
    }

    /// <summary>KR: zero for a 128-bit key, the last 64 bits and their complement for a 192-bit one, the last 128 bits for a 256-bit one.</summary>
    private static UInt128 ReadKeyRight(ReadOnlySpan<byte> key)
    {
        if (key.Length == 16)
        {
            return UInt128.Zero;
        }

        ulong high = BinaryPrimitives.ReadUInt64BigEndian(key[16..]);
        ulong low = key.Length == 24 ? ~high : BinaryPrimitives.ReadUInt64BigEndian(key[24..]);
        return new UInt128(high, low);
    }

    /// <summary>
    /// Decryption runs the rounds with the subkeys in reverse, except that each pair of
    /// whitening keys keeps its order: kw3, kw4 first and kw1, kw2 last.
    /// </summary>
    private static void ReverseForDecryption(ReadOnlySpan<ulong> encryption, Span<ulong> decryption)
    {
        encryption.CopyTo(decryption);
        decryption.Reverse();
        (decryption[0], decryption[1]) = (decryption[1], decryption[0]);
        int last = decryption.Length - 1;
        (decryption[last - 1], decryption[last]) = (decryption[last], decryption[last - 1]);
    }

    /// <summary>RFC 3713 section 2.4.1, the F-function: the key XOR, the S-boxes, then the P-function.</summary>
    internal static ulong Function(ulong input, ulong subkey)
    {
        ulong x = input ^ subkey;
        ReadOnlySpan<byte> box = SubstitutionBox1;
        uint t1 = box[(int)(x >> 56)];
        uint t2 = RotateByteLeft(box[(int)(x >> 48) & 0xFF], 1);
        uint t3 = RotateByteLeft(box[(int)(x >> 40) & 0xFF], 7);
        uint t4 = box[(int)RotateByteLeft((uint)(x >> 32) & 0xFF, 1)];
        uint t5 = RotateByteLeft(box[(int)(x >> 24) & 0xFF], 1);
        uint t6 = RotateByteLeft(box[(int)(x >> 16) & 0xFF], 7);
        uint t7 = box[(int)RotateByteLeft((uint)(x >> 8) & 0xFF, 1)];
        uint t8 = box[(int)(x & 0xFF)];
        ulong y1 = t1 ^ t3 ^ t4 ^ t6 ^ t7 ^ t8;
        ulong y2 = t1 ^ t2 ^ t4 ^ t5 ^ t7 ^ t8;
        ulong y3 = t1 ^ t2 ^ t3 ^ t5 ^ t6 ^ t8;
        ulong y4 = t2 ^ t3 ^ t4 ^ t5 ^ t6 ^ t7;
        ulong y5 = t1 ^ t2 ^ t6 ^ t7 ^ t8;
        ulong y6 = t2 ^ t3 ^ t5 ^ t7 ^ t8;
        ulong y7 = t3 ^ t4 ^ t5 ^ t6 ^ t8;
        ulong y8 = t1 ^ t4 ^ t5 ^ t6 ^ t7;
        return (y1 << 56) | (y2 << 48) | (y3 << 40) | (y4 << 32) | (y5 << 24) | (y6 << 16) | (y7 << 8) | y8;
    }

    /// <summary>RFC 3713 section 2.4.2, the FL-function.</summary>
    internal static ulong FunctionLayer(ulong input, ulong subkey)
    {
        uint x1 = (uint)(input >> 32);
        uint x2 = (uint)input;
        x2 ^= uint.RotateLeft(x1 & (uint)(subkey >> 32), 1);
        x1 ^= x2 | (uint)subkey;
        return ((ulong)x1 << 32) | x2;
    }

    /// <summary>RFC 3713 section 2.4.3, the FLINV-function, the inverse of <see cref="FunctionLayer" />.</summary>
    internal static ulong InverseFunctionLayer(ulong input, ulong subkey)
    {
        uint y1 = (uint)(input >> 32);
        uint y2 = (uint)input;
        y1 ^= y2 | (uint)subkey;
        y2 ^= uint.RotateLeft(y1 & (uint)(subkey >> 32), 1);
        return ((ulong)y1 << 32) | y2;
    }

    private static uint RotateByteLeft(uint value, int count) => ((value << count) | (value >> (8 - count))) & 0xFF;

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"Camellia needs {expected} bytes here; this is {length}.", parameterName);
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

    /// <summary>
    /// RFC 3713 section 2.3: whitening, groups of six Feistel rounds with an FL and FLINV
    /// layer between each group, then the second whitening, with
    /// <paramref name="subkeys" /> in the order this direction uses them.
    /// </summary>
    private void TransformBlock(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<ulong> subkeys)
    {
        ulong left = BinaryPrimitives.ReadUInt64BigEndian(source) ^ subkeys[0];
        ulong right = BinaryPrimitives.ReadUInt64BigEndian(source[8..]) ^ subkeys[1];
        int next = 2;
        for (int group = 0; group < roundGroups; group++)
        {
            if (group > 0)
            {
                left = FunctionLayer(left, subkeys[next]);
                right = InverseFunctionLayer(right, subkeys[next + 1]);
                next += 2;
            }

            for (int round = 0; round < 3; round++)
            {
                right ^= Function(left, subkeys[next]);
                left ^= Function(right, subkeys[next + 1]);
                next += 2;
            }
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, right ^ subkeys[next]);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..], left ^ subkeys[next + 1]);
    }
}
