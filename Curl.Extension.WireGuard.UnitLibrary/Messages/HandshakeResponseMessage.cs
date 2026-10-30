using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.WireGuard.Messages;

/// <summary>
/// The handshake response message, type 2, 92 bytes (whitepaper section 5.4.3): the
/// responder's index, the initiator's index it answers, the responder's ephemeral public key,
/// the empty plaintext sealed (its 16-byte tag), then mac1 and mac2.
/// </summary>
/// <param name="SenderIndex">The index the responder picked.</param>
/// <param name="ReceiverIndex">The initiator's index from the initiation this answers.</param>
/// <param name="UnencryptedEphemeral">The responder's 32-byte ephemeral public key.</param>
/// <param name="EncryptedNothing">The empty plaintext sealed: its 16-byte tag.</param>
/// <param name="Mac1">The 16-byte mac1.</param>
/// <param name="Mac2">The 16-byte mac2, all zero without a cookie.</param>
public sealed record HandshakeResponseMessage(
    uint SenderIndex,
    uint ReceiverIndex,
    byte[] UnencryptedEphemeral,
    byte[] EncryptedNothing,
    byte[] Mac1,
    byte[] Mac2)
{
    /// <summary>The length of the message in bytes.</summary>
    public const int Size = 92;

    /// <summary>Lays the message out as whitepaper section 5.4.3 does.</summary>
    /// <exception cref="ArgumentException">A field is not its fixed length.</exception>
    public byte[] Encode()
    {
        var message = new byte[Size];
        WireGuardMessageLayout.WriteHeader(WireGuardMessageType.HandshakeResponse, message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4, 4), SenderIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(8, 4), ReceiverIndex);
        WireGuardMessageLayout.WriteField(UnencryptedEphemeral, message.AsSpan(12, 32), nameof(UnencryptedEphemeral));
        WireGuardMessageLayout.WriteField(EncryptedNothing, message.AsSpan(44, 16), nameof(EncryptedNothing));
        WireGuardMessageLayout.WriteField(Mac1, message.AsSpan(60, 16), nameof(Mac1));
        WireGuardMessageLayout.WriteField(Mac2, message.AsSpan(76, 16), nameof(Mac2));
        return message;
    }

    /// <summary>Reads a handshake response; <c>false</c> for any datagram that is not one.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out HandshakeResponseMessage? message)
    {
        message = null;
        if (!WireGuardMessageLayout.StartsWithHeader(datagram, WireGuardMessageType.HandshakeResponse) || datagram.Length != Size)
        {
            return false;
        }

        message = new HandshakeResponseMessage(
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[4..8]),
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[8..12]),
            datagram[12..44].ToArray(),
            datagram[44..60].ToArray(),
            datagram[60..76].ToArray(),
            datagram[76..92].ToArray());
        return true;
    }
}
