using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.WireGuard.Messages;

/// <summary>
/// The cookie reply message, type 3, 64 bytes (whitepaper section 5.4.7): the index of the
/// handshake message it answers, a 24-byte nonce and the 16-byte cookie sealed with
/// XChaCha20-Poly1305, its tag appended.
/// </summary>
/// <param name="ReceiverIndex">The sender index of the handshake message this answers.</param>
/// <param name="Nonce">The 24-byte XChaCha20-Poly1305 nonce.</param>
/// <param name="EncryptedCookie">The cookie sealed: 16 bytes of ciphertext then the 16-byte tag.</param>
public sealed record CookieReplyMessage(uint ReceiverIndex, byte[] Nonce, byte[] EncryptedCookie)
{
    /// <summary>The length of the message in bytes.</summary>
    public const int Size = 64;

    /// <summary>Lays the message out as whitepaper section 5.4.7 does.</summary>
    /// <exception cref="ArgumentException">A field is not its fixed length.</exception>
    public byte[] Encode()
    {
        var message = new byte[Size];
        WireGuardMessageLayout.WriteHeader(WireGuardMessageType.CookieReply, message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4, 4), ReceiverIndex);
        WireGuardMessageLayout.WriteField(Nonce, message.AsSpan(8, 24), nameof(Nonce));
        WireGuardMessageLayout.WriteField(EncryptedCookie, message.AsSpan(32, 32), nameof(EncryptedCookie));
        return message;
    }

    /// <summary>Reads a cookie reply; <c>false</c> for any datagram that is not one.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out CookieReplyMessage? message)
    {
        message = null;
        if (!WireGuardMessageLayout.StartsWithHeader(datagram, WireGuardMessageType.CookieReply) || datagram.Length != Size)
        {
            return false;
        }

        message = new CookieReplyMessage(
            BinaryPrimitives.ReadUInt32LittleEndian(datagram[4..8]),
            datagram[8..32].ToArray(),
            datagram[32..64].ToArray());
        return true;
    }
}
