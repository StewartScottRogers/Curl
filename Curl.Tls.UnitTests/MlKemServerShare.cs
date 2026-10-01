using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The server's half of the ML-KEM groups, which the client library does not need: for pure
/// ML-KEM (draft-ietf-tls-mlkem) an encapsulation to the client's key, answered as the
/// ciphertext; for SecP256r1MLKEM768 and SecP384r1MLKEM1024 (draft-ietf-tls-ecdhe-mlkem) the
/// server's own point and an agreement with the client's, then the encapsulation, answered
/// as the point followed by the ciphertext. X25519MLKEM768 goes to <see cref="X25519MlKem768ServerShare" />.
/// </summary>
internal static class MlKemServerShare
{
    /// <summary>
    /// Returns whether <paramref name="group" /> - pure ML-KEM or one of the three hybrids,
    /// X25519MLKEM768 lying between the other two - is answered by encapsulation.
    /// </summary>
    public static bool IsMlKemGroup(ushort group) =>
        group is (>= TlsNamedGroup.MlKem512 and <= TlsNamedGroup.MlKem1024) or (>= TlsNamedGroup.SecP256r1MlKem768 and <= TlsNamedGroup.SecP384r1MlKem1024);

    /// <summary>Answers <paramref name="clientShare" />, or with no client share a random value and an all-zero secret.</summary>
    public static (byte[] KeyExchange, byte[] SharedSecret) Answer(ushort group, byte[]? clientShare)
    {
        if (group == TlsNamedGroup.X25519MlKem768)
        {
            return X25519MlKem768ServerShare.Answer(clientShare);
        }

        (ushort? curve, MlKemParameterSet parameterSet) = HalvesOf(group);
        using EcdhKeyShare? ecdh = curve is { } named ? EcdhKeyShare.Generate(named) : null;
        byte[] point = ecdh?.PublicKey ?? [];
        int ciphertextLength = MlKem.GetCiphertextSize(parameterSet);
        if (clientShare is null)
        {
            return ([.. point, .. RandomNumberGenerator.GetBytes(ciphertextLength)], new byte[32]);
        }

        Assert.AreEqual(point.Length + MlKem.GetEncapsulationKeySize(parameterSet), clientShare.Length);
        byte[] ecdhSecret = ecdh?.ComputeSharedSecret(clientShare[..point.Length]) ?? [];
        byte[] ciphertext = new byte[ciphertextLength];
        byte[] mlKemSecret = new byte[MlKem.SharedSecretSize];
        Assert.IsTrue(MlKem.TryEncapsulate(parameterSet, clientShare.AsSpan(point.Length), ciphertext, mlKemSecret));
        return ([.. point, .. ciphertext], [.. ecdhSecret, .. mlKemSecret]);
    }

    private static (ushort? Curve, MlKemParameterSet ParameterSet) HalvesOf(ushort group) => group switch
    {
        TlsNamedGroup.SecP256r1MlKem768 => (TlsNamedGroup.Secp256r1, MlKemParameterSet.MlKem768),
        TlsNamedGroup.SecP384r1MlKem1024 => (TlsNamedGroup.Secp384r1, MlKemParameterSet.MlKem1024),
        _ => (null, MlKemKeyShare.ParameterSetOf(group)),
    };
}
