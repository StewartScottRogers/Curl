using System.Buffers.Binary;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// The <c>none</c> cipher with no MAC that both directions use until the first
/// <c>NEWKEYS</c> (RFC 4253 section 6): packets travel as framed, padded to 8 bytes.
/// </summary>
internal sealed class SshPlainPacketProtection : ISshPacketProtection
{
    /// <inheritdoc />
    public int BlockSize => 8;

    /// <inheritdoc />
    public bool PadsPacketLengthField => true;

    /// <inheritdoc />
    public int LengthBlockLength => sizeof(uint);

    /// <inheritdoc />
    public int TagLength => 0;

    /// <inheritdoc />
    public byte[] Seal(uint sequenceNumber, ReadOnlySpan<byte> packet) => packet.ToArray();

    /// <inheritdoc />
    public uint DecryptPacketLength(uint sequenceNumber, byte[] lengthBlock) =>
        BinaryPrimitives.ReadUInt32BigEndian(lengthBlock);

    /// <inheritdoc />
    public byte[] Open(uint sequenceNumber, byte[] lengthBlock, byte[] remainder) => remainder;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
