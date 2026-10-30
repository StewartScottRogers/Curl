using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.WireGuard.Messages;

/// <summary>
/// The handshake initiation message, type 1, 148 bytes (whitepaper section 5.4.2): the
/// initiator's index, its ephemeral public key, its static public key and a TAI64N timestamp
/// each sealed with a 16-byte tag, then mac1 and mac2.
/// </summary>
/// <param name="SenderIndex">The index the initiator picked for this handshake.</param>
/// <param name="UnencryptedEphemeral">The initiator's 32-byte ephemeral public key.</param>
/// <param name="EncryptedStatic">The initiator's static public key, sealed: 48 bytes.</param>
/// <param name="EncryptedTimestamp">The TAI64N timestamp, sealed: 28 bytes.</param>
/// <param name="Mac1">The 16-byte mac1.</param>
/// <param name="Mac2">The 16-byte mac2, all zero without a cookie.</param>
public sealed record HandshakeInitiationMessage(
    uint SenderIndex,
    byte[] UnencryptedEphemeral,
    byte[] EncryptedStatic,
    byte[] EncryptedTimestamp,
    byte[] Mac1,
    byte[] Mac2)
{
    /// <summary>The length of the message in bytes.</summary>
    public const int Size = 148;

    /// <summary>Lays the message out as whitepaper section 5.4.2 does.</summary>
    /// <exception cref="ArgumentException">A field is not its fixed length.</exception>
    public byte[] Encode()
    {
        var message = new byte[Size];
        WireGuardMessageLayout.WriteHeader(WireGuardMessageType.HandshakeInitiation, message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4, 4), SenderIndex);
        WireGuardMessageLayout.WriteField(UnencryptedEphemeral, message.AsSpan(8, 32), nameof(UnencryptedEphemeral));
        WireGuardMessageLayout.WriteField(EncryptedStatic, message.AsSpan(40, 48), nameof(EncryptedStatic));
        WireGuardMessageLayout.WriteField(EncryptedTimestamp, message.AsSpan(88, 28), nameof(EncryptedTimestamp));
        WireGuardMessageLayout.WriteField(Mac1, message.AsSpan(116, 16), nameof(Mac1));
        WireGuardMessageLayout.WriteField(Mac2, message.AsSpan(132, 16), nameof(Mac2));
        return message;
    }

    /// <summary>Reads a handshake initiation; <c>false</c> for any datagram that is not one.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out HandshakeInitiationMessage? message)
    {
        message = null;
        if (!WireGuardMessageLayout.StartsWithHeader(datagram, WireGuardMessageType.HandshakeInitiation) || datagram.Length != Size)
        {
            return false;
        }

        message = new HandshakeInitiationMessage(
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[4..8]),
            datagram[8..40].ToArray(),
            datagram[40..88].ToArray(),
            datagram[88..116].ToArray(),
            datagram[116..132].ToArray(),
            datagram[132..148].ToArray());
        return true;
    }
}
