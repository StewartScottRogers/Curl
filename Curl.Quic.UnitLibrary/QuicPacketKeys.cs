using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// The keys one packet protection secret gives (RFC 9001 section 5.1): the AEAD key
/// (<c>quic key</c>), the IV (<c>quic iv</c>) and the header protection key
/// (<c>quic hp</c>), each HKDF-Expand-Label of the secret with the suite's hash.
/// </summary>
/// <param name="Key">The AEAD key, as long as the suite's key.</param>
/// <param name="Iv">The 12-byte IV each packet's nonce is built from.</param>
/// <param name="HeaderProtectionKey">The header protection key, as long as the AEAD key.</param>
public sealed record QuicPacketKeys(byte[] Key, byte[] Iv, byte[] HeaderProtectionKey)
{
    /// <summary>The length of every AEAD IV and nonce QUIC uses.</summary>
    public const int IvLength = 12;

    /// <summary>Derives the keys of <paramref name="secret" />.</summary>
    /// <param name="cipherSuite">The suite the handshake negotiated, or <see cref="QuicInitialSecrets.CipherSuite" /> for Initial packets.</param>
    /// <param name="secret">The packet protection secret.</param>
    /// <returns>The key, IV and header protection key.</returns>
    public static QuicPacketKeys Derive(Tls13CipherSuite cipherSuite, byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(cipherSuite);
        ArgumentNullException.ThrowIfNull(secret);
        Tls13KeySchedule keySchedule = cipherSuite.KeySchedule;
        return new QuicPacketKeys(
            keySchedule.ExpandLabel(secret, "quic key", [], cipherSuite.KeyLength),
            keySchedule.ExpandLabel(secret, "quic iv", [], IvLength),
            keySchedule.ExpandLabel(secret, "quic hp", [], cipherSuite.KeyLength));
    }

    /// <summary>
    /// Returns the secret of the next key phase (RFC 9001 section 6.1): HKDF-Expand-Label
    /// of <paramref name="secret" /> with <c>quic ku</c>, as long as the hash. The header
    /// protection key does not change with it.
    /// </summary>
    /// <param name="cipherSuite">The negotiated suite.</param>
    /// <param name="secret">The current 1-RTT secret.</param>
    /// <returns>The next-generation secret.</returns>
    public static byte[] DeriveNextSecret(Tls13CipherSuite cipherSuite, byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(cipherSuite);
        ArgumentNullException.ThrowIfNull(secret);
        Tls13KeySchedule keySchedule = cipherSuite.KeySchedule;
        return keySchedule.ExpandLabel(secret, "quic ku", [], keySchedule.HashLength);
    }
}
