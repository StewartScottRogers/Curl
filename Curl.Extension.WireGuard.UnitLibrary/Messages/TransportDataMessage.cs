using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.WireGuard.Messages;

/// <summary>
/// The transport data message, type 4 (whitepaper section 5.4.6): the receiver's index, the
/// 64-bit little-endian counter that is the nonce, and the sealed, padded packet with its
/// 16-byte tag. A keepalive seals an empty packet, so the shortest message is 32 bytes.
/// </summary>
/// <param name="ReceiverIndex">The index the receiving peer picked in the handshake.</param>
/// <param name="Counter">The sender's message counter, the nonce.</param>
/// <param name="EncryptedPacket">The sealed packet, tag included: at least 16 bytes.</param>
public sealed record TransportDataMessage(uint ReceiverIndex, ulong Counter, byte[] EncryptedPacket)
{
    /// <summary>The length of the header, index and counter before the sealed packet.</summary>
    public const int HeaderSize = 16;

    /// <summary>The length of the shortest message: a sealed empty packet.</summary>
    public const int MinimumSize = HeaderSize + 16;

    /// <summary>Lays the message out as whitepaper section 5.4.6 does.</summary>
    public byte[] Encode()
    {
        var message = new byte[HeaderSize + EncryptedPacket.Length];
        WireGuardMessageLayout.WriteHeader(WireGuardMessageType.TransportData, message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4, 4), ReceiverIndex);
        BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(8, 8), Counter);
        EncryptedPacket.CopyTo(message, HeaderSize);
        return message;
    }

    /// <summary>Reads a transport data message; <c>false</c> for any datagram that is not one.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out TransportDataMessage? message)
    {
        message = null;
        if (!WireGuardMessageLayout.StartsWithHeader(datagram, WireGuardMessageType.TransportData) || datagram.Length < MinimumSize)
        {
            return false;
        }

        message = new TransportDataMessage(
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[4..8]),
            BinaryPrimitives.ReadUInt64LittleEndian(datagram[8..16]),
            datagram[HeaderSize..].ToArray());
        return true;
    }
}
