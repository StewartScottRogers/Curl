using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// <c>ssh-ed25519</c> (RFC 8709): an Ed25519 signature over H, from the hand-built
/// <see cref="Ed25519" />. The key blob carries the 32-byte public key and the signature blob
/// the 64-byte signature, each as a <c>string</c>.
/// </summary>
internal sealed class Ed25519SshSignatureVerifier : ISshSignatureVerifier
{
    private const string Name = "ssh-ed25519";

    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash)
    {
        ReadOnlySpan<byte> publicKey = SshKeyBlobReader.Open(hostKey, Name).ReadString().Span;
        if (publicKey.Length != Ed25519.PublicKeySize)
        {
            throw new InvalidDataException($"The SSH server's {Name} host key is {publicKey.Length} bytes, not {Ed25519.PublicKeySize}.");
        }

        byte[] signatureBytes = SshKeyBlobReader.ReadSignature(signature, Name);
        return signatureBytes.Length == Ed25519.SignatureSize && Ed25519.Verify(publicKey, exchangeHash, signatureBytes);
    }
}
