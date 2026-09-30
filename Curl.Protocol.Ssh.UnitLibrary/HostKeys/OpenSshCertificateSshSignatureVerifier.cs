using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// An OpenSSH certificate host-key algorithm, such as
/// <c>ssh-ed25519-cert-v01@openssh.com</c> (OpenSSH's <c>PROTOCOL.certkeys</c>): the
/// signature over H is checked with the key the certificate certifies, by the verifier of
/// that key's own algorithm. The certificate blob is its name, a nonce, the certified key's
/// fields, then the serial, type, key ID, principals, validity window, options, extensions,
/// CA key and CA signature. As libssh2 1.11.1 does, only the name, the nonce and the key
/// are read: the CA, its signature, the validity window and the principals are not checked
/// (measured 2026-09-29, ADR-0266).
/// </summary>
/// <param name="certificateName">The certificate's key type, which the blob must start with.</param>
/// <param name="keyType">The certified key's own key type, such as <c>ssh-ed25519</c>.</param>
/// <param name="keyFieldCount">
/// How many <c>string</c> or <c>mpint</c> fields the certified key has after its key type:
/// 2 for RSA (e, n), 2 for ECDSA (curve, point), 1 for Ed25519, one more for an <c>sk-</c>
/// key's application.
/// </param>
/// <param name="keyVerifier">The verifier of the certified key's algorithm.</param>
internal sealed class OpenSshCertificateSshSignatureVerifier(
    string certificateName,
    string keyType,
    int keyFieldCount,
    ISshSignatureVerifier keyVerifier) : ISshSignatureVerifier
{
    /// <inheritdoc />
    public bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash) =>
        keyVerifier.Verify(CertifiedKey(hostKey), signature, exchangeHash);

    /// <summary>
    /// Rebuilds the key blob of the key a certificate certifies.
    /// </summary>
    /// <param name="certificate">The certificate blob.</param>
    /// <returns>The key blob: the key type, then the certificate's key fields.</returns>
    /// <exception cref="InvalidDataException">The certificate is malformed or names another type.</exception>
    internal byte[] CertifiedKey(ReadOnlyMemory<byte> certificate)
    {
        SshWireReader reader = SshKeyBlobReader.Open(certificate, certificateName);
        reader.ReadString();
        SshWireWriter key = new();
        key.WriteString(Encoding.ASCII.GetBytes(keyType));
        for (int field = 0; field < keyFieldCount; field++)
        {
            key.WriteString(reader.ReadString().Span);
        }

        return key.ToArray();
    }
}
