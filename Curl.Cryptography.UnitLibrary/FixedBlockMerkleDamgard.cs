using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The SHA family's Merkle-Damgard construction (FIPS 180-4 section 5.1) over a message
/// whose length is secret within a public range: <c>prefix || header || data[..dataLength]</c>
/// with <c>dataLength</c> anywhere from <c>minimumDataLength</c> to <c>data.Length</c>. The
/// compression function runs over the same number of blocks for every
/// <c>dataLength</c>, as OpenSSL's <c>ssl3_cbc_digest_record</c> does.
/// </summary>
/// <remarks>
/// The blocks every allowed length fills with data are hashed as they are. Each block after
/// them is built byte by byte with masks - the data, the <c>0x80</c> byte, zeros, and the
/// bit length in the block the true length ends in - and all of them are compressed; the
/// state after the true last block is picked out with a mask. No branch, loop bound or
/// index depends on <c>dataLength</c> or on the bytes, only on the public lengths. Every
/// temporary is zeroed.
/// </remarks>
internal static class FixedBlockMerkleDamgard<TCompression>
    where TCompression : struct, IBigEndianCompressionFunction
{
    /// <summary>
    /// Writes the hash of <c><paramref name="prefixBlocks" /> || <paramref name="header" /> ||
    /// <paramref name="data" />[..<paramref name="dataLength" />]</c> to the first
    /// <c>HashSize</c> bytes of <paramref name="destination" />.
    /// </summary>
    /// <param name="prefixBlocks">Whole blocks hashed first; the HMAC pad.</param>
    /// <param name="header">Bytes hashed before the data; may be secret, its length is not.</param>
    /// <param name="data">The data at its longest.</param>
    /// <param name="dataLength">The secret length of the data hashed, <paramref name="minimumDataLength" /> to <c>data.Length</c>.</param>
    /// <param name="minimumDataLength">The public least <paramref name="dataLength" /> can be.</param>
    /// <param name="destination">Receives the hash.</param>
    internal static void Hash(ReadOnlySpan<byte> prefixBlocks, ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int dataLength, int minimumDataLength, Span<byte> destination)
    {
        int blockSize = TCompression.BlockSize;
        Span<ulong> state = stackalloc ulong[TCompression.StateWords];
        Span<ulong> result = stackalloc ulong[TCompression.StateWords];
        Span<byte> block = stackalloc byte[blockSize];
        try
        {
            TCompression.Initialize(state);
            for (int offset = 0; offset < prefixBlocks.Length; offset += blockSize)
            {
                TCompression.Compress(state, prefixBlocks.Slice(offset, blockSize));
            }

            int publicBlocks = (header.Length + minimumDataLength) / blockSize;
            for (int index = 0; index < publicBlocks; index++)
            {
                FillPublicBlock(header, data, index * blockSize, block);
                TCompression.Compress(state, block);
            }

            uint messageLength = (uint)(header.Length + dataLength);
            ulong bitLength = ((ulong)prefixBlocks.Length + messageLength) << 3;
            HashVariableBlocks(state, result, block, header, data, publicBlocks, messageLength, bitLength);
            TCompression.WriteDigest(result, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state));
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(result));
            CryptographicOperations.ZeroMemory(block);
        }
    }

    /// <summary>
    /// Compresses every block from <paramref name="firstBlock" /> to the last one the longest
    /// message needs, and copies into <paramref name="result" /> the state after the block
    /// the <paramref name="messageLength" />-byte message ends in.
    /// </summary>
    private static void HashVariableBlocks(Span<ulong> state, Span<ulong> result, Span<byte> block, ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int firstBlock, uint messageLength, ulong bitLength)
    {
        int shift = BitOperations.Log2((uint)TCompression.BlockSize);
        uint finalBlock = (messageLength + (uint)TCompression.LengthFieldSize) >> shift;
        int lastBlock = (header.Length + data.Length + TCompression.LengthFieldSize) >> shift;
        for (int index = firstBlock; index <= lastBlock; index++)
        {
            uint isFinal = ConstantTime.EqualMask((uint)index, finalBlock);
            FillVariableBlock(header, data, index * TCompression.BlockSize, messageLength, isFinal, bitLength, block);
            TCompression.Compress(state, block);
            ulong wideMask = (ulong)(long)(int)isFinal;
            for (int word = 0; word < state.Length; word++)
            {
                result[word] = (state[word] & wideMask) | (result[word] & ~wideMask);
            }
        }
    }

    /// <summary>Copies the block at <paramref name="offset" /> of a message every allowed length fills.</summary>
    private static void FillPublicBlock(ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int offset, Span<byte> block)
    {
        for (int position = 0; position < block.Length; position++)
        {
            block[position] = MessageByte(header, data, offset + position);
        }
    }

    /// <summary>
    /// Builds the block at <paramref name="offset" /> with masks: message bytes before
    /// <paramref name="messageLength" />, <c>0x80</c> at it, zeros after, and the bit length
    /// in its last eight bytes when <paramref name="isFinal" /> is all ones.
    /// </summary>
    private static void FillVariableBlock(ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int offset, uint messageLength, uint isFinal, ulong bitLength, Span<byte> block)
    {
        int longestMessage = header.Length + data.Length;
        for (int position = 0; position < block.Length; position++)
        {
            int index = offset + position;
            uint value = index < longestMessage ? MessageByte(header, data, index) : 0u;
            int fromEnd = block.Length - 1 - position;
            uint lengthByte = fromEnd < sizeof(ulong) ? (uint)(byte)(bitLength >> (fromEnd * 8)) : 0u;
            block[position] = (byte)(
                (value & ConstantTime.LessThanMask((uint)index, messageLength))
                | (0x80u & ConstantTime.EqualMask((uint)index, messageLength))
                | (lengthByte & isFinal));
        }
    }

    /// <summary>The byte at the public <paramref name="index" /> of <c>header || data</c>.</summary>
    private static byte MessageByte(ReadOnlySpan<byte> header, ReadOnlySpan<byte> data, int index) =>
        index < header.Length ? header[index] : data[index - header.Length];
}
