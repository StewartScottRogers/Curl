using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Extension.WireGuard.Messages;

/// <summary>
/// The mac1 and mac2 fields that end both handshake messages (whitepaper section 5.4.4) and
/// the key that seals a cookie reply (section 5.4.7). mac1 is MAC(HASH(LABEL_MAC1 || Spub_m'),
/// msg_alpha) and mac2 is MAC(cookie, msg_beta), where MAC is keyed BLAKE2s with a 16-byte
/// output, msg_alpha the message up to mac1 and msg_beta the message up to mac2.
/// </summary>
public static class WireGuardMessageAuthentication
{
    /// <summary>The length of mac1, mac2 and a cookie.</summary>
    public const int MacSize = 16;

    /// <summary>The length of a key derived from a public key.</summary>
    public const int KeySize = Blake2s.HashSize;

    /// <summary>
    /// Writes HASH(LABEL_MAC1 || <paramref name="receiverPublicKey" />), the mac1 key of a
    /// message sent to the holder of that static public key.
    /// </summary>
    public static void DeriveMac1Key(ReadOnlySpan<byte> receiverPublicKey, Span<byte> key) =>
        HashLabelled("mac1----"u8, receiverPublicKey, key);

    /// <summary>
    /// Writes HASH(LABEL_COOKIE || <paramref name="senderPublicKey" />), the key that seals a
    /// cookie reply sent by the holder of that static public key.
    /// </summary>
    public static void DeriveCookieKey(ReadOnlySpan<byte> senderPublicKey, Span<byte> key) =>
        HashLabelled("cookie--"u8, senderPublicKey, key);

    /// <summary>Writes mac1 over the bytes before it, 32 bytes from the end of <paramref name="message" />.</summary>
    public static void WriteMac1(Span<byte> message, ReadOnlySpan<byte> mac1Key) =>
        Blake2s.HashData(mac1Key, message[..^(2 * MacSize)], message[^(2 * MacSize)..^MacSize]);

    /// <summary>Writes mac2 over the bytes before it, the last 16 bytes of <paramref name="message" />.</summary>
    public static void WriteMac2(Span<byte> message, ReadOnlySpan<byte> cookie) =>
        Blake2s.HashData(cookie, message[..^MacSize], message[^MacSize..]);

    /// <summary>Whether the message's mac1 matches the one <paramref name="mac1Key" /> gives, compared in fixed time.</summary>
    public static bool HasValidMac1(ReadOnlySpan<byte> message, ReadOnlySpan<byte> mac1Key)
    {
        Span<byte> expected = stackalloc byte[MacSize];
        Blake2s.HashData(mac1Key, message[..^(2 * MacSize)], expected);
        return CryptographicOperations.FixedTimeEquals(expected, message[^(2 * MacSize)..^MacSize]);
    }

    /// <summary>Whether the message's mac2 matches the one <paramref name="cookie" /> gives, compared in fixed time.</summary>
    public static bool HasValidMac2(ReadOnlySpan<byte> message, ReadOnlySpan<byte> cookie)
    {
        Span<byte> expected = stackalloc byte[MacSize];
        Blake2s.HashData(cookie, message[..^MacSize], expected);
        return CryptographicOperations.FixedTimeEquals(expected, message[^MacSize..]);
    }

    private static void HashLabelled(ReadOnlySpan<byte> label, ReadOnlySpan<byte> publicKey, Span<byte> key)
    {
        using var hash = new Blake2s();
        hash.AppendData(label);
        hash.AppendData(publicKey);
        hash.GetHashAndReset(key);
    }
}
