using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The server's half of X25519MLKEM768 (draft-ietf-tls-ecdhe-mlkem), which the client
/// library does not need: encapsulate to the client's ML-KEM-768 key and agree X25519
/// with its X25519 key, answering the ciphertext followed by the server's X25519 key.
/// </summary>
internal static class X25519MlKem768ServerShare
{
    private static readonly int EncapsulationKeyLength = MlKem.GetEncapsulationKeySize(MlKemParameterSet.MlKem768);

    private static readonly int CiphertextLength = MlKem.GetCiphertextSize(MlKemParameterSet.MlKem768);

    /// <summary>Answers <paramref name="clientShare" />, or with no client share a random value and an all-zero secret.</summary>
    public static (byte[] KeyExchange, byte[] SharedSecret) Answer(byte[]? clientShare)
    {
        byte[] x25519PrivateKey = RandomNumberGenerator.GetBytes(X25519.KeySize);
        byte[] keyExchange = new byte[CiphertextLength + X25519.KeySize];
        X25519.ComputePublicKey(x25519PrivateKey, keyExchange.AsSpan(CiphertextLength));
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize + X25519.KeySize];
        if (clientShare is null)
        {
            RandomNumberGenerator.Fill(keyExchange.AsSpan(0, CiphertextLength));
            return (keyExchange, sharedSecret);
        }

        Assert.AreEqual(EncapsulationKeyLength + X25519.KeySize, clientShare.Length);
        Assert.IsTrue(MlKem.TryEncapsulate(MlKemParameterSet.MlKem768, clientShare.AsSpan(0, EncapsulationKeyLength), keyExchange.AsSpan(0, CiphertextLength), sharedSecret.AsSpan(0, MlKem.SharedSecretSize)));
        Assert.IsTrue(X25519.TryComputeSharedSecret(x25519PrivateKey, clientShare.AsSpan(EncapsulationKeyLength), sharedSecret.AsSpan(MlKem.SharedSecretSize)));
        return (keyExchange, sharedSecret);
    }
}
