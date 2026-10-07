using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the <see cref="ConnectResult.PeerCertificates" /> a successful handshake reports:
/// the server's certificate first, then the others it sent, in the order sent (ADR-0054).
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

    private static byte[] CreateUnrelatedAuthorityDer()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = CreateAuthority("CN=Unrelated", key, null, null);
        return authority.RawData;
    }
}
