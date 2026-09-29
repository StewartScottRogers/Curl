using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Checks the <c>tls-server-end-point</c> application data curl with MIT passes over HTTPS
/// (RFC 5929 section 4.1, BL-915): the prefix and the certificate's hash in its signature's
/// algorithm, SHA-256 in place of MD5 and SHA-1, none without a certificate, and exit 91 for a
/// signature that names no hash, as curl 8.18.0 was measured failing (BL-965).
/// </summary>
[TestClass]
public sealed class TlsServerEndPointChannelBindingsTests
{
    /// <summary>The Ed25519 certificate curl 8.18.0 was measured failing with exit 91 on (BL-965), from <c>openssl req -newkey ed25519</c>.</summary>
    private const string OpenSslEd25519Certificate =
        "MIIBUTCCAQOgAwIBAgIUWYEJLSnHnOz1gOikz9rSuVlTw+cwBQYDK2VwMB4xHDAaBgNVBAMME3NlcnZlci5leGFtcGxlLnRlc3QwHhcNMjYwOTI5MjIzMTI4WhcNMjYxMDI5MjIzMTI4WjAeMRwwGgYDVQQDDBNzZXJ2ZXIuZXhhbXBsZS50ZXN0MCowBQYDK2VwAyEAiLDCn4A5yM6OePY9JUurDwaO+xbZD6onwSSCHd7tuzCjUzBRMB0GA1UdDgQWBBSSMuF5nO9118C2uVpy6MzON3BzkjAfBgNVHSMEGDAWgBSSMuF5nO9118C2uVpy6MzON3BzkjAPBgNVHRMBAf8EBTADAQH/MAUGAytlcANBAOG9X/qGx5hsHQozZZL4ori0Wr5AJ/+uEJ8uL2oNLkojIX4SLoWA+xf7pnxXdyeNedlXveWOz11FvoZTPM8bDw0=";

    /// <summary>The Ed448 certificate curl 8.18.0 was measured failing with exit 91 on (BL-965), from <c>openssl req -newkey ed448</c>.</summary>
    private const string OpenSslEd448Certificate =
        "MIIBnDCCARygAwIBAgIULkXSpg8AY/BXdAYomib1hI3Ap20wBQYDK2VxMB4xHDAaBgNVBAMME3NlcnZlci5leGFtcGxlLnRlc3QwHhcNMjYwOTI5MjIzMTI4WhcNMjYxMDI5MjIzMTI4WjAeMRwwGgYDVQQDDBNzZXJ2ZXIuZXhhbXBsZS50ZXN0MEMwBQYDK2VxAzoAZbuGsaXnbXB9ffDPKVkup86XVCIPSlAWjxROUwFpHr/v5d3Yo9EdpdsNiGRCrq/zYaxCPtWpgMaAo1MwUTAdBgNVHQ4EFgQUQecfDDyukfKRrRx5vZSXX+9rvn4wHwYDVR0jBBgwFoAUQecfDDyukfKRrRx5vZSXX+9rvn4wDwYDVR0TAQH/BAUwAwEB/zAFBgMrZXEDcwB3khrROgd2cz6JmhNbcqPYwcVJNU2BemFh0xRN5oiURs7EcZwNxUZ+JKHR7sPPebIHAf63/MK76YDElusNuwV1OuKXgsXYuk/NQSh38zaNvrvoacPcROkDzNSH3eWW9yEh1pa5MqZhh0GpRxg5vqcLFQA=";

    /// <summary>
    /// The sha224WithRSAEncryption certificate curl 8.18.0 was measured sending a Negotiate token
    /// over (BL-965), from <c>openssl req -sha224</c>; <c>openssl dgst -sha224</c> of it is
    /// <c>59abc990…9140</c>.
    /// </summary>
    private const string OpenSslSha224RsaCertificate =
        "MIIDHTCCAgWgAwIBAgIUWiN2MPfP9qPNhLYvYMhVNrW9wj8wDQYJKoZIhvcNAQEOBQAwHjEcMBoGA1UEAwwTc2VydmVyLmV4YW1wbGUudGVzdDAeFw0yNjA5MjkyMjMxMjlaFw0yNjEwMjkyMjMxMjlaMB4xHDAaBgNVBAMME3NlcnZlci5leGFtcGxlLnRlc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDaDR1/N5Tyo77OoiCktLYUXrHECo4LUiaBZATyVcSLtYsdFTYjiXiq6ssbdhH6gXDGzt7A89oMgJSwVuqdK3+qMCVK/uCE5a9l9TBdMmR2UiT/16LwES+2+N4U0PdzbmZmFN3fVKdLjzWrNaMo6Lmf5OozOBx+nFdE/9Hh9s/FsIVZzvWOnoBswo0PX1IjRZ/3jZuSWc30bm7ej9bOpIbmMbZs3c2AK9ThxAgpQkl84GxMtTYsxfePdFujVnpFlj3ea/t582ifbShr+hMG+lYdX+bW3Yf1Q7WXa7HnpRjZKmrmitkiOd/T0t2FuXM7+oDwIJ/sGIybOdgE6crlIZhBAgMBAAGjUzBRMB0GA1UdDgQWBBR9wjhzkGz+GpuxkI6rYbrWaTgTDTAfBgNVHSMEGDAWgBR9wjhzkGz+GpuxkI6rYbrWaTgTDTAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3DQEBDgUAA4IBAQCsTp4FnmgM1gOrr7BjGii4hIExFPYWqYpfB45IpgsnIoLtGNUjYbkIZFi36FDpf0Yz+ig4bgFaRg+mGv70XKDo25WDTobBnixZIKSHpDZQl8YgkTsisZHPmPTFNfWxj8Hk4H7ctgOjoEECUd8W12zcCTSoYTnlGdI4T9HrXdJjuHJONOz05fHd/BIFUQ71+FPdVhXs9/mMwa/hwtRDpyA5NJcOGd8oIHarH9/5vohNrh52VNEwjr6rMV/YwIhCBE99s7SjeyXjsLx+rOuQtFwCf1KeEe+KNKaTq07Tfax9jfMPNTVAQ7JDA8yxZSanFeo+ONllqMju7kWO5r4+jK5O";

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
        byte[] certificate = CertificateNaming("1.2.840.113549.1.1.5");

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
    public void Of_OpenSslSha224RsaCertificate_TakesSha224AsOpenSslHashesIt()
    {
        byte[] certificate = Convert.FromBase64String(OpenSslSha224RsaCertificate);

        CollectionAssert.AreEqual(Expected(Convert.FromHexString("59abc99016d35ab0ac2d8f5b6205a8746607e4d8ded9b9a1f53f9140")), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    [DataRow("1.2.840.10045.4.3.1", DisplayName = "ecdsa-with-SHA224")]
    [DataRow("2.16.840.1.101.3.4.3.1", DisplayName = "dsa-with-sha224")]
    public void Of_OtherSha224Certificate_TakesSha224(string signatureAlgorithm)
    {
        byte[] certificate = CertificateNaming(signatureAlgorithm);

        CollectionAssert.AreEqual(Expected(Sha224.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_RsaPssCertificate_FailsWithExit91AsCurlWasMeasured()
    {
        AssertFails(RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pss), TlsServerEndPointChannelBindings.NoDigestAlgorithmMessage);
    }

    [TestMethod]
    [DataRow(OpenSslEd25519Certificate, DisplayName = "Ed25519")]
    [DataRow(OpenSslEd448Certificate, DisplayName = "Ed448")]
    public void Of_EdDsaCertificate_FailsWithExit91AsCurlWasMeasured(string certificate)
    {
        AssertFails(Convert.FromBase64String(certificate), TlsServerEndPointChannelBindings.NoDigestAlgorithmMessage);
    }

    [TestMethod]
    public void Of_SignatureAlgorithmOpenSslDoesNotKnow_FailsWithExit91AndNoDigestNid()
    {
        AssertFails(CertificateNaming("1.2.840.113549.1.1.127"), TlsServerEndPointChannelBindings.NoDigestNidMessage);
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

    private static void AssertFails(byte[] certificate, string message)
    {
        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(() => TlsServerEndPointChannelBindings.Of(certificate));

        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    /// <summary>
    /// A self-signed RSA certificate's DER whose signature algorithm is
    /// <paramref name="signatureAlgorithm" />, which <see cref="CertificateRequest" /> may refuse
    /// to make itself; the signature is an RSA SHA-256 one whatever the OID says, as only the
    /// OID is read.
    /// </summary>
    private static byte[] CertificateNaming(string signatureAlgorithm)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=server.example.test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 signed = request.Create(
            request.SubjectName, new NamedSignatureGenerator(key, signatureAlgorithm), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100), [1]);
        return signed.RawData;
    }

    /// <summary>Signs with RSA SHA-256 under whatever signature algorithm OID it is given.</summary>
    private sealed class NamedSignatureGenerator(RSA key, string signatureAlgorithm) : X509SignatureGenerator
    {
        public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm)
        {
            AsnWriter writer = new(AsnEncodingRules.DER);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(signatureAlgorithm);
                writer.WriteNull();
            }

            return writer.Encode();
        }

        public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) => key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        protected override PublicKey BuildPublicKey() => X509SignatureGenerator.CreateForRSA(key, RSASignaturePadding.Pkcs1).PublicKey;
    }
}
