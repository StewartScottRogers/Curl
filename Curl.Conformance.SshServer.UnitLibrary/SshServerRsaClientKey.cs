using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The fixed RSA key pair a case's client authenticates with on Windows: a 2048-bit key
/// generated once, as upstream's <c>sshserver.pl</c> generates RSA client keys, because
/// curl's libssh2 1.11.1 WinCNG build reads no Ed25519 or ECDSA private key (BL-1951). The
/// private key is a PKCS #1 PEM file, the form WinCNG's libssh2 reads for <c>--key</c>.
/// </summary>
internal static class SshServerRsaClientKey
{
    /// <summary>The key type, the name inside its blob and its public key file.</summary>
    internal const string KeyType = "ssh-rsa";

    private const string PrivateKeyPkcs1 =
        "MIIEowIBAAKCAQEAx9aZursrbCYTz/+c1bxgZpFLfK85Fls0y2R6BN/zjaBNljc5pnpVJqaTLc4AP4o/y2BcJqqRnhvgBfJNQzSzedLmtRkDI1aU1L8V4Xe/5195T4XgYJs5XgY3PeSTv2ZlNPsjeSiZsrqNejMb9lPh26Zc8sDIGp1hnCUjDi2CeWLqGUBbbNXNSdtm/7sgplvFV0lp6Yj+z1y7wxokmXrfHPE70Y4uApjoT2Aq8jbJWUSVf312UVdj120aZX+4YZkDC2IV5HFvJTsG5tp+mHocP9LJrHfDtwK49qlUWBWRUdF55VdpYXHiHhydB+Xx2mjYwmdS9tBu+kv1g6ASm+pbeQIDAQABAoIBADMXD1hpD7HnjjsoG32dWnv+e3EWDx9DFB1Hw9ZJNygnKo7T8Z45OlnWTlxwhSm5e5PQP972zqHAeKasiso6yAPGQotcKKw5L3WwoLRy/BH4G7iT/ohURvdd2XOuY4OUdx1zc18/XQQPo6nxNR3l1iFkx/CcDsyXHeRgrjSqt+e2SRmDG1HaegX9EL2iTgbRcYYPUoZXRWyVG+YQ+AByMznfX+h9HuODPExrTzbSO1YCqu8ZVzq+5+nFJz8VvxJJk2bdLZ7XulsA2IUUgnvSqkct6EhOnB3NNPDDIRmkSRfrgzHE0sXy/CoiZdOYwjkJGAp5NEWxbWjDV8ObsJdZ9q0CgYEAz+8mvD4cdCsX0TzyR7v9aKU58lutWzL3RVIfGpPVJ0ZpLS9UeZmGTp2imsUrqsKJmDo2AlmhabQhOoTJByLH+AONiIqSj3Azy1sOHgzXJukbMN6t7udESq/j7QqDnEY6c6iza/D43GMbWlEYfEwYUw6GOFiVV2SoltF6ECiWakMCgYEA9ghcYOASGxhz0T8ntGWSSk6kZeGmt1afymWZiuzBNcgo9sEyDzghzk4kfP+wQLQBRhA9zpImLRkuP40nHO5wZIss15tV/G+RFlPYm2UpZZyCSAU46yiOTkhb61Ugmy+rc17hbxaTHgHMn/4Zz6y68IJ2lSB5Sp0RS15hxy67XZMCgYARF3xcntJh7iTgCx1zLg2va11vAYAnbfILXau22I3903jTwP81m5wBnE2AYVKsj58VxzsnSEzPToIouD95+a7sQQaCQbm4VqW7QnnPmIia5zBX/QXZDGN9rCWVF3coAlHNrgFJjx41p2PZMa9MkEizkw8PVKC59UkyVHevGlb61wKBgQCKmfswuAp7KTNV1fZQMkhPhC4pFw4MishKBSKSmP3fhntNR1kkKcMhrfPwVzq7uKxfBBdNoBfRoDXlp2c5bTnz6lPAwsmWBGP/6AxGvp6rl5ftasO99oL0pt7hnHtZ8Wjs/a6SHm2SogDcwRFZZT2K8HRNXpX16rYrd2guBa2X8QKBgGiLa2nrI22feCPrkqN38y1xx/EbsnbcbIWarN7Iw+YeEmOmr3ZXtFEJIZG9BnY1sAU+rZgdXCVxfWGWj7aC9LptJLmqM098gygVBphSovxlx47CZiEr1xllfW9XJOVWtZKh40tvGc9Hkxdsq7clindNvgM64smN0tHmEuJ2aP86";

    /// <summary>
    /// Gets the private key as a PKCS #1 PEM file (<c>BEGIN RSA PRIVATE KEY</c>), the text a
    /// case writes for <c>--key</c>.
    /// </summary>
    internal static string PrivateKeyPem { get; } = PemEncoding.WriteString("RSA PRIVATE KEY", Convert.FromBase64String(PrivateKeyPkcs1)) + "\n";

    /// <summary>Gets the public key blob: the key type, then e and n as mpints (RFC 4253 section 6.6).</summary>
    internal static byte[] PublicKeyBlob { get; } = CreateBlob();

    /// <summary>
    /// Gets the public key file's one line, <c>ssh-rsa &lt;base64 blob&gt;</c>, the text a case
    /// writes for <c>--pubkey</c> and the server's account holds as its authorized key.
    /// </summary>
    internal static string PublicKeyLine { get; } = KeyType + " " + Convert.ToBase64String(PublicKeyBlob);

    /// <summary>
    /// Gets whether a <c>publickey</c> request's algorithm signs with this key: <c>ssh-rsa</c>
    /// (SHA-1), <c>rsa-sha2-256</c> or <c>rsa-sha2-512</c> (RFC 8332).
    /// </summary>
    /// <param name="algorithm">The algorithm the request names.</param>
    /// <returns>Whether the algorithm is one of the three.</returns>
    internal static bool IsSignatureAlgorithm(string algorithm) => algorithm is KeyType or "rsa-sha2-256" or "rsa-sha2-512";

    /// <summary>Checks a PKCS #1 v1.5 signature made with this key under one of its algorithms.</summary>
    /// <param name="algorithm">The algorithm, one <see cref="IsSignatureAlgorithm" /> accepts.</param>
    /// <param name="signedData">The data the client signed.</param>
    /// <param name="signature">The signature's bytes.</param>
    /// <returns>Whether the signature is this key's over the data.</returns>
    internal static bool Verifies(string algorithm, ReadOnlySpan<byte> signedData, ReadOnlySpan<byte> signature)
    {
        using RSA rsa = CreateKey();
        return rsa.VerifyData(signedData, signature, HashFor(algorithm), RSASignaturePadding.Pkcs1);
    }

    /// <summary>Signs data with this key, as the client would.</summary>
    /// <param name="algorithm">The algorithm, one <see cref="IsSignatureAlgorithm" /> accepts.</param>
    /// <param name="data">The data to sign.</param>
    /// <returns>The signature blob: the algorithm's name, then the signature, both as strings.</returns>
    internal static byte[] Sign(string algorithm, ReadOnlySpan<byte> data)
    {
        using RSA rsa = CreateKey();
        SshWireWriter writer = new();
        writer.WriteString(Encoding.ASCII.GetBytes(algorithm));
        writer.WriteString(rsa.SignData(data.ToArray(), HashFor(algorithm), RSASignaturePadding.Pkcs1));
        return writer.ToArray();
    }

    private static HashAlgorithmName HashFor(string algorithm) => algorithm switch
    {
        "rsa-sha2-256" => HashAlgorithmName.SHA256,
        "rsa-sha2-512" => HashAlgorithmName.SHA512,
        _ => HashAlgorithmName.SHA1,
    };

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
