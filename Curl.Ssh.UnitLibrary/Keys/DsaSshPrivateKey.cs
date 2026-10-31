using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Ssh.Transport;

namespace Curl.Ssh.Keys;

/// <summary>
/// A DSA user key: <c>ssh-dss</c> blobs and signatures (RFC 4253 section 6.6), r and s over
/// SHA-1, signed with the hand-built <see cref="DsaSignature" /> so the key works on every
/// platform.
/// </summary>
internal sealed class DsaSshPrivateKey : SshPrivateKey
{
    /// <summary>The key type.</summary>
    internal const string DsaKeyType = "ssh-dss";

    private readonly byte[] prime;

    private readonly byte[] subprime;

    private readonly byte[] generator;

    private readonly byte[] privateKey;

    private DsaSshPrivateKey(byte[] prime, byte[] subprime, byte[] generator, byte[] publicKey, byte[] privateKey)
    {
        this.prime = prime;
        this.subprime = subprime;
        this.generator = generator;
        this.privateKey = privateKey;
        SshWireWriter blob = new();
        blob.WriteString(Encoding.ASCII.GetBytes(DsaKeyType));
        blob.WriteMpint(prime);
        blob.WriteMpint(subprime);
        blob.WriteMpint(generator);
        blob.WriteMpint(publicKey);
        PublicKeyBlob = blob.ToArray();
    }

    /// <inheritdoc />
    internal override string KeyType => DsaKeyType;

    /// <inheritdoc />
    internal override byte[] PublicKeyBlob { get; }

    /// <summary>
    /// Builds the key from its domain parameters, y and x, each unsigned big-endian.
    /// </summary>
    /// <param name="prime">p.</param>
    /// <param name="subprime">q.</param>
    /// <param name="generator">g.</param>
    /// <param name="publicKey">y, or <see langword="null" /> to compute it as g^x mod p, as PKCS #8 leaves it out.</param>
    /// <param name="privateKey">x.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException">The parameters or x are outside what <see cref="DsaSignature" /> accepts.</exception>
    internal static DsaSshPrivateKey Create(byte[] prime, byte[] subprime, byte[] generator, byte[]? publicKey, byte[] privateKey)
    {
        using DsaSignature check = new(prime, subprime, generator, privateKey);
        byte[] y = publicKey ?? ComputePublicKey(check);
        return new DsaSshPrivateKey(prime, subprime, generator, y, privateKey);
    }

    /// <inheritdoc />
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        using DsaSignature dsa = new(prime, subprime, generator, privateKey);
        byte[] signature = new byte[dsa.SignatureLength];
        dsa.SignHash(SHA1.HashData(data), HashAlgorithmName.SHA1, signature);
        return signature;
    }

    /// <summary>y = g^x mod p by <see cref="DsaSignature" />'s constant-time exponentiation, never <c>BigInteger</c> on x (AF-0197).</summary>
    private static byte[] ComputePublicKey(DsaSignature key)
    {
        byte[] y = new byte[key.PublicKeyLength];
        key.ExportPublicKey(y);
        return y;
    }
}
