using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// X25519 (RFC 7748) from the hand-built <see cref="X25519" />: 32-byte public keys each way,
/// and an all-zero secret refused, as RFC 8731 section 3 and OpenSSH require.
/// </summary>
internal sealed class X25519SshKeyShare : ISshKeyShare
{
    private readonly byte[] privateKey = new byte[X25519.KeySize];

    /// <summary>
    /// Initializes a new instance of the <see cref="X25519SshKeyShare" /> class with a
    /// private key from <paramref name="keySource" />.
    /// </summary>
    /// <param name="keySource">Where the private key comes from.</param>
    internal X25519SshKeyShare(ISshEphemeralKeySource keySource)
    {
        keySource.CreateX25519PrivateKey(privateKey);
        X25519.ComputePublicKey(privateKey, ClientShare);
    }

    /// <inheritdoc />
    public byte[] ClientShare { get; } = new byte[X25519.KeySize];

    /// <inheritdoc />
    public int ServerShareLength => X25519.KeySize;

    /// <inheritdoc />
    public byte[] ComputeSharedSecret(ReadOnlySpan<byte> serverShare)
    {
        if (serverShare.Length != X25519.KeySize)
        {
            throw new InvalidDataException($"The SSH server's X25519 public key is {serverShare.Length} bytes, not {X25519.KeySize}.");
        }

        byte[] sharedSecret = new byte[X25519.KeySize];
        if (!X25519.TryComputeSharedSecret(privateKey, serverShare, sharedSecret))
        {
            throw new InvalidDataException("The SSH server's X25519 public key gives an all-zero shared secret.");
        }

        return sharedSecret;
    }

    /// <inheritdoc />
    public void Dispose() => CryptographicOperations.ZeroMemory(privateKey);
}
