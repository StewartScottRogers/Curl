using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>An X25519 key share (RFC 7748, RFC 8446 section 7.4.2): 32-byte public values.</summary>
public sealed class X25519KeyShare : Tls13KeyShare
{
    private readonly byte[] privateKey;

    /// <summary>Creates the share of <paramref name="privateKey" />.</summary>
    /// <param name="privateKey">The 32-byte private scalar.</param>
    /// <exception cref="ArgumentException">The key is not 32 bytes.</exception>
    public X25519KeyShare(byte[] privateKey)
        : base(TlsNamedGroup.X25519, PublicKeyOf(privateKey)) => this.privateKey = [.. privateKey];

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        byte[] sharedSecret = new byte[X25519.KeySize];
        return peerPublicKey.Length == X25519.KeySize && X25519.TryComputeSharedSecret(privateKey, peerPublicKey, sharedSecret)
            ? sharedSecret
            : null;
    }

    private static byte[] PublicKeyOf(byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        byte[] publicKey = new byte[X25519.KeySize];
        X25519.ComputePublicKey(privateKey, publicKey);
        return publicKey;
    }
}
