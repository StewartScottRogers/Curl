using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// A pure ML-KEM key share on MLKEM512, MLKEM768 or MLKEM1024 (draft-ietf-tls-mlkem): the
/// client's public value is the ML-KEM encapsulation key, the server answers with the
/// ML-KEM ciphertext, and the shared secret is the 32-byte ML-KEM shared secret.
/// </summary>
public sealed class MlKemKeyShare : Tls13KeyShare
{
    private readonly MlKem mlKem;

    /// <summary>Creates the share of <paramref name="mlKem" />, which it takes ownership of.</summary>
    /// <param name="group">The named group: MLKEM512, MLKEM768 or MLKEM1024.</param>
    /// <param name="mlKem">A key pair of the group's parameter set.</param>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a pure ML-KEM group.</exception>
    /// <exception cref="ArgumentException">The key is not of the group's parameter set.</exception>
    public MlKemKeyShare(ushort group, MlKem mlKem)
        : base(group, EncapsulationKeyOf(ParameterSetOf(group), mlKem)) => this.mlKem = mlKem;

    /// <summary>Generates a fresh key pair of <paramref name="group" />'s parameter set.</summary>
    /// <param name="group">The named group: MLKEM512, MLKEM768 or MLKEM1024.</param>
    /// <returns>The share.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a pure ML-KEM group.</exception>
    public static MlKemKeyShare Generate(ushort group) => new(group, MlKem.GenerateKey(ParameterSetOf(group)));

    /// <summary>Returns the ML-KEM parameter set of a pure ML-KEM group.</summary>
    /// <param name="group">The named group: MLKEM512, MLKEM768 or MLKEM1024.</param>
    /// <returns>The parameter set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The group is not a pure ML-KEM group.</exception>
    public static MlKemParameterSet ParameterSetOf(ushort group) => group switch
    {
        TlsNamedGroup.MlKem512 => MlKemParameterSet.MlKem512,
        TlsNamedGroup.MlKem768 => MlKemParameterSet.MlKem768,
        TlsNamedGroup.MlKem1024 => MlKemParameterSet.MlKem1024,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "The group is not a pure ML-KEM group."),
    };

    /// <inheritdoc />
    /// <remarks>Any ciphertext of the right length decapsulates; a forged one gives FIPS 203's implicit rejection secret.</remarks>
    public override byte[]? ComputeSharedSecret(byte[] peerPublicKey)
    {
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        if (peerPublicKey.Length != MlKem.GetCiphertextSize(mlKem.ParameterSet))
        {
            return null;
        }

        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        mlKem.Decapsulate(peerPublicKey, sharedSecret);
        return sharedSecret;
    }

    /// <summary>Exports the encapsulation key of <paramref name="mlKem" />, after checking it is of <paramref name="parameterSet" />.</summary>
    /// <param name="parameterSet">The parameter set the group needs.</param>
    /// <param name="mlKem">The key pair.</param>
    /// <returns>The encapsulation key.</returns>
    /// <exception cref="ArgumentException">The key is not of <paramref name="parameterSet" />.</exception>
    internal static byte[] EncapsulationKeyOf(MlKemParameterSet parameterSet, MlKem mlKem)
    {
        ArgumentNullException.ThrowIfNull(mlKem);
        if (mlKem.ParameterSet != parameterSet)
        {
            throw new ArgumentException($"The group needs an {parameterSet} key.", nameof(mlKem));
        }

        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        mlKem.ExportEncapsulationKey(encapsulationKey);
        return encapsulationKey;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        mlKem.Dispose();
        base.Dispose(disposing);
    }
}
