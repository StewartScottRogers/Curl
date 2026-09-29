using System.Security.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// The host-key algorithms this library verifies, by the name <c>KEXINIT</c> offers them
/// under (ADR-0122's host-key table; <c>ssh-ed25519</c>, certificates and <c>sk-</c> keys
/// join when their tasks land).
/// </summary>
internal static class SshSignatureVerifiers
{
    private static readonly Dictionary<string, ISshSignatureVerifier> Verifiers = new()
    {
        ["ecdsa-sha2-nistp256"] = new EcdsaSshSignatureVerifier(SshNistCurve.NistP256),
        ["ecdsa-sha2-nistp384"] = new EcdsaSshSignatureVerifier(SshNistCurve.NistP384),
        ["ecdsa-sha2-nistp521"] = new EcdsaSshSignatureVerifier(SshNistCurve.NistP521),
        ["rsa-sha2-512"] = new RsaSshSignatureVerifier("rsa-sha2-512", HashAlgorithmName.SHA512),
        ["rsa-sha2-256"] = new RsaSshSignatureVerifier("rsa-sha2-256", HashAlgorithmName.SHA256),
        ["ssh-rsa"] = new RsaSshSignatureVerifier("ssh-rsa", HashAlgorithmName.SHA1),
        ["ssh-dss"] = new DsaSshSignatureVerifier(),
    };

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
}
