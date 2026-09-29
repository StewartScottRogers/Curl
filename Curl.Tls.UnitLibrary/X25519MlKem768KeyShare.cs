using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An X25519MLKEM768 hybrid key share (draft-ietf-tls-ecdhe-mlkem): the client's public
/// value is the ML-KEM-768 encapsulation key (1184 bytes) followed by the X25519 public key
/// (32); the server answers with the ML-KEM-768 ciphertext (1088) followed by its X25519
/// public key; the shared secret is the ML-KEM shared secret followed by the X25519 one.
/// </summary>
public sealed class X25519MlKem768KeyShare : Tls13KeyShare
{
    /// <summary>The length in bytes of the server's share: the ML-KEM-768 ciphertext and an X25519 public key.</summary>
    public static readonly int ServerShareLength = MlKem.GetCiphertextSize(MlKemParameterSet.MlKem768) + X25519.KeySize;

    private readonly MlKem mlKem;
    private readonly byte[] x25519PrivateKey;

    /// <summary>Creates the share of <paramref name="mlKem" />, which it takes ownership of, and <paramref name="x25519PrivateKey" />.</summary>
    /// <param name="mlKem">An ML-KEM-768 key pair.</param>
    /// <param name="x25519PrivateKey">The 32-byte X25519 private scalar.</param>
    /// <exception cref="ArgumentException">The ML-KEM key is not ML-KEM-768, or the X25519 key is not 32 bytes.</exception>
    public X25519MlKem768KeyShare(MlKem mlKem, byte[] x25519PrivateKey)
        : base(TlsNamedGroup.X25519MlKem768, PublicKeyOf(mlKem, x25519PrivateKey))
    {
        this.mlKem = mlKem;
        this.x25519PrivateKey = [.. x25519PrivateKey];
    }

    /// <summary>Generates a fresh ML-KEM-768 key pair and X25519 private key.</summary>
    /// <returns>The share.</returns>
    public static X25519MlKem768KeyShare Generate() =>
        new(MlKem.GenerateKey(MlKemParameterSet.MlKem768), RandomNumberGenerator.GetBytes(X25519.KeySize));

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        if (peerPublicKey.Length != ServerShareLength)
        {
            return null;
        }

        int ciphertextLength = ServerShareLength - X25519.KeySize;
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize + X25519.KeySize];
        mlKem.Decapsulate(peerPublicKey.AsSpan(0, ciphertextLength), sharedSecret.AsSpan(0, MlKem.SharedSecretSize));
        if (X25519.TryComputeSharedSecret(x25519PrivateKey, peerPublicKey.AsSpan(ciphertextLength), sharedSecret.AsSpan(MlKem.SharedSecretSize)))
        {
            return sharedSecret;
        }

        CryptographicOperations.ZeroMemory(sharedSecret);
        return null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        mlKem.Dispose();
        CryptographicOperations.ZeroMemory(x25519PrivateKey);
        base.Dispose(disposing);
    }

    private static byte[] PublicKeyOf(MlKem mlKem, byte[] x25519PrivateKey)
    {
        ArgumentNullException.ThrowIfNull(mlKem);
        ArgumentNullException.ThrowIfNull(x25519PrivateKey);
        if (mlKem.ParameterSet != MlKemParameterSet.MlKem768)
        {
            throw new ArgumentException("X25519MLKEM768 needs an ML-KEM-768 key.", nameof(mlKem));
        }

        int encapsulationKeyLength = MlKem.GetEncapsulationKeySize(MlKemParameterSet.MlKem768);
        byte[] publicKey = new byte[encapsulationKeyLength + X25519.KeySize];
        mlKem.ExportEncapsulationKey(publicKey.AsSpan(0, encapsulationKeyLength));
        X25519.ComputePublicKey(x25519PrivateKey, publicKey.AsSpan(encapsulationKeyLength));
        return publicKey;
    }
}
