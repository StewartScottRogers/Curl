using System.Security.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// The host-key algorithms this library verifies, by the name <c>KEXINIT</c> offers them
/// under: every row of ADR-0122's host-key table, the OpenSSH certificate forms and the
/// <c>sk-</c> security-key forms among them (ADR-0266).
/// </summary>
internal static class SshSignatureVerifiers
{
    private static readonly Dictionary<string, ISshSignatureVerifier> Verifiers = Create();

    /// <summary>Gets the names of the verified host-key algorithms.</summary>
    internal static IEnumerable<string> Names => Verifiers.Keys;

    /// <summary>
    /// Gets the verifier for the host-key algorithm negotiated.
    /// </summary>
    /// <param name="name">The algorithm's name.</param>
    /// <returns>The verifier.</returns>
    /// <exception cref="NotSupportedException">
    /// The name is not implemented; the catalogue never offers such a name, so only a
    /// catalogue built for a test can agree one.
    /// </exception>
    internal static ISshSignatureVerifier For(string name) =>
        Verifiers.TryGetValue(name, out ISshSignatureVerifier? verifier)
            ? verifier
            : throw new NotSupportedException($"The SSH host-key algorithm {name} is not implemented.");

    private static Dictionary<string, ISshSignatureVerifier> Create()
    {
        ISshSignatureVerifier ecdsaP256 = new EcdsaSshSignatureVerifier(SshNistCurve.NistP256);
        ISshSignatureVerifier ecdsaP384 = new EcdsaSshSignatureVerifier(SshNistCurve.NistP384);
        ISshSignatureVerifier ecdsaP521 = new EcdsaSshSignatureVerifier(SshNistCurve.NistP521);
        ISshSignatureVerifier rsaSha512 = new RsaSshSignatureVerifier("rsa-sha2-512", HashAlgorithmName.SHA512);
        ISshSignatureVerifier rsaSha256 = new RsaSshSignatureVerifier("rsa-sha2-256", HashAlgorithmName.SHA256);
        ISshSignatureVerifier rsaSha1 = new RsaSshSignatureVerifier("ssh-rsa", HashAlgorithmName.SHA1);
        ISshSignatureVerifier ed25519 = new Ed25519SshSignatureVerifier();
        ISshSignatureVerifier securityKeyEcdsa = new SecurityKeyEcdsaSshSignatureVerifier();
        ISshSignatureVerifier securityKeyEd25519 = new SecurityKeyEd25519SshSignatureVerifier();
        return new Dictionary<string, ISshSignatureVerifier>(StringComparer.Ordinal)
        {
            ["ecdsa-sha2-nistp256"] = ecdsaP256,
            ["ecdsa-sha2-nistp384"] = ecdsaP384,
            ["ecdsa-sha2-nistp521"] = ecdsaP521,
            ["ecdsa-sha2-nistp256-cert-v01@openssh.com"] = Certificate("ecdsa-sha2-nistp256-cert-v01@openssh.com", "ecdsa-sha2-nistp256", 2, ecdsaP256),
            ["ecdsa-sha2-nistp384-cert-v01@openssh.com"] = Certificate("ecdsa-sha2-nistp384-cert-v01@openssh.com", "ecdsa-sha2-nistp384", 2, ecdsaP384),
            ["ecdsa-sha2-nistp521-cert-v01@openssh.com"] = Certificate("ecdsa-sha2-nistp521-cert-v01@openssh.com", "ecdsa-sha2-nistp521", 2, ecdsaP521),
            ["rsa-sha2-512"] = rsaSha512,
            ["rsa-sha2-256"] = rsaSha256,
            ["ssh-rsa"] = rsaSha1,
            ["rsa-sha2-512-cert-v01@openssh.com"] = Certificate("rsa-sha2-512-cert-v01@openssh.com", "ssh-rsa", 2, rsaSha512),
            ["rsa-sha2-256-cert-v01@openssh.com"] = Certificate("rsa-sha2-256-cert-v01@openssh.com", "ssh-rsa", 2, rsaSha256),
            ["ssh-rsa-cert-v01@openssh.com"] = Certificate("ssh-rsa-cert-v01@openssh.com", "ssh-rsa", 2, rsaSha1),
            ["ssh-dss"] = new DsaSshSignatureVerifier(),
            ["ssh-ed25519"] = ed25519,
            ["ssh-ed25519-cert-v01@openssh.com"] = Certificate("ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519", 1, ed25519),
            ["sk-ecdsa-sha2-nistp256@openssh.com"] = securityKeyEcdsa,
            ["sk-ssh-ed25519@openssh.com"] = securityKeyEd25519,
            ["sk-ecdsa-sha2-nistp256-cert-v01@openssh.com"] = Certificate("sk-ecdsa-sha2-nistp256-cert-v01@openssh.com", "sk-ecdsa-sha2-nistp256@openssh.com", 3, securityKeyEcdsa),
            ["sk-ssh-ed25519-cert-v01@openssh.com"] = Certificate("sk-ssh-ed25519-cert-v01@openssh.com", "sk-ssh-ed25519@openssh.com", 2, securityKeyEd25519),
        };
    }

    private static OpenSshCertificateSshSignatureVerifier Certificate(string certificateName, string keyType, int keyFieldCount, ISshSignatureVerifier keyVerifier) =>
        new(certificateName, keyType, keyFieldCount, keyVerifier);
}
