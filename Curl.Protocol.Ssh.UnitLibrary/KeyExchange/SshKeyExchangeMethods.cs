using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The key-exchange methods this library implements, by the name <c>KEXINIT</c> offers
/// them under (ADR-0122's key-exchange table).
/// </summary>
internal static class SshKeyExchangeMethods
{
    private static readonly Dictionary<string, Func<ISshEphemeralKeySource, ISshKeyExchange>> Factories = new()
    {
        ["mlkem768x25519-sha256"] = HybridKemSshKeyExchange.MlKem768X25519,
        ["mlkem768nistp256-sha256"] = HybridKemSshKeyExchange.MlKem768NistP256,
        ["mlkem1024nistp384-sha384"] = HybridKemSshKeyExchange.MlKem1024NistP384,
        ["sntrup761x25519-sha512"] = HybridKemSshKeyExchange.Sntrup761X25519,
        ["sntrup761x25519-sha512@openssh.com"] = HybridKemSshKeyExchange.Sntrup761X25519,
        ["curve25519-sha256"] = keys => new Curve25519SshKeyExchange(keys),
        ["curve25519-sha256@libssh.org"] = keys => new Curve25519SshKeyExchange(keys),
        ["ecdh-sha2-nistp256"] = keys => new EcdhSshKeyExchange(SshNistCurve.NistP256, keys),
        ["ecdh-sha2-nistp384"] = keys => new EcdhSshKeyExchange(SshNistCurve.NistP384, keys),
        ["ecdh-sha2-nistp521"] = keys => new EcdhSshKeyExchange(SshNistCurve.NistP521, keys),
        ["diffie-hellman-group-exchange-sha256"] = keys => new GroupExchangeSshKeyExchange(HashAlgorithmName.SHA256, keys),
        ["diffie-hellman-group16-sha512"] = keys => new FiniteFieldSshKeyExchange(FiniteFieldDiffieHellmanGroup.Group16, HashAlgorithmName.SHA512, keys),
        ["diffie-hellman-group18-sha512"] = keys => new FiniteFieldSshKeyExchange(FiniteFieldDiffieHellmanGroup.Group18, HashAlgorithmName.SHA512, keys),
        ["diffie-hellman-group14-sha256"] = keys => new FiniteFieldSshKeyExchange(FiniteFieldDiffieHellmanGroup.Group14, HashAlgorithmName.SHA256, keys),
        ["diffie-hellman-group14-sha1"] = keys => new FiniteFieldSshKeyExchange(FiniteFieldDiffieHellmanGroup.Group14, HashAlgorithmName.SHA1, keys),
        ["diffie-hellman-group1-sha1"] = keys => new FiniteFieldSshKeyExchange(FiniteFieldDiffieHellmanGroup.Group2, HashAlgorithmName.SHA1, keys),
        ["diffie-hellman-group-exchange-sha1"] = keys => new GroupExchangeSshKeyExchange(HashAlgorithmName.SHA1, keys),
    };

    /// <summary>Gets the names of the implemented methods.</summary>
    internal static IEnumerable<string> Names => Factories.Keys;

    /// <summary>
    /// Creates the method negotiated.
    /// </summary>
    /// <param name="name">The method's name.</param>
    /// <param name="keySource">Where its ephemeral key pair comes from.</param>
    /// <returns>The method.</returns>
    /// <exception cref="NotSupportedException">
    /// The name is not implemented; the catalogue never offers such a name, so only a
    /// catalogue built for a test can agree one.
    /// </exception>
    internal static ISshKeyExchange Create(string name, ISshEphemeralKeySource keySource) =>
        Factories.TryGetValue(name, out Func<ISshEphemeralKeySource, ISshKeyExchange>? factory)
            ? factory(keySource)
            : throw new NotSupportedException($"The SSH key-exchange method {name} is not implemented.");
}
