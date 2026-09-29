using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Checks the <c>tls-server-end-point</c> application data curl with MIT passes over HTTPS
/// (RFC 5929 section 4.1, BL-915): the prefix and the certificate's hash in its signature's
/// algorithm, SHA-256 in place of MD5 and SHA-1, and none without a certificate or for a
/// signature that names no known hash.
/// </summary>
[TestClass]
public sealed class TlsServerEndPointChannelBindingsTests
{
    private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("tls-server-end-point:");

    [TestMethod]
    public void Of_Sha256RsaCertificate_IsThePrefixAndTheCertificatesSha256()
    {
        byte[] certificate = RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        CollectionAssert.AreEqual(Expected(SHA256.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_Sha1RsaCertificate_TakesSha256()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=server.example.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 signed = request.Create(
            request.SubjectName, new Sha1RsaSignatureGenerator(key), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100), [1]);
        byte[] certificate = signed.RawData;

        CollectionAssert.AreEqual(Expected(SHA256.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_Sha384EcdsaCertificate_TakesSha384()
    {
        byte[] certificate = EcdsaCertificate(HashAlgorithmName.SHA384);

        CollectionAssert.AreEqual(Expected(SHA384.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_Sha512RsaCertificate_TakesSha512()
    {
        byte[] certificate = RsaCertificate(HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

        CollectionAssert.AreEqual(Expected(SHA512.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_RsaPssCertificate_IsNull()
    {
        Assert.IsNull(TlsServerEndPointChannelBindings.Of(RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
    }

    [TestMethod]
    public void Of_NoCertificate_IsNull()
    {
        Assert.IsNull(TlsServerEndPointChannelBindings.Of(ReadOnlyMemory<byte>.Empty));
    }

    /// <summary>A self-signed RSA certificate's DER, signed with <paramref name="hash" /> and <paramref name="padding" />.</summary>
    internal static byte[] RsaCertificate(HashAlgorithmName hash, RSASignaturePadding padding)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=server.example.test", key, hash, padding);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100));
        return certificate.RawData;
    }

    private static byte[] EcdsaCertificate(HashAlgorithmName hash)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        CertificateRequest request = new("CN=server.example.test", key, hash);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100));
        return certificate.RawData;
    }

    private static byte[] Expected(byte[] hash) => [.. Prefix, .. hash];

    /// <summary>
    /// Signs sha1WithRSAEncryption (1.2.840.113549.1.1.5), which <see cref="CertificateRequest" />
    /// refuses to make itself.
    /// </summary>
    private sealed class Sha1RsaSignatureGenerator(RSA key) : X509SignatureGenerator
    {
        private static readonly byte[] Sha1WithRsaEncryption = [0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x05, 0x05, 0x00];

        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm) => Sha1WithRsaEncryption;

        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) => key.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);

        protected override PublicKey BuildPublicKey() => X509SignatureGenerator.CreateForRSA(key, RSASignaturePadding.Pkcs1).PublicKey;
    }
}
