using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// A SecP256r1MLKEM768 or SecP384r1MLKEM1024 hybrid key share (draft-ietf-tls-ecdhe-mlkem):
/// unlike X25519MLKEM768, the elliptic-curve half comes first. The client's public value is
/// the uncompressed ECDH point followed by the ML-KEM encapsulation key; the server answers
/// with its uncompressed point followed by the ML-KEM ciphertext; the shared secret is the
/// ECDH secret (the X coordinate) followed by the 32-byte ML-KEM secret.
/// </summary>
public sealed class EcdhMlKemKeyShare : Tls13KeyShare
{
    private readonly EcdhKeyShare ecdh;
    private readonly MlKem mlKem;

    /// <summary>Creates the share of <paramref name="ecdh" /> and <paramref name="mlKem" />, which it takes ownership of.</summary>
    /// <param name="group">The named group: SecP256r1MLKEM768 or SecP384r1MLKEM1024.</param>
    /// <param name="ecdh">A share on the group's curve: secp256r1 or secp384r1.</param>
    /// <param name="mlKem">A key pair of the group's parameter set: ML-KEM-768 or ML-KEM-1024.</param>
    /// <exception cref="ArgumentOutOfRangeException">The group is not one of the two hybrids.</exception>
    /// <exception cref="ArgumentException">The share is on another curve, or the key is of another parameter set.</exception>
    public EcdhMlKemKeyShare(ushort group, EcdhKeyShare ecdh, MlKem mlKem)
        : base(group, PublicKeyOf(group, ecdh, mlKem))
    {
        this.ecdh = ecdh;
        this.mlKem = mlKem;
    }

    /// <summary>Generates a fresh ECDH key pair and ML-KEM key pair for <paramref name="group" />.</summary>
    /// <param name="group">The named group: SecP256r1MLKEM768 or SecP384r1MLKEM1024.</param>
    /// <returns>The share.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group is not one of the two hybrids.</exception>
    public static EcdhMlKemKeyShare Generate(ushort group)
    {
        (ushort curve, MlKemParameterSet parameterSet) = HalvesOf(group);
        return new EcdhMlKemKeyShare(group, EcdhKeyShare.Generate(curve), MlKem.GenerateKey(parameterSet));
    }

    /// <inheritdoc />
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        int pointLength = ecdh.PublicKey.Length;
        if (peerPublicKey.Length != pointLength + MlKem.GetCiphertextSize(mlKem.ParameterSet)
            || ecdh.ComputeSharedSecret(peerPublicKey[..pointLength]) is not { } ecdhSecret)
        {
            return null;
        }

        byte[] sharedSecret = new byte[ecdhSecret.Length + MlKem.SharedSecretSize];
        ecdhSecret.CopyTo(sharedSecret, 0);
        CryptographicOperations.ZeroMemory(ecdhSecret);
        mlKem.Decapsulate(peerPublicKey.AsSpan(pointLength), sharedSecret.AsSpan(sharedSecret.Length - MlKem.SharedSecretSize));
        return sharedSecret;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        ecdh.Dispose();
        mlKem.Dispose();
        base.Dispose(disposing);
    }

    private static (ushort Curve, MlKemParameterSet ParameterSet) HalvesOf(ushort group) => group switch
    {
        TlsNamedGroup.SecP256r1MlKem768 => (TlsNamedGroup.Secp256r1, MlKemParameterSet.MlKem768),
        TlsNamedGroup.SecP384r1MlKem1024 => (TlsNamedGroup.Secp384r1, MlKemParameterSet.MlKem1024),
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "The group is not an ECDH and ML-KEM hybrid."),
    };

    private static byte[] PublicKeyOf(ushort group, EcdhKeyShare ecdh, MlKem mlKem)
    {
        (ushort curve, MlKemParameterSet parameterSet) = HalvesOf(group);
        ArgumentNullException.ThrowIfNull(ecdh);
        if (ecdh.Group != curve)
        {
            throw new ArgumentException("The ECDH share is not on the group's curve.", nameof(ecdh));
        }

        return [.. ecdh.PublicKey, .. MlKemKeyShare.EncapsulationKeyOf(parameterSet, mlKem)];
    }
}
