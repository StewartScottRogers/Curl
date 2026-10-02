using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandBuiltTlsProvider" /> with <c>--cert-status</c> and
/// <c>--ssl-auto-client-cert</c> against <see cref="Tls13TestServer" />, a copy of
/// <c>Curl.Tls.UnitTests</c>' in-memory TLS 1.3 server that staples the OCSP response
/// <see cref="OcspResponseBuilder" /> writes. The exit 91 texts are the ones every platform
/// prints (ADR-0191), so each case runs as both builds.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCertStatusAndAGoodStapledResponse_Connects(bool matchesSchannelBuild)
    {
        using var pki = new OcspTestPki();

        var (result, _) = await HandshakeWithStaplingServerAsync(pki, pki.Response(DateTimeOffset.UtcNow).Build(), matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCertStatusAndARevokedStapledResponse_FailsWithExit91AndTheReason(bool matchesSchannelBuild)
    {
        using var pki = new OcspTestPki();
        var revoked = (pki.Response(DateTimeOffset.UtcNow) with { CertStatus = OcspStapleStatus.Revoked, RevocationReason = 1 }).Build();

        var (result, plaintextDisposed) = await HandshakeWithStaplingServerAsync(pki, revoked, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Assert.AreEqual("SSL certificate revocation reason: keyCompromise (1)", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(plaintextDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCertStatusAndNoStapledResponse_FailsWithExit91(bool matchesSchannelBuild)
    {
        using var pki = new OcspTestPki();

        var (result, _) = await HandshakeWithStaplingServerAsync(pki, null, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Assert.AreEqual("No OCSP response received", result.ErrorMessage);
    }

    // -v's status line, after the handshake's own lines, as curl's verifystatus() prints it
    // (BL-875): an accepted response, a revoked certificate and an unknown one, each read.
    [TestMethod]
    [DataRow(OcspStapleStatus.Good, "SSL certificate status: good (0)", CurlExitCode.Ok)]
    [DataRow(OcspStapleStatus.Revoked, "SSL certificate status: revoked (1)", CurlExitCode.SslInvalidCertStatus)]
    [DataRow(OcspStapleStatus.Unknown, "SSL certificate status: unknown (2)", CurlExitCode.SslInvalidCertStatus)]
    public async Task AuthenticateAsClientAsync_WithCertStatus_ReportsTheCertificateStatusLine(OcspStapleStatus status, string expectedLine, CurlExitCode expectedExitCode)
    {
        using var pki = new OcspTestPki();
        var response = (pki.Response(DateTimeOffset.UtcNow) with { CertStatus = status, RevocationReason = 1 }).Build();
        var events = new RecordingTransferEvents();

        var (result, _) = await HandshakeWithStaplingServerAsync(pki, response, OpenSslBuild, events);

        Assert.AreEqual(expectedExitCode, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(expectedLine, events.Info.Single());
        Assert.HasCount(status == OcspStapleStatus.Good ? 1 : 0, events.Handshakes);
        if (result.Connection is { } connection)
        {
            await connection.DisposeAsync();
        }
    }

    // curl stops before the status line for a response it never found the certificate in.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusAndNoStapledResponse_ReportsNoStatusLine()
    {
        using var pki = new OcspTestPki();
        var events = new RecordingTransferEvents();

        var (result, _) = await HandshakeWithStaplingServerAsync(pki, null, SchannelBuild, events);

        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Assert.IsEmpty(events.Info);
    }

    // The TLS 1.2 client asks too: a server-side SslStream staples nothing.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusOverTls12AndNoStapledResponse_FailsWithExit91()
    {
        var provider = Provider(Tls12Only(new TlsClientOptions(Insecure: true, RequireCertificateStatus: true)), OpenSslBuild);

        var (result, plaintextDisposed) = await HandshakeAsync(provider, CertificateHost);

        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Assert.AreEqual("No OCSP response received", result.ErrorMessage);
        Assert.IsTrue(plaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAutoClientCertificate_PresentsTheCertificateFromThePersonalStore()
    {
        using var pki = new OcspTestPki();
        var store = new FakeClientCertificateStore
        {
            Certificates = [X509CertificateLoader.LoadPkcs12(
                s_clientCertificate.Export(X509ContentType.Pkcs12), null)],
        };
        var testServer = new Tls13TestServer(pki.LeafCredential) { RequestClientCertificate = true };
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true, AutoClientCertificate: true), OpenSslBuild, TimeProvider.System, store, SystemTlsRandomSource.Instance);

        var (result, _) = await HandshakeWithTestServerAsync(provider, testServer);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(s_clientCertificate.RawData, testServer.ClientCertificates.Single());
        CollectionAssert.AreEqual(new[] { (ClientCertificateStoreLocation.CurrentUser, "MY") }, store.Opened);
        await result.Connection!.DisposeAsync();
    }

    // A server that sends the leaf and its CA, stapling the response to the leaf unless it is null.
    private static Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeWithStaplingServerAsync(
        OcspTestPki pki,
        byte[]? response,
        bool matchesSchannelBuild,
        RecordingTransferEvents? events = null)
    {
        var testServer = new Tls13TestServer(pki.LeafCredential)
        {
            IssuerCertificates = [pki.Ca.RawData],
            LeafExtensions = response is null ? [] : [StatusRequestExtension.EncodeOcspResponse(response)],
        };
        return HandshakeWithTestServerAsync(
            Provider(new TlsClientOptions(Insecure: true, RequireCertificateStatus: true), matchesSchannelBuild), testServer, events);
    }

    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeWithTestServerAsync(
        HandBuiltTlsProvider provider,
        Tls13TestServer testServer,
        RecordingTransferEvents? events = null)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(() => new Tls13RecordTestServer(server, testServer).HandshakeAsync());

        var plaintext = new StreamConnection(client, ServerEndPoint);
        var result = events is null
            ? await provider.AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None)
            : await provider.AuthenticateAsClientAsync(plaintext, CertificateHost, events, CancellationToken.None);

        var plaintextDisposed = client.IsDisposed;
        if (result.Connection is null)
        {
            await server.DisposeAsync();
        }

        await IgnoreServerFailureAsync(serverTask);
        return (result, plaintextDisposed);
    }

    // The server's side fails when the client refuses the handshake and closes.
    private static async Task IgnoreServerFailureAsync(Task serverTask)
    {
        try
        {
            await serverTask;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or EndOfStreamException or InvalidOperationException or TlsAlertException)
        {
            // Expected once the client has refused the handshake.
        }
    }
}
