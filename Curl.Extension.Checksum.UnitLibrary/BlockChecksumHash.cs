using System.Buffers.Binary;

namespace Curl.Extension.Checksum;

/// <summary>
/// A Merkle-Damgård hash of FIPS 180-4 (§5.1): it buffers the message into blocks, and pads it with a one bit,
/// zeros and the message's length in bits, big-endian, in the last eighth of a block (§5.1.1, §5.1.2).
/// </summary>
/// <param name="blockSize">The block's size in bytes: 64 for SHA-224 and SHA-256, 128 for the SHA-512 family.</param>
internal abstract class BlockChecksumHash(int blockSize) : IChecksumHash
{
    private readonly byte[] _block = new byte[blockSize];
    private int _buffered;
    private ulong _length;

    public void AppendData(ReadOnlySpan<byte> data)
    {
        _length += (ulong)data.Length;
        Feed(data);
    }

    public byte[] GetHashAndReset()
    {
        var lengthSize = blockSize / 8;
        var tail = new byte[((_buffered + 1 + lengthSize + blockSize - 1) / blockSize * blockSize) - _buffered];
        tail[0] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(tail.AsSpan(tail.Length - 8), _length * 8);
        Feed(tail);
        var digest = Digest();
        _buffered = 0;
        _length = 0;
        ResetState();
        return digest;
    }

    public void Dispose()
    {
    }

    /// <summary>Mixes one full block into the hash state.</summary>
    /// <param name="block">The block, <c>blockSize</c> bytes.</param>
    protected abstract void ProcessBlock(ReadOnlySpan<byte> block);

    /// <summary>Gives the state as the digest, truncated to the algorithm's digest length.</summary>
    /// <returns>The digest.</returns>
    protected abstract byte[] Digest();

    /// <summary>Puts the state back to the algorithm's initial hash value.</summary>
    protected abstract void ResetState();

    private void Feed(ReadOnlySpan<byte> data)
    {
        while (data.Length > 0)
        {
            var count = Math.Min(blockSize - _buffered, data.Length);
            data[..count].CopyTo(_block.AsSpan(_buffered));
            _buffered += count;
            data = data[count..];
            if (_buffered == blockSize)
            {
                ProcessBlock(_block);
                _buffered = 0;
            }
        }
    }
}
