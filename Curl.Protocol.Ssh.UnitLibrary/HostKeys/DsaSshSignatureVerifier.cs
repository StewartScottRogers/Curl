using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// <c>ssh-dss</c> (RFC 4253 section 6.6): DSA over SHA-1(H), with a 160-bit r and s, from
/// the hand-built <see cref="DsaSignature" /> because the BCL's DSA is missing on macOS.
/// </summary>
internal sealed class DsaSshSignatureVerifier : ISshSignatureVerifier
{
    private const string Name = "ssh-dss";

    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash)
    {
        SshWireReader key = SshKeyBlobReader.Open(hostKey, Name);
        ReadOnlySpan<byte> prime = key.ReadMpint().Span;
        ReadOnlySpan<byte> subprime = key.ReadMpint().Span;
        ReadOnlySpan<byte> generator = key.ReadMpint().Span;
        ReadOnlySpan<byte> publicKey = key.ReadMpint().Span;
        byte[] signatureBytes = SshKeyBlobReader.ReadSignature(signature, Name);
        return DsaSignature.VerifyHash(prime, subprime, generator, publicKey, SHA1.HashData(exchangeHash), signatureBytes);
    }
}
