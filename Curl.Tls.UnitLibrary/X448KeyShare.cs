using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>An x448 key share (RFC 7748, RFC 8446 section 7.4.2): 56-byte public values.</summary>
public sealed class X448KeyShare : Tls13KeyShare
{
    private readonly byte[] privateKey;

    /// <summary>Creates the share of <paramref name="privateKey" />.</summary>
    /// <param name="privateKey">The 56-byte private scalar.</param>
    /// <exception cref="ArgumentException">The key is not 56 bytes.</exception>
    public X448KeyShare(byte[] privateKey)
        : base(TlsNamedGroup.X448, PublicKeyOf(privateKey)) => this.privateKey = [.. privateKey];

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        byte[] sharedSecret = new byte[X448.KeySize];
        return peerPublicKey.Length == X448.KeySize && X448.TryComputeSharedSecret(privateKey, peerPublicKey, sharedSecret)
            ? sharedSecret
            : null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        CryptographicOperations.ZeroMemory(privateKey);
        base.Dispose(disposing);
    }

    private static byte[] PublicKeyOf(byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        byte[] publicKey = new byte[X448.KeySize];
        X448.ComputePublicKey(privateKey, publicKey);
        return publicKey;
    }
}
