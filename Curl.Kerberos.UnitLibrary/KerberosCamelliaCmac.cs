using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// CMAC (NIST SP 800-38B) with Camellia as the block cipher and the full 128-bit tag, the
/// checksum and key-derivation PRF of RFC 6803's <c>camellia*-cts-cmac</c> types.
/// </summary>
internal static class KerberosCamelliaCmac
{
    private const int BlockSize = Camellia.BlockSize;

    // SP 800-38B section 5.3, R128: the low byte of the reduction constant for 128-bit blocks.
    private const byte Reduction = 0x87;

    /// <summary>Computes the CMAC of <paramref name="message" /> under <paramref name="key" />.</summary>
    /// <returns>The 16-byte tag.</returns>
    public static byte[] Compute(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message)
    {
        using Camellia cipher = new(key);
        Span<byte> buffers = stackalloc byte[3 * BlockSize];
        Span<byte> chain = buffers[..BlockSize];
        Span<byte> subkey = buffers[BlockSize..(2 * BlockSize)];
        Span<byte> last = buffers[(2 * BlockSize)..];
        try
        {
            buffers.Clear();

            // SP 800-38B section 6.2: M1 to Mn-1 are CBC-MACed whole; Mn is exclusive-ored
            // with K1 when it is a whole block and, padded with 10*, with K2 when it is not
            // (an empty message is one empty, padded block: C#'s remainder keeps the
            // dividend's sign, so its last length is (-1 % 16) + 1 = 0).
            int lastLength = ((message.Length - 1) % BlockSize) + 1;
            int headLength = message.Length - lastLength;
            cipher.EncryptBlock(chain, subkey);
            DoubleInPlace(subkey);
            if (lastLength < BlockSize)
            {
                DoubleInPlace(subkey);
                last[lastLength] = 0x80;
            }

            message[headLength..].CopyTo(last);
            for (int offset = 0; offset < headLength; offset += BlockSize)
            {
                ExclusiveOr(chain, message.Slice(offset, BlockSize));
                cipher.EncryptBlock(chain, chain);
            }

            ExclusiveOr(last, subkey);
            ExclusiveOr(chain, last);
            cipher.EncryptBlock(chain, chain);
            return chain.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffers);
        }
    }

    /// <summary>SP 800-38B section 6.1: the block shifted left one bit, exclusive-ored with R128 when its high bit was set.</summary>
    private static void DoubleInPlace(Span<byte> block)
    {
        UInt128 value = BinaryPrimitives.ReadUInt128BigEndian(block);
        UInt128 doubled = (value << 1) ^ (Reduction * (value >> 127));
        BinaryPrimitives.WriteUInt128BigEndian(block, doubled);
    }

    private static void ExclusiveOr(Span<byte> destination, ReadOnlySpan<byte> other)
    {
        for (int index = 0; index < BlockSize; index++)
        {
            destination[index] ^= other[index];
        }
    }
}
