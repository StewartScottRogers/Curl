using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The Merkle-Damgard construction MD4 and RIPEMD-160 share (RFC 1320 section 3.1 and
/// 3.2): data is buffered into 64-byte blocks for <typeparamref name="TCompression" />,
/// then ended with a <c>0x80</c> byte, zeros up to 56 bytes into a block, and the message
/// length in bits as a little-endian 64-bit number.
/// </summary>
/// <remarks>
/// No branch or index depends on the data, only on its length. <see cref="Clear" /> zeroes
/// the state and the buffered bytes.
/// </remarks>
internal sealed class LittleEndianMerkleDamgard<TCompression>
    where TCompression : ILittleEndianCompressionFunction
{
    /// <summary>The length in bytes of a block.</summary>
    internal const int BlockSize = 64;

    private const int LengthOffset = BlockSize - sizeof(ulong);

    private readonly uint[] state = new uint[TCompression.StateWords];

    private readonly byte[] buffer = new byte[BlockSize];

    private int bufferLength;

    private ulong messageLength;

    /// <summary>Starts an empty message.</summary>
    internal LittleEndianMerkleDamgard()
    {
        TCompression.Initialize(state);
    }

    /// <summary>Adds <paramref name="data" /> to the message.</summary>
    internal void Append(ReadOnlySpan<byte> data)
    {
        messageLength += (ulong)data.Length;
        if (bufferLength > 0)
        {
            int taken = Math.Min(BlockSize - bufferLength, data.Length);
            data[..taken].CopyTo(buffer.AsSpan(bufferLength));
            bufferLength += taken;
            data = data[taken..];
            if (bufferLength < BlockSize)
            {
                return;
            }

            TCompression.Compress(state, buffer);
            bufferLength = 0;
        }

        for (; data.Length >= BlockSize; data = data[BlockSize..])
        {
            TCompression.Compress(state, data[..BlockSize]);
        }

        data.CopyTo(buffer);
        bufferLength = data.Length;
    }

    /// <summary>
    /// Pads the message, writes its digest to <paramref name="destination" />, which is
    /// four bytes per state word, and starts an empty message.
    /// </summary>
    internal void Finish(Span<byte> destination)
    {
        buffer[bufferLength++] = 0x80;
        if (bufferLength > LengthOffset)
        {
            buffer.AsSpan(bufferLength).Clear();
            TCompression.Compress(state, buffer);
            bufferLength = 0;
        }

        buffer.AsSpan(bufferLength, LengthOffset - bufferLength).Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(LengthOffset), messageLength << 3);
        TCompression.Compress(state, buffer);
        for (int word = 0; word < state.Length; word++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[(word * sizeof(uint))..], state[word]);
        }

        Clear();
        TCompression.Initialize(state);
    }

    /// <summary>Zeroes the state, the buffered bytes and the length.</summary>
    internal void Clear()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state.AsSpan()));
        CryptographicOperations.ZeroMemory(buffer);
        bufferLength = 0;
        messageLength = 0;
    }
}
