using System.Security.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// <c>sk-ecdsa-sha2-nistp256@openssh.com</c> (OpenSSH's <c>PROTOCOL.u2f</c>): a FIDO
/// security key's ECDSA P-256 signature, over SHA-256, of the data
/// <see cref="SecurityKeySignedData" /> composes from H. The key blob carries the curve name,
/// the public point and the application string; the signature blob the ECDSA signature,
/// then a flags byte and a counter. The flags are not checked, as OpenSSH does not check
/// them on a host key's signature over H (ADR-0266).
/// </summary>
internal sealed class SecurityKeyEcdsaSshSignatureVerifier : ISshSignatureVerifier
{
    private const string Name = "sk-ecdsa-sha2-nistp256@openssh.com";

    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash)
    {
        SshNistCurve curve = SshNistCurve.NistP256;
        SshWireReader key = SshKeyBlobReader.Open(hostKey, Name);
        ECParameters publicKey = EcdsaSshSignatureVerifier.ReadPublicKey(key, curve, Name);
        ReadOnlyMemory<byte> application = key.ReadString();
        SshWireReader signatureReader = SshKeyBlobReader.Open(signature, Name);
        byte[]? fixedWidth = EcdsaSshSignatureVerifier.ToFixedWidth(signatureReader.ReadString(), curve);
        byte[] signedData = SecurityKeySignedData.Read(application, signatureReader, exchangeHash);
        return fixedWidth is not null && EcdsaSshSignatureVerifier.VerifyData(publicKey, signedData, fixedWidth, curve.HashAlgorithm);
    }
}
