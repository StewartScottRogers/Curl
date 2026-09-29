using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// AES in Counter with CBC-MAC mode (RFC 3610, NIST SP 800-38C), the AEAD of TLS's
/// <c>TLS_AES_128_CCM_SHA256</c>, <c>TLS_AES_128_CCM_8_SHA256</c> and TLS 1.2's
/// <c>*_AES_*_CCM*</c> suites. The block cipher is the BCL's <see cref="Aes" />; this type
/// adds CCM, because the BCL's <c>AesCcm</c> is not supported on macOS (ADR-0118). The
/// parameter order follows <c>AesCcm</c>: a nonce of 7 to 13 bytes, and a tag whose length,
/// an even number from 4 to 16, is the length of the <c>tag</c> span.
/// </summary>
/// <remarks>
/// Constant-time: CBC-MAC and the counter blocks depend only on lengths, the block cipher
/// is the BCL's, and the received tag is compared with
/// <see cref="CryptographicOperations.FixedTimeEquals" />. Every intermediate block is
/// zeroed after use and the key by <see cref="Dispose" />.
/// </remarks>
public sealed class AeadAesCcm : IDisposable
{
    /// <summary>The length in bytes of an AES block.</summary>
    public const int BlockSize = 16;

    /// <summary>The shortest nonce CCM allows, in bytes; it leaves an 8-byte length field.</summary>
    public const int MinimumNonceSize = 7;

    /// <summary>The longest nonce CCM allows, in bytes; it leaves a 2-byte length field.</summary>
    public const int MaximumNonceSize = 13;

    /// <summary>The shortest tag CCM allows, in bytes.</summary>
    public const int MinimumTagSize = 4;

    /// <summary>The longest tag CCM allows, in bytes.</summary>
    public const int MaximumTagSize = 16;

    // RFC 3610 section 2.2: associated data shorter than 2^16 - 2^8 bytes has its length
    // in two bytes; a longer one is marked 0xFF 0xFE and has its length in four.
    private const int ShortAssociatedDataLimit = 0xFF00;

    private readonly Aes aes;

    private bool disposed;

    /// <summary>Keys AES with <paramref name="key" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not 16, 24 or 32 bytes.</exception>
    public AeadAesCcm(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException($"AES needs a key of 16, 24 or 32 bytes; this is {key.Length}.", nameof(key));
        }

        aes = Aes.Create();
        aes.SetKey(key);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext" /> into <paramref name="ciphertext" /> and
    /// writes the tag over <paramref name="associatedData" /> and the plaintext into
    /// <paramref name="tag" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A span has the wrong length, or <paramref name="plaintext" /> is too long for the
    /// length field <paramref name="nonce" /> leaves.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag,
        ReadOnlySpan<byte> associatedData = default)
    {
        RequireUsable(nonce.Length, plaintext.Length, ciphertext.Length, tag.Length);
        ComputeTag(nonce, associatedData, plaintext, tag);
        ApplyKeyStream(nonce, plaintext, ciphertext);
    }

    /// <summary>
    /// Decrypts <paramref name="ciphertext" /> into <paramref name="plaintext" /> and checks
    /// <paramref name="tag" /> over <paramref name="associatedData" /> and that plaintext in
    /// fixed time.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="plaintext" /> all zero, when the tag does not
    /// match (ADR-0118's typed failure); otherwise <c>true</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A span has the wrong length, or <paramref name="ciphertext" /> is too long for the
    /// length field <paramref name="nonce" /> leaves.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public bool TryDecrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext,
        ReadOnlySpan<byte> associatedData = default)
    {
        RequireUsable(nonce.Length, ciphertext.Length, plaintext.Length, tag.Length);
        ApplyKeyStream(nonce, ciphertext, plaintext);
        Span<byte> expectedTag = stackalloc byte[tag.Length];
        try
        {
            ComputeTag(nonce, associatedData, plaintext, expectedTag);
            if (CryptographicOperations.FixedTimeEquals(expectedTag, tag))
            {
                return true;
            }

            CryptographicOperations.ZeroMemory(plaintext);
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedTag);
        }
    }

    /// <summary>Zeroes the key; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        aes.Dispose();
        disposed = true;
    }

    /// <summary>
    /// Writes the flags byte, <paramref name="nonce" /> and <paramref name="value" /> as a
    /// big-endian integer filling the rest of <paramref name="block" />: B0 of the CBC-MAC
    /// and the counter blocks A<sub>i</sub> share this layout (RFC 3610 section 2.2 and 2.3).
    /// </summary>
    internal static void FormatBlock(byte flags, ReadOnlySpan<byte> nonce, long value, Span<byte> block)
    {
        block[0] = flags;
        nonce.CopyTo(block[1..]);
        int lengthFieldSize = BlockSize - 1 - nonce.Length;
        for (int index = 0; index < lengthFieldSize; index++)
        {
            block[BlockSize - 1 - index] = (byte)(value >> (8 * index));
        }
    }

    private static void RequireNonceSize(int nonceLength)
    {
        if (nonceLength is < MinimumNonceSize or > MaximumNonceSize)
        {
            throw new ArgumentException($"AES-CCM needs a nonce of {MinimumNonceSize} to {MaximumNonceSize} bytes; this is {nonceLength}.", "nonce");
        }
    }

    private static void RequireTagSize(int tagLength)
    {
        if (tagLength is < MinimumTagSize or > MaximumTagSize || tagLength % 2 != 0)
        {
            throw new ArgumentException($"AES-CCM needs a tag of an even {MinimumTagSize} to {MaximumTagSize} bytes; this is {tagLength}.", "tag");
        }
    }

    private static void RequireMessageLength(int nonceLength, int sourceLength, int destinationLength)
    {
        if (destinationLength != sourceLength)
        {
            throw new ArgumentException($"AES-CCM needs a destination as long as the source, {sourceLength} bytes; this is {destinationLength}.", "destination");
        }

        int lengthFieldSize = BlockSize - 1 - nonceLength;
        if (lengthFieldSize < sizeof(int) && sourceLength >> (8 * lengthFieldSize) != 0)
        {
            throw new ArgumentException($"A {nonceLength}-byte AES-CCM nonce leaves a {lengthFieldSize}-byte length field, too short for {sourceLength} bytes.", "source");
        }
    }

    private void RequireUsable(int nonceLength, int sourceLength, int destinationLength, int tagLength)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireNonceSize(nonceLength);
        RequireTagSize(tagLength);
        RequireMessageLength(nonceLength, sourceLength, destinationLength);
    }

    /// <summary>
    /// Computes CCM's CBC-MAC over B0, the length-prefixed <paramref name="associatedData" />
    /// and <paramref name="plaintext" />, and writes it encrypted with counter block A0 into
    /// <paramref name="tag" /> (RFC 3610 section 2.2 and 2.3).
    /// </summary>
    private void ComputeTag(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext, Span<byte> tag)
    {
        Span<byte> mac = stackalloc byte[BlockSize];
        Span<byte> block = stackalloc byte[BlockSize];
        Span<byte> keyStream = stackalloc byte[BlockSize];
        try
        {
            int associatedDataFlag = associatedData.IsEmpty ? 0 : 0x40;
            byte flags = (byte)(associatedDataFlag | (((tag.Length - 2) / 2) << 3) | (BlockSize - 2 - nonce.Length));
            FormatBlock(flags, nonce, plaintext.Length, block);
            aes.EncryptEcb(block, mac, PaddingMode.None);
            if (!associatedData.IsEmpty)
            {
                AbsorbAssociatedData(associatedData, mac, block);
            }

            Absorb(plaintext, mac, block);
            FormatBlock((byte)(BlockSize - 2 - nonce.Length), nonce, 0, block);
            aes.EncryptEcb(block, keyStream, PaddingMode.None);
            for (int index = 0; index < tag.Length; index++)
            {
                tag[index] = (byte)(mac[index] ^ keyStream[index]);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mac);
            CryptographicOperations.ZeroMemory(block);
            CryptographicOperations.ZeroMemory(keyStream);
        }
    }

    /// <summary>Absorbs the length prefix and <paramref name="associatedData" />, zero-padded to whole blocks.</summary>
    private void AbsorbAssociatedData(ReadOnlySpan<byte> associatedData, Span<byte> mac, Span<byte> block)
    {
        block.Clear();
        int prefixLength = 2;
        if (associatedData.Length < ShortAssociatedDataLimit)
        {
            block[0] = (byte)(associatedData.Length >> 8);
            block[1] = (byte)associatedData.Length;
        }
        else
        {
            block[0] = 0xFF;
            block[1] = 0xFE;
            BinaryPrimitives.WriteInt32BigEndian(block[2..], associatedData.Length);
            prefixLength = 6;
        }

        int firstLength = Math.Min(associatedData.Length, BlockSize - prefixLength);
        associatedData[..firstLength].CopyTo(block[prefixLength..]);
        Absorb(block, mac, block);
        Absorb(associatedData[firstLength..], mac, block);
    }

    /// <summary>
    /// Runs CBC-MAC over <paramref name="data" />, its last block zero-padded, updating
    /// <paramref name="mac" />; <paramref name="scratch" /> is a block of working space.
    /// </summary>
    private void Absorb(ReadOnlySpan<byte> data, Span<byte> mac, Span<byte> scratch)
    {
        for (int offset = 0; offset < data.Length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, data.Length - offset);
            for (int index = 0; index < count; index++)
            {
                mac[index] ^= data[offset + index];
            }

            mac.CopyTo(scratch);
            aes.EncryptEcb(scratch, mac, PaddingMode.None);
        }
    }

    /// <summary>
    /// Exclusive-ors <paramref name="source" /> with the keystream of counter blocks A1,
    /// A2, ... into <paramref name="destination" /> (RFC 3610 section 2.3).
    /// </summary>
    private void ApplyKeyStream(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Span<byte> counter = stackalloc byte[BlockSize];
        Span<byte> keyStream = stackalloc byte[BlockSize];
        try
        {
            byte flags = (byte)(BlockSize - 2 - nonce.Length);
            long blockNumber = 1;
            for (int offset = 0; offset < source.Length; offset += BlockSize, blockNumber++)
            {
                FormatBlock(flags, nonce, blockNumber, counter);
                aes.EncryptEcb(counter, keyStream, PaddingMode.None);
                int count = Math.Min(BlockSize, source.Length - offset);
                for (int index = 0; index < count; index++)
                {
                    destination[offset + index] = (byte)(source[offset + index] ^ keyStream[index]);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(counter);
            CryptographicOperations.ZeroMemory(keyStream);
        }
    }
}
