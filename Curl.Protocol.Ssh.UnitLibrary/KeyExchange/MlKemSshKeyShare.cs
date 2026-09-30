using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// ML-KEM (FIPS 203) from the hand-built <see cref="MlKem" />: the client sends its
/// encapsulation key, the server answers with a ciphertext, and the client decapsulates
/// it to the 32-byte secret.
/// </summary>
internal sealed class MlKemSshKeyShare : ISshKeyShare
{
    private readonly MlKem key;

    /// <summary>
    /// Initializes a new instance of the <see cref="MlKemSshKeyShare" /> class with a key
    /// pair from <paramref name="keySource" />.
    /// </summary>
    /// <param name="parameterSet">ML-KEM-768 or ML-KEM-1024.</param>
    /// <param name="keySource">Where the key pair comes from.</param>
    internal MlKemSshKeyShare(MlKemParameterSet parameterSet, ISshEphemeralKeySource keySource)
    {
        key = keySource.CreateMlKemKey(parameterSet);
        ClientShare = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        key.ExportEncapsulationKey(ClientShare);
        ServerShareLength = MlKem.GetCiphertextSize(parameterSet);
    }

    /// <inheritdoc />
    public byte[] ClientShare { get; }

    /// <inheritdoc />
    public int ServerShareLength { get; }

    /// <inheritdoc />
    public byte[] ComputeSharedSecret(ReadOnlySpan<byte> serverShare)
    {
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        key.Decapsulate(serverShare, sharedSecret);
        return sharedSecret;
    }

    /// <inheritdoc />
    public void Dispose() => key.Dispose();
}
