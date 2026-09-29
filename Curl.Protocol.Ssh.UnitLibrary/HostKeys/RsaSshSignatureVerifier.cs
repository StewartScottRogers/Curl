using System.Security.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// <c>rsa-sha2-512</c>, <c>rsa-sha2-256</c> (RFC 8332) and <c>ssh-rsa</c> (RFC 4253
/// section 6.6): RSASSA-PKCS1-v1_5 over H with an <c>ssh-rsa</c> host key, from the BCL's
/// <see cref="RSA" />.
/// </summary>
/// <param name="signatureName">The signature algorithm, which the signature blob must name.</param>
/// <param name="hashAlgorithm">Its hash.</param>
internal sealed class RsaSshSignatureVerifier(string signatureName, HashAlgorithmName hashAlgorithm) : ISshSignatureVerifier
{
    private const string KeyType = "ssh-rsa";

    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash)
    {
        SshWireReader key = SshKeyBlobReader.Open(hostKey, KeyType);
        byte[] exponent = key.ReadMpint().ToArray();
        byte[] modulus = key.ReadMpint().ToArray();
        if (modulus.Length == 0)
        {
            // Windows' RSA refuses an empty modulus with a CryptographicException, but
            // OpenSSL's throws IndexOutOfRangeException; refuse it here so every platform
            // fails the key exchange the same way.
            throw new CryptographicException("The ssh-rsa host key has an empty modulus.");
        }

        byte[] signatureBytes = SshKeyBlobReader.ReadSignature(signature, signatureName);
        if (signatureBytes.Length > modulus.Length)
        {
            return false;
        }

        // OpenSSH and libssh2 accept a signature shorter than the modulus by padding it with
        // leading zeros; the BCL wants it the modulus's length.
        byte[] padded = new byte[modulus.Length];
        signatureBytes.CopyTo(padded, modulus.Length - signatureBytes.Length);
        using RSA rsa = RSA.Create(new RSAParameters { Exponent = exponent, Modulus = modulus });
        return rsa.VerifyData(exchangeHash, padded, hashAlgorithm, RSASignaturePadding.Pkcs1);
    }
}
