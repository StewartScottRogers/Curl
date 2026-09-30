using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// Streamlined NTRU Prime 761 from the hand-built <see cref="Sntrup761" />: the client sends
/// its public key, the server answers with a ciphertext, and the client decapsulates it to
/// the 32-byte secret.
/// </summary>
internal sealed class Sntrup761SshKeyShare : ISshKeyShare
{
    private readonly byte[] secretKey = new byte[Sntrup761.SecretKeySize];

    /// <summary>
    /// Initializes a new instance of the <see cref="Sntrup761SshKeyShare" /> class with a key
    /// pair from <paramref name="keySource" />.
    /// </summary>
    /// <param name="keySource">Where the key pair comes from.</param>
    internal Sntrup761SshKeyShare(ISshEphemeralKeySource keySource) =>
        keySource.CreateSntrup761KeyPair(ClientShare, secretKey);

    /// <inheritdoc />
    public byte[] ClientShare { get; } = new byte[Sntrup761.PublicKeySize];

    /// <inheritdoc />
    public int ServerShareLength => Sntrup761.CiphertextSize;

    /// <inheritdoc />
    public byte[] ComputeSharedSecret(ReadOnlySpan<byte> serverShare)
    {
        byte[] sharedSecret = new byte[Sntrup761.SharedSecretSize];
        Sntrup761.Decapsulate(secretKey, serverShare, sharedSecret);
        return sharedSecret;
    }

    /// <inheritdoc />
    public void Dispose() => CryptographicOperations.ZeroMemory(secretKey);
}
