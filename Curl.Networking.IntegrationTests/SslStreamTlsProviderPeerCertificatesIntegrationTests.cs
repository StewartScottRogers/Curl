using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

[TestClass]
public sealed class SslStreamTlsProviderPeerCertificatesIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate()
    {
        var runName = Guid.NewGuid().ToString("N");
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var root = CreateAuthority($"CN=BL303 Test Root {runName}", rootKey, null, null);
        using var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var intermediate = CreateAuthority($"CN=BL303 Intermediate {runName}", intermediateKey, root, rootKey);
        using var leaf = CreateServerCertificate(intermediate, intermediateKey);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var serverEndPoint = (IPEndPoint)listener.LocalEndpoint;
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(serverEndPoint, CancellationToken.None);
            using var accepted = await listener.AcceptTcpClientAsync();
            var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);

            var serverTask = Task.Run(async () =>
            {
                await using var sslStream = new SslStream(accepted.GetStream());
                await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificateContext = context });
                _ = await sslStream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
            });
            var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));
            var result = await provider.AuthenticateAsClientAsync(
                new StreamConnection(client.GetStream(), serverEndPoint, client.Client.LocalEndPoint),
                "localhost",
                CancellationToken.None);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
            Assert.HasCount(2, result.PeerCertificates);
            CollectionAssert.AreEqual(leaf.RawData, result.PeerCertificates[0].ToArray());
            CollectionAssert.AreEqual(intermediate.RawData, result.PeerCertificates[1].ToArray());
            await result.Connection!.DisposeAsync();
            await serverTask;
        }
        finally
        {
            RemoveFromCurrentUserCaStore(intermediate);
        }
    }

    private static void RemoveFromCurrentUserCaStore(X509Certificate2 intermediate)
    {
        using var store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Remove(intermediate);
    }

    private static X509Certificate2 CreateAuthority(string subject, ECDsa key, X509Certificate2? issuer, ECDsa? issuerKey)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        var notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(2);
        if (issuer is null)
        {
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        using var signed = request.Create(issuer.SubjectName, X509SignatureGenerator.CreateForECDsa(issuerKey!), notBefore, notAfter, [2]);
        return signed.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateServerCertificate(X509Certificate2 issuer, ECDsa issuerKey)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var signed = request.Create(
            issuer.SubjectName,
            X509SignatureGenerator.CreateForECDsa(issuerKey),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            [3]);
        using var withKey = signed.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
    }
}
