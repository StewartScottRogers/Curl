using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Checks the <c>tls-server-end-point</c> application data curl with MIT passes over HTTPS
/// (RFC 5929 section 4.1, BL-915): the prefix and the certificate's hash in its signature's
/// algorithm, SHA-256 in place of MD5 and SHA-1, none without a certificate, and exit 91 for a
/// signature that names no hash, as curl 8.18.0 was measured failing (BL-965), and SHA-3 for a
/// SHA-3 signature, as it was measured accepting (BL-980).
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

    /// <summary>
    /// The RSA-SHA3-256 certificate curl 8.18.0 was measured sending a Negotiate token over
    /// (BL-980), from <c>openssl req -sha3-256</c>.
    /// </summary>
    private const string OpenSslSha3_256RsaCertificate =
        "MIIDHTCCAgWgAwIBAgIUMg92DXixtcPC9NBkOIinYd7Iy80wDQYJYIZIAWUDBAMOBQAwHjEcMBoGA1UEAwwTc2VydmVyLmV4YW1wbGUudGVzdDAeFw0yNjA5MjkyMzIyNDRaFw0yNjEwMjkyMzIyNDRaMB4xHDAaBgNVBAMME3NlcnZlci5leGFtcGxlLnRlc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCd435yId+bVtIX7t3bQHWYCt1UnrJgPXvguuQO42qVg2zoNP+M9hQH0fDUveXtHcsHBpDXjQEiM8TU6FBFxTZe83/yIrttAsQbMfOzch6bOKQKpljn9I+wb1eCUZkvdYMKWhX2upX89j4e7NLtIWuf/xdTapDIfmC17YM4Ry9m36MtwNkll3JWcw+R3CBTURyI3SiQ9WE2R/KkKhdB+VEI1nu5VmAzOIErsmqDFwsbuR/Y6DtwJz9CHs+XXvOSdACqH0WVxmXeheFNJXYoeAUtJpxttO7naWJ6iWxoi9OdCFx9/cLBQ4KM3SJZEbySCeKgfruAJQbfiPu02lGDn7ZTAgMBAAGjUzBRMB0GA1UdDgQWBBQhEqO6QfNgsWhUaFdchdkLeMo8bDAfBgNVHSMEGDAWgBQhEqO6QfNgsWhUaFdchdkLeMo8bDAPBgNVHRMBAf8EBTADAQH/MA0GCWCGSAFlAwQDDgUAA4IBAQCItppssHa2BW16MUVDJ85jzLscHlhGBuimoZsoZacwHkwuy52z4XjMxw4Ub20oIsoV+QZr6Kzm8mSVDtJ0wiD9ytChIdcla97Si6fz2BvtA5lgJKlNOzjLyEEYHQ0flymQFcBZFGUzmG7lkGnnAc08OKTIDgwEGh1HdyDalpZ/quAoCZVNPzcWxxOGXqqcqStEgk1gOo9kxugaJbXrcJwH4Pjn8siExwx+41tRdEtoO8VeAq4ulfLXAmi6U1elTcawEFGcPCp9bANP7X0x49tqkVh5SMt0b5jNlG898qGqHiYZ5PXXoA5jVYEnFK4/kNFbhMajou5gnpL8QLU83zW0";

    /// <summary>
    /// The ecdsa_with_SHA3-512 certificate curl 8.18.0 was measured sending a Negotiate token
    /// over (BL-980), from <c>openssl req -newkey ec -sha3-512</c>.
    /// </summary>
    private const string OpenSslSha3_512EcdsaCertificate =
        "MIIBkzCCATigAwIBAgIUWxeR5usPngqVU0a19su6GkOXnvYwCwYJYIZIAWUDBAMMMB4xHDAaBgNVBAMME3NlcnZlci5leGFtcGxlLnRlc3QwHhcNMjYwOTI5MjMyMjQ0WhcNMjYxMDI5MjMyMjQ0WjAeMRwwGgYDVQQDDBNzZXJ2ZXIuZXhhbXBsZS50ZXN0MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE+QWU5ca2Crub0W1HnIYPj0S5NuCuQz1pURpqtwbhtyUo9F+MET6o+9VvJNVk2ITkE8EhlS8R+oGggs0MuX2ZrqNTMFEwHQYDVR0OBBYEFI4QgSATdUr6TL9ZCvE+YrEiWmX4MB8GA1UdIwQYMBaAFI4QgSATdUr6TL9ZCvE+YrEiWmX4MA8GA1UdEwEB/wQFMAMBAf8wCwYJYIZIAWUDBAMMA0gAMEUCIQCbCjQtmx/uqVh5BnLALASO++ZmOmtamOlvAiDYkNJKcgIgXa7GZ3mNZziFH0F1LMmMDyr9KHQlnxZhTejvPP5xmTA=";

    private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("tls-server-end-point:");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Of_Sha256RsaCertificate_IsThePrefixAndTheCertificatesSha256()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature hash", "SHA256 with PKCS#1 padding");
        byte[] certificate = RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(SHA256.HashData(certificate)), binding ?? []);
        CollectionAssert.AreEqual(Expected(SHA256.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_Sha1RsaCertificate_TakesSha256()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature algorithm", "1.2.840.113549.1.1.5");
        byte[] certificate = CertificateNaming("1.2.840.113549.1.1.5");
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(SHA256.HashData(certificate)), binding ?? []);
        CollectionAssert.AreEqual(Expected(SHA256.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_Sha384EcdsaCertificate_TakesSha384()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature hash", "SHA384 with ECDSA P-384");
        byte[] certificate = EcdsaCertificate(HashAlgorithmName.SHA384);
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(SHA384.HashData(certificate)), binding ?? []);
        CollectionAssert.AreEqual(Expected(SHA384.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_Sha512RsaCertificate_TakesSha512()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature hash", "SHA512 with PKCS#1 padding");
        byte[] certificate = RsaCertificate(HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(SHA512.HashData(certificate)), binding ?? []);
        CollectionAssert.AreEqual(Expected(SHA512.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_OpenSslSha224RsaCertificate_TakesSha224AsOpenSslHashesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("openssl sha224 digest", "59abc99016d35ab0ac2d8f5b6205a8746607e4d8ded9b9a1f53f9140");
        byte[] certificate = Convert.FromBase64String(OpenSslSha224RsaCertificate);
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(Convert.FromHexString("59abc99016d35ab0ac2d8f5b6205a8746607e4d8ded9b9a1f53f9140")), binding ?? []);
        CollectionAssert.AreEqual(Expected(Convert.FromHexString("59abc99016d35ab0ac2d8f5b6205a8746607e4d8ded9b9a1f53f9140")), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    [DataRow("1.2.840.10045.4.3.1", DisplayName = "ecdsa-with-SHA224")]
    [DataRow("2.16.840.1.101.3.4.3.1", DisplayName = "dsa-with-sha224")]
    public void Of_OtherSha224Certificate_TakesSha224(string signatureAlgorithm)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature algorithm", signatureAlgorithm);
        byte[] certificate = CertificateNaming(signatureAlgorithm);
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(Sha224.HashData(certificate)), binding ?? []);
        CollectionAssert.AreEqual(Expected(Sha224.HashData(certificate)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    [DataRow(OpenSslSha3_256RsaCertificate, "c5829afc46088a28ed3cbc7693af23672708c8dec95a4ebad03e1428cfb44217", DisplayName = "RSA-SHA3-256")]
    [DataRow(
        OpenSslSha3_512EcdsaCertificate,
        "2179cd4b8b0f6f2159fee5709456a18b7c88ebafaab241b5546edc3720c6a4b298503a26a1cbdd223e370851175c1ef3fa34929eb630ebdbf922c0f678ecdae6",
        DisplayName = "ecdsa_with_SHA3-512")]
    public void Of_OpenSslSha3Certificate_TakesItsSha3AsOpenSslHashesIt(string certificate, string expectedHash)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("certificate base64", certificate);
        diagnostics.Arrange("expected hash", expectedHash);
        diagnostics.Bytes("certificate", Convert.FromBase64String(certificate));

        byte[]? binding = TlsServerEndPointChannelBindings.Of(Convert.FromBase64String(certificate));

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(Convert.FromHexString(expectedHash)), binding ?? []);
        CollectionAssert.AreEqual(Expected(Convert.FromHexString(expectedHash)), TlsServerEndPointChannelBindings.Of(Convert.FromBase64String(certificate)));
    }

    [TestMethod]
    [DataRow("2.16.840.1.101.3.4.3.9", 224, DisplayName = "id-ecdsa-with-sha3-224")]
    [DataRow("2.16.840.1.101.3.4.3.10", 256, DisplayName = "id-ecdsa-with-sha3-256")]
    [DataRow("2.16.840.1.101.3.4.3.11", 384, DisplayName = "id-ecdsa-with-sha3-384")]
    [DataRow("2.16.840.1.101.3.4.3.12", 512, DisplayName = "id-ecdsa-with-sha3-512")]
    [DataRow("2.16.840.1.101.3.4.3.13", 224, DisplayName = "id-rsassa-pkcs1-v1_5-with-sha3-224")]
    [DataRow("2.16.840.1.101.3.4.3.14", 256, DisplayName = "id-rsassa-pkcs1-v1_5-with-sha3-256")]
    [DataRow("2.16.840.1.101.3.4.3.15", 384, DisplayName = "id-rsassa-pkcs1-v1_5-with-sha3-384")]
    [DataRow("2.16.840.1.101.3.4.3.16", 512, DisplayName = "id-rsassa-pkcs1-v1_5-with-sha3-512")]
    public void Of_Sha3Certificate_TakesThatSha3(string signatureAlgorithm, int bits)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature algorithm", signatureAlgorithm);
        diagnostics.Arrange("bits", bits);
        byte[] certificate = CertificateNaming(signatureAlgorithm);
        diagnostics.Bytes("certificate", certificate);

        byte[]? binding = TlsServerEndPointChannelBindings.Of(certificate);

        diagnostics.Bytes("binding", binding ?? []);
        diagnostics.Act("binding length", binding?.Length);
        diagnostics.Diff("binding", Expected(Sha3Of(certificate, bits)), binding ?? []);
        CollectionAssert.AreEqual(Expected(Sha3Of(certificate, bits)), TlsServerEndPointChannelBindings.Of(certificate));
    }

    [TestMethod]
    public void Of_RsaPssCertificate_FailsWithExit91AsCurlWasMeasured()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature", "SHA256 with PSS padding");
        AssertFails(diagnostics, RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pss), TlsServerEndPointChannelBindings.NoDigestAlgorithmMessage);
    }

    [TestMethod]
    [DataRow(OpenSslEd25519Certificate, DisplayName = "Ed25519")]
    [DataRow(OpenSslEd448Certificate, DisplayName = "Ed448")]
    public void Of_EdDsaCertificate_FailsWithExit91AsCurlWasMeasured(string certificate)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("certificate base64", certificate);
        AssertFails(diagnostics, Convert.FromBase64String(certificate), TlsServerEndPointChannelBindings.NoDigestAlgorithmMessage);
    }

    [TestMethod]
    public void Of_SignatureAlgorithmOpenSslDoesNotKnow_FailsWithExit91AndNoDigestNid()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("signature algorithm", "1.2.840.113549.1.1.127");
        AssertFails(diagnostics, CertificateNaming("1.2.840.113549.1.1.127"), TlsServerEndPointChannelBindings.NoDigestNidMessage);
    }

    [TestMethod]
    public void Of_NoCertificate_IsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("certificate", "empty");

        byte[]? binding = TlsServerEndPointChannelBindings.Of(ReadOnlyMemory<byte>.Empty);

        diagnostics.Act("binding", binding);
        diagnostics.Assert("binding", null, binding);
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

    private static byte[] Sha3Of(byte[] certificate, int bits)
    {
        byte[] hash = new byte[bits / 8];
        switch (bits)
        {
            case 224: Sha3.HashData224(certificate, hash); break;
            case 256: Sha3.HashData256(certificate, hash); break;
            case 384: Sha3.HashData384(certificate, hash); break;
            default: Sha3.HashData512(certificate, hash); break;
        }

        return hash;
    }

    private static void AssertFails(TestDiagnostics diagnostics, byte[] certificate, string message)
    {
        diagnostics.Bytes("certificate", certificate);
        HttpAuthenticationFailedException failure = Assert.ThrowsExactly<HttpAuthenticationFailedException>(() => TlsServerEndPointChannelBindings.Of(certificate));

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.SslInvalidCertStatus, failure.ExitCode);
        diagnostics.Diff("message", message, failure.Message);
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
