using System.Buffers.Binary;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// A cipher with a separate MAC, in either order: MAC-then-encrypt as RFC 4253 section 6
/// defines it (the MAC covers the unencrypted packet and the whole packet, length
/// included, is encrypted), or encrypt-then-MAC as OpenSSH's <c>-etm@openssh.com</c> MACs
/// define it (the length travels unencrypted and the MAC covers it and the ciphertext).
/// The MAC is checked before anything is decrypted under encrypt-then-MAC.
/// </summary>
/// <param name="cipher">The direction's cipher.</param>
/// <param name="mac">The direction's MAC.</param>
internal sealed class CipherAndMacPacketProtection(ISshCipher cipher, SshMac mac) : ISshPacketProtection
{
    /// <inheritdoc />
    public int BlockSize => Math.Max(8, cipher.BlockSize);

    /// <inheritdoc />
    public bool PadsPacketLengthField => !mac.IsEncryptThenMac;

    /// <inheritdoc />
    public int LengthBlockLength => mac.IsEncryptThenMac ? sizeof(uint) : cipher.BlockSize;

    /// <inheritdoc />
    public int TagLength => mac.Length;

    /// <inheritdoc />
    public byte[] Seal(uint sequenceNumber, ReadOnlySpan<byte> packet)
    {
        byte[] sealedPacket = new byte[packet.Length + mac.Length];
        Span<byte> body = sealedPacket.AsSpan(0, packet.Length);
        if (mac.IsEncryptThenMac)
        {
            packet[..sizeof(uint)].CopyTo(body);
            cipher.Transform(packet[sizeof(uint)..], body[sizeof(uint)..]);
            mac.Compute(sequenceNumber, body, []).CopyTo(sealedPacket, packet.Length);
        }
        else
        {
            mac.Compute(sequenceNumber, packet, []).CopyTo(sealedPacket, packet.Length);
            cipher.Transform(packet, body);
        }

        return sealedPacket;
    }

    /// <inheritdoc />
    public uint DecryptPacketLength(uint sequenceNumber, byte[] lengthBlock)
    {
        if (!mac.IsEncryptThenMac)
        {
            cipher.Transform(lengthBlock, lengthBlock);
        }

        return BinaryPrimitives.ReadUInt32BigEndian(lengthBlock);
    }

    /// <inheritdoc />
    public byte[] Open(uint sequenceNumber, byte[] lengthBlock, byte[] remainder)
    {
        ReadOnlySpan<byte> received = remainder.AsSpan(remainder.Length - mac.Length);
        Span<byte> rest = remainder.AsSpan(0, remainder.Length - mac.Length);
        if (mac.IsEncryptThenMac)
        {
            mac.Verify(sequenceNumber, lengthBlock, rest, received);
            cipher.Transform(rest, rest);
        }
        else
        {
            cipher.Transform(rest, rest);
            mac.Verify(sequenceNumber, lengthBlock, rest, received);
        }

        return [.. lengthBlock.AsSpan(sizeof(uint)), .. rest];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        cipher.Dispose();
        mac.Dispose();
    }
}
