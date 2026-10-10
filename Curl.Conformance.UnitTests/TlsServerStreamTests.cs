using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance;

/// <summary>
/// Pins that <see cref="TlsServerStream"/> completes a TLS handshake with an <see cref="SslStream"/>
/// client over an <see cref="InMemoryDuplexStream"/> pair, and carries out its ALPN and
/// client-certificate options.
/// </summary>
[TestClass]
public sealed class TlsServerStreamTests
{
    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_CompletesHandshakeAndCarriesBytesBothWays()
    {
        using X509Certificate2 certificate = CreateCertificate("localhost");
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);

        (SslStream server, SslStream client) = await HandshakeAsync(serverEnd, clientEnd, options, [], clientCertificate: null);
        await using (server)
        await using (client)
        {
            await client.WriteAsync("ping"u8.ToArray());
            byte[] serverBuffer = new byte[4];
            await server.ReadExactlyAsync(serverBuffer);
            await server.WriteAsync("pong"u8.ToArray());
            byte[] clientBuffer = new byte[4];
            await client.ReadExactlyAsync(clientBuffer);

            Assert.AreEqual("ping", System.Text.Encoding.ASCII.GetString(serverBuffer));
            Assert.AreEqual("pong", System.Text.Encoding.ASCII.GetString(clientBuffer));
            Assert.AreEqual(default, server.NegotiatedApplicationProtocol);
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_WithApplicationProtocols_NegotiatesTheOneTheClientShares()
    {
        using X509Certificate2 certificate = CreateCertificate("localhost");
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        var options = new TlsServerOptions(certificate, ["h2", "http/1.1"], RequestClientCertificate: false);

        (SslStream server, SslStream client) = await HandshakeAsync(serverEnd, clientEnd, options, [SslApplicationProtocol.Http11], clientCertificate: null);
        await using (server)
        await using (client)
        {
            Assert.AreEqual(SslApplicationProtocol.Http11, server.NegotiatedApplicationProtocol);
            Assert.AreEqual(SslApplicationProtocol.Http11, client.NegotiatedApplicationProtocol);
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_RequestingClientCertificate_ReceivesTheClientsCertificate()
    {
        using X509Certificate2 certificate = CreateCertificate("localhost");
        using X509Certificate2 clientCertificate = CreateCertificate("client");
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: true);

        (SslStream server, SslStream client) = await HandshakeAsync(serverEnd, clientEnd, options, [], clientCertificate);
        await using (server)
        await using (client)
        {
            Assert.IsNotNull(server.RemoteCertificate);
            Assert.AreEqual(clientCertificate.Thumbprint, X509CertificateLoader.LoadCertificate(server.RemoteCertificate.GetRawCertData()).Thumbprint);
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_NotRequestingClientCertificate_ReceivesNone()
    {
        using X509Certificate2 certificate = CreateCertificate("localhost");
        using X509Certificate2 clientCertificate = CreateCertificate("client");
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);

        (SslStream server, SslStream client) = await HandshakeAsync(serverEnd, clientEnd, options, [], clientCertificate);
        await using (server)
        await using (client)
        {
            Assert.IsNull(server.RemoteCertificate);
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_ClientGoneBeforeHandshake_ThrowsAndDisposesInnerStream()
    {
        using X509Certificate2 certificate = CreateCertificate("localhost");
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        await clientEnd.DisposeAsync();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);

        await Assert.ThrowsAsync<Exception>(() => TlsServerStream.AuthenticateAsync(serverEnd, options, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await serverEnd.WriteAsync("x"u8.ToArray()));
    }

    private static async Task<(SslStream Server, SslStream Client)> HandshakeAsync(
        Stream serverEnd,
        Stream clientEnd,
        TlsServerOptions options,
        List<SslApplicationProtocol> clientProtocols,
        X509Certificate2? clientCertificate)
    {
        var client = new SslStream(clientEnd, leaveInnerStreamOpen: false);
        var clientOptions = new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            RemoteCertificateValidationCallback = static (_, _, _, _) => true,
            ApplicationProtocols = clientProtocols.Count == 0 ? null : clientProtocols,
            LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCertificate!,
        };
        Task<SslStream> server = TlsServerStream.AuthenticateAsync(serverEnd, options, CancellationToken.None);
        await Task.WhenAll(server, client.AuthenticateAsClientAsync(clientOptions));
        return (await server, client);
    }

    /// <summary>
    /// A self-signed RSA certificate reloaded through PKCS#12, since Windows Schannel and macOS
    /// reject a server certificate whose key is ephemeral.
    /// </summary>
    private static X509Certificate2 CreateCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={subjectName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName(subjectName);
        request.CertificateExtensions.Add(alternativeNames.Build());
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }
}
