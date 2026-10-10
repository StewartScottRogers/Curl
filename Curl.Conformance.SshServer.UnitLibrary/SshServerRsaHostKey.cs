using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The server's fixed <c>ssh-rsa</c> host key, a 2048-bit key generated once for this server,
/// so its blob and fingerprints are the same on every run and platform. It is the host key a
/// client gets when it offers no <c>ssh-ed25519</c>, as curl's libssh2 1.11.1 WinCNG build
/// offers none (BL-1951). It signs as <c>rsa-sha2-512</c>, <c>rsa-sha2-256</c> (RFC 8332) or
/// <c>ssh-rsa</c> (RFC 4253 section 6.6), whichever the client agreed.
/// </summary>
internal static class SshServerRsaHostKey
{
    /// <summary>The key type, the name inside its blob.</summary>
    internal const string KeyType = "ssh-rsa";

    /// <summary>
    /// The MD5 of <see cref="Blob" /> as 32 lowercase hex digits, the form
    /// <c>--hostpubmd5</c> takes.
    /// </summary>
    internal const string Md5Fingerprint = "2948c3aaadd13b5fc3053eb5f02ff41d";

    /// <summary>
    /// The SHA-256 of <see cref="Blob" /> in base64 without padding, the form
    /// <c>--hostpubsha256</c> takes.
    /// </summary>
    internal const string Sha256Fingerprint = "oKu2ijiKRpAnWn3uWXJlDBMslPHR6h9ZZOV89/I8n2o";

    private const string PrivateKeyPkcs1 =
        "MIIEpAIBAAKCAQEAxXD0qJwgnyV5mEvxKp7FFu7CgTLqtjz2ugCWftONeEPAahGdCTiO9hzXsOBxwemHY5Nr1Wyg51yjmRuXyjB5WrXh2cW2IfB9uZeQOzCl/1LqntG93w41LDTe3qLaL+r7zu0uVi3lnLa2ZuiDRGZJK+qKQnN8ZTtGsRBAqEkZ5W/mU9YQCHv/CGxBhZcBMgJCARfX920OKjt8XnJ3s1n2fs8IsaoJA8P+Ocjv2qSRHrgbMaM9TfDVmL2okurAneQohA19fiw0Ypw26JJ6sQTQPJXWhphH0wx0o6erhEoibM3Z3XdUbYfj7qmWjg8iaXcLU4pPzIoVelUzdsoHOgBx8QIDAQABAoIBABZUdBctsTLOljX3QMtFMZ0qW60pCQqbUnwu3NZzLqjPYM+eNlh4oRKMqIhEmhkIRFuqrKNyqbAA9i+2wzAG03LC50wt5C9qd42p6pIHKn757bcQmZzEipb5U8tyN6L7vIbmpDry7ekPcQmqek4eE2B4IjLpxMPeNFyKLgXaXqdoET0fCx3QkKp03WEIaA9vBjV8MGR6tdCaXaz4rotqnXXGaaCYI+I5kFM5vuZo2Mz6bn2i/8psYxgAHpIcYqa/Vi3RLWZtBh3ceMyBme7it6y1NlQZN62iH/ro3ZoV1GMgzeYt04ZHX2EYuokXeYbrDDXVcsNyEuSv6YcbDiI55pECgYEA/K1yJIv2dG7PpcGGAPxp1kC5mwUiCaE3R8GCVZlng383AvUuwaXR/Ta3C19cFA8Qdy0MYh+ZIt23dLqQZP3qVdgCTHsfLjmTUiK0j1pSxhSF0XTIcG5aqdPLBK4JTXQatcVe7/R3Il43BcUTDwJHBwtSZfGWpVqPpttKwtXMMXMCgYEAyAmTTHDtfGdoqwkU+DOxXr9Rdoiizfzr8yaYsr7dAUiuj+zGR7Mi9e6wjvVrm2RetqqZbormW/TMx5fe9bJrffGOvc4N1ZGHNobJHKSXkFL/eX0JC4RoYGW67KQfc4oimJflxOao8ebtrH3CX/7/1LoAFBmBhJ0ecbOED0u/5gsCgYATtpB2K3lB4jdPDkcfIpI7Rfd1EbRzHeHLNlytwvchejZXg7tvHjnA0Jj9SSZM5lP5iEk0CpUO9E9QyxFi37B9nAmp390x5QoKWWzO1lQo4gcCrWLJ6sImB8EKD23oXguLbOeYPvFgfAs85wAyppHWCdsW2v9OzT7x3J/7jWSN4wKBgQCprF2RuDPokroYYVK1GPu49yS9Gzak7ISisTlSXEyPuzu9/sNMq+janASMriUBIIip/li4h/7PPvy3y90loJpGsK831eCsJ41+NaO+tOmOVKg4P1N9vmjeY0vDT48R7LhdoHIXquLglNVCu0U1MdGEeeR5KA1RFAhvvyoj40e5cwKBgQD0GK0dImKt3gwgWDr/UuOt53oRA4FvVq0pYdEZNKb4oMU/HqLsQIa04/INeX+EEScEpjszsuvdtK+QF0PLeMbNa81nGqJOAL02EkyqwBukMnrN9Sj/LNEKitq5VuC42x1gjfNmGX4u7+Wy1Fu4SaGhRIsk3d5XTV+IRfP9mfSBOA==";

    private static readonly Dictionary<string, HashAlgorithmName> SignatureHashes = new(StringComparer.Ordinal)
    {
        ["rsa-sha2-512"] = HashAlgorithmName.SHA512,
        ["rsa-sha2-256"] = HashAlgorithmName.SHA256,
        [KeyType] = HashAlgorithmName.SHA1,
    };

    /// <summary>Gets the host-key algorithms the key signs with, in libssh2's order of preference.</summary>
    internal static IReadOnlyList<string> Algorithms { get; } = ["rsa-sha2-512", "rsa-sha2-256", KeyType];

    /// <summary>Gets the public key blob: the key type, then e and n as mpints (RFC 4253 section 6.6).</summary>
    internal static byte[] Blob { get; } = CreateBlob();

    /// <summary>Computes the MD5 fingerprint of <see cref="Blob" />, as <see cref="Md5Fingerprint" /> pins it.</summary>
    /// <returns>32 lowercase hex digits.</returns>
    internal static string ComputeMd5Fingerprint() => Convert.ToHexStringLower(MD5.HashData(Blob));

    /// <summary>Computes the SHA-256 fingerprint of <see cref="Blob" />, as <see cref="Sha256Fingerprint" /> pins it.</summary>
    /// <returns>Base64 without its trailing <c>=</c>.</returns>
    internal static string ComputeSha256Fingerprint() => Convert.ToBase64String(SHA256.HashData(Blob)).TrimEnd('=');

    /// <summary>
    /// Signs an exchange hash with RSASSA-PKCS1-v1_5 over the hash <paramref name="algorithm" /> names.
    /// </summary>
    /// <param name="algorithm">One of <see cref="Algorithms" />: the host-key algorithm the client agreed.</param>
    /// <param name="exchangeHash">H.</param>
    /// <returns>The signature blob: the algorithm's name, then the signature, both as strings.</returns>
    internal static byte[] Sign(string algorithm, ReadOnlySpan<byte> exchangeHash)
    {
        using RSA rsa = CreateKey();
        SshWireWriter writer = new();
        writer.WriteString(Encoding.ASCII.GetBytes(algorithm));
        writer.WriteString(rsa.SignData(exchangeHash.ToArray(), SignatureHashes[algorithm], RSASignaturePadding.Pkcs1));
        return writer.ToArray();
    }

    private static RSA CreateKey()
    {
        RSA rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(PrivateKeyPkcs1), out _);
        return rsa;
    }

    private static byte[] CreateBlob()
    {
        using RSA rsa = CreateKey();
        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: false);
        SshWireWriter writer = new();
        writer.WriteString(Encoding.ASCII.GetBytes(KeyType));
        writer.WriteMpint(parameters.Exponent);
        writer.WriteMpint(parameters.Modulus);
        return writer.ToArray();
    }
}
