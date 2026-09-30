using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// <c>sk-ssh-ed25519@openssh.com</c> (OpenSSH's <c>PROTOCOL.u2f</c>): a FIDO security key's
/// Ed25519 signature, from the hand-built <see cref="Ed25519" />, of the data
/// <see cref="SecurityKeySignedData" /> composes from H. The key blob carries the 32-byte
/// public key and the application string; the signature blob the 64-byte signature, then a
/// flags byte and a counter. The flags are not checked, as OpenSSH does not check them
/// on a host key's signature over H (ADR-0266).
/// </summary>
internal sealed class SecurityKeyEd25519SshSignatureVerifier : ISshSignatureVerifier
{
    private const string Name = "sk-ssh-ed25519@openssh.com";

    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash)
    {
        SshWireReader key = SshKeyBlobReader.Open(hostKey, Name);
        ReadOnlyMemory<byte> publicKey = key.ReadString();
        if (publicKey.Length != Ed25519.PublicKeySize)
        {
            throw new InvalidDataException($"The SSH server's {Name} host key is {publicKey.Length} bytes, not {Ed25519.PublicKeySize}.");
        }

        ReadOnlyMemory<byte> application = key.ReadString();
        SshWireReader signatureReader = SshKeyBlobReader.Open(signature, Name);
        ReadOnlyMemory<byte> signatureBytes = signatureReader.ReadString();
        byte[] signedData = SecurityKeySignedData.Read(application, signatureReader, exchangeHash);
        return signatureBytes.Length == Ed25519.SignatureSize && Ed25519.Verify(publicKey.Span, signedData, signatureBytes.Span);
    }
}
