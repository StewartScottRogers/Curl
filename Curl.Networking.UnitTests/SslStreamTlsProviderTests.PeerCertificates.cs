using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the <see cref="ConnectResult.PeerCertificates" /> a successful handshake reports:
/// the server's certificate first, then the others it sent, in the order sent (ADR-0053).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerSendsOnlyItsOwnCertificate_ReportsIt()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(1, result.PeerCertificates);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, result.PeerCertificates[0].ToArray());
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate()
    {
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var root = CreateAuthority("CN=BL303 Test Root", rootKey, null, null);
        using var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var intermediate = CreateAuthority("CN=BL303 Intermediate", intermediateKey, root, rootKey);
        using var leaf = CreateServerCertificate(intermediate, intermediateKey);
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificateContext = context });
            _ = await sslStream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
        });
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(2, result.PeerCertificates);
        CollectionAssert.AreEqual(leaf.RawData, result.PeerCertificates[0].ToArray());
        CollectionAssert.AreEqual(intermediate.RawData, result.PeerCertificates[1].ToArray());
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public void ListPeerCertificates_WithNoCertificate_ReturnsNone()
    {
        Assert.IsEmpty(SslStreamTlsProvider.ListPeerCertificates(null, null));
    }

    [TestMethod]
    public void ListPeerCertificates_WithNoChain_ReturnsTheServersCertificateAlone()
    {
        var listed = SslStreamTlsProvider.ListPeerCertificates(s_serverCertificate, null);

        Assert.HasCount(1, listed);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, listed[0].ToArray());
    }

    [TestMethod]
    public void ListPeerCertificates_WithTheServersCertificateAlsoInTheExtraStore_ListsItOnceAndFirst()
    {
        using var other = X509CertificateLoader.LoadCertificate(CreateUnrelatedAuthorityDer());
        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(other);
        chain.ChainPolicy.ExtraStore.Add(s_serverCertificate);

        var listed = SslStreamTlsProvider.ListPeerCertificates(s_serverCertificate, chain);

        Assert.HasCount(2, listed);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, listed[0].ToArray());
        CollectionAssert.AreEqual(other.RawData, listed[1].ToArray());
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

    // Reloaded from PKCS#12, as the class's own server certificate is, so the platform can
    // use its key in a handshake.
    private static X509Certificate2 CreateServerCertificate(X509Certificate2 issuer, ECDsa issuerKey)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(CertificateHost);
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

    private static byte[] CreateUnrelatedAuthorityDer()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = CreateAuthority("CN=Unrelated", key, null, null);
        return authority.RawData;
    }
}
