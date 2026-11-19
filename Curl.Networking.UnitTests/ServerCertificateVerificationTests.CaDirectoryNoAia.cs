using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that a CA directory (<c>--capath</c>, <c>--proxy-capath</c>, <c>SSL_CERT_DIR</c>) never
/// downloads a missing intermediate through the leaf's AIA link, as OpenSSL curl does not (BL-5448).
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationCaDirectoryNoAiaTests
{
    private const string Host = "capath-noaia.test";

    // One instant for every certificate of the chain: reading the clock per certificate let an
    // intermediate outlive its issuer when the second changed between the two reads (BL-5521).
    private static readonly DateTimeOffset s_certificateEpoch = DateTimeOffset.UtcNow;

    [TestMethod]
    public void VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "curl-capath-aia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var aiaServer = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            aiaServer.Start();
            var port = ((IPEndPoint)aiaServer.LocalEndpoint).Port;
            using var root = CreateCa("CN=Aia Root", null);
            using var intermediate = CreateCa("CN=Aia Intermediate", root);
            using var leaf = CreateLeaf(intermediate, port);
            File.WriteAllText(Path.Combine(directory, $"{OpenSslSubjectNameHash.Of(root.SubjectName)}.0"), root.ExportCertificatePem());
            var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateDirectory: directory), false, TimeProvider.System);
            var (_, anchors, _) = verification.ReadTrustAnchors();
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.Build(leaf);

            var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors);

            Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
            Assert.IsFalse(aiaServer.Pending());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static X509Certificate2 CreateCa(string subject, X509Certificate2? issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var notBefore = s_certificateEpoch.AddDays(-2);
        var notAfter = s_certificateEpoch.AddDays(30);
        if (issuer is null)
        {
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        using var certificate = request.Create(issuer, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
        return certificate.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, int aiaPort)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        request.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(
            null, [$"http://127.0.0.1:{aiaPort}/int.der"]));
        return request.Create(issuer, s_certificateEpoch.AddDays(-1), s_certificateEpoch.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
