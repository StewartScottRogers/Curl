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
        var options = new TlsClientOptions(Insecure: true);
        var provider = new SslStreamTlsProvider(options);
        ArrangeOptions(options);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActResult(result);
        Diagnostics.Act("peer certificate count", result.PeerCertificates.Count);
        Diagnostics.Assert("peer certificate count", 1, result.PeerCertificates.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(1, result.PeerCertificates);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, result.PeerCertificates[0].ToArray());
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public void ListPeerCertificates_WithNoCertificate_ReturnsNone()
    {
        Diagnostics.Arrange("server certificate", "none");
        Diagnostics.Arrange("chain", "none");

        var listed = SslStreamTlsProvider.ListPeerCertificates(null, null);

        Diagnostics.Act("listed count", listed.Length);
        Diagnostics.Assert("listed count", 0, listed.Length);
        Assert.IsEmpty(listed);
    }

    [TestMethod]
    public void ListPeerCertificates_WithNoChain_ReturnsTheServersCertificateAlone()
    {
        ArrangeCertificate("server certificate", s_serverCertificate);
        Diagnostics.Arrange("chain", "none");

        var listed = SslStreamTlsProvider.ListPeerCertificates(s_serverCertificate, null);

        Diagnostics.Act("listed count", listed.Length);
        Diagnostics.Assert("listed count", 1, listed.Length);
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
        ArrangeCertificate("server certificate", s_serverCertificate);
        ArrangeCertificate("extra store", other);
        Diagnostics.Arrange("extra store", "and the server certificate, unbuilt chain");

        var listed = SslStreamTlsProvider.ListPeerCertificates(s_serverCertificate, chain);

        Diagnostics.Act("listed count", listed.Length);
        Diagnostics.Assert("listed count", 2, listed.Length);
        Assert.HasCount(2, listed);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, listed[0].ToArray());
        CollectionAssert.AreEqual(other.RawData, listed[1].ToArray());
    }

    private void ArrangeCertificate(string label, X509Certificate2 certificate) =>
        Diagnostics.Arrange(label, $"{certificate.Subject} {certificate.Thumbprint}");

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
