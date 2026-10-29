using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// CGR-227 (BL-3756): curl 8.18.0's OpenSSL build names an IPv4-mapped IPv6 target
/// without brackets in the certificate-mismatch message, while other IPv6 targets keep them.
/// </summary>
[TestClass]
public sealed class Cgr227Tests
{
    [TestMethod]
    [DataRow("::ffff:127.0.0.1", "ipv6 address '::ffff:127.0.0.1'")]
    [DataRow("fd00::1", "ipv6 address '[fd00::1]'")]
    public void OpenSslPeerFailedVerification_WithAnIpv6TargetAndIpAlternativeNames_BracketsOnlyAnUnmappedAddress(
        string targetHost,
        string target)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Parse("10.9.9.9"));
        request.CertificateExtensions.Add(names.Build());
        using var leaf = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf);

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, chain, targetHost);

        Assert.AreEqual($"SSL: no alternative certificate subject name matches target {target}", message);
    }
}
