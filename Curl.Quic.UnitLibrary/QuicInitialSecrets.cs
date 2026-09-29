using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// The Initial secrets of QUIC version 1 (RFC 9001 section 5.2): HKDF-Extract of the
/// client's first Destination Connection ID under the version 1 salt, then
/// HKDF-Expand-Label with <c>client in</c> and <c>server in</c>, always with SHA-256.
/// Initial packets are protected with <c>AEAD_AES_128_GCM</c> under these secrets.
/// </summary>
public static class QuicInitialSecrets
{
    /// <summary>Gets the cipher suite whose AEAD, header protection and hash protect Initial packets.</summary>
    public static Tls13CipherSuite CipherSuite => Tls13CipherSuite.Aes128GcmSha256;

    /// <summary>Gets the QUIC version 1 <c>initial_salt</c>.</summary>
    public static ReadOnlySpan<byte> InitialSalt =>
    [
        0x38, 0x76, 0x2c, 0xf7, 0xf5, 0x59, 0x34, 0xb3, 0x4d, 0x17,
        0x9a, 0xe6, 0xa4, 0xc8, 0x0c, 0xad, 0xcc, 0xbb, 0x7f, 0x0a,
    ];

    /// <summary>Returns <c>initial_secret</c>, HKDF-Extract(initial_salt, <paramref name="destinationConnectionId" />).</summary>
    /// <param name="destinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The 32-byte initial secret both directions derive from.</returns>
    public static byte[] DeriveInitialSecret(ReadOnlySpan<byte> destinationConnectionId) =>
        CipherSuite.KeySchedule.Extract(InitialSalt.ToArray(), destinationConnectionId.ToArray());

    /// <summary>Returns <c>client_initial_secret</c>, which protects the client's Initial packets.</summary>
    /// <param name="destinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The 32-byte secret.</returns>
    public static byte[] DeriveClientInitialSecret(ReadOnlySpan<byte> destinationConnectionId) =>
        Expand(destinationConnectionId, "client in");

    /// <summary>Returns <c>server_initial_secret</c>, which protects the server's Initial packets.</summary>
    /// <param name="destinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The 32-byte secret.</returns>
    public static byte[] DeriveServerInitialSecret(ReadOnlySpan<byte> destinationConnectionId) =>
        Expand(destinationConnectionId, "server in");

    private static byte[] Expand(ReadOnlySpan<byte> destinationConnectionId, string label)
    {
        Tls13KeySchedule keySchedule = CipherSuite.KeySchedule;
        return keySchedule.ExpandLabel(DeriveInitialSecret(destinationConnectionId), label, [], keySchedule.HashLength);
    }
}
