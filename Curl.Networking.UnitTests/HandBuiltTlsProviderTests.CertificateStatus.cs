using System.Security.Authentication;
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
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, RequireCertificateStatus: true");
        Diagnostics.Arrange("server", "TLS 1.3 staples a good OCSP response");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithStaplingServerAsync(pki, pki.Response(DateTimeOffset.UtcNow).Build(), matchesSchannelBuild);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
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
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, RequireCertificateStatus: true");
        Diagnostics.Arrange("server", "TLS 1.3 staples a revoked OCSP response, reason 1");
        Diagnostics.Bytes("stapled response", revoked);

        ConnectResult result;
        bool plaintextDisposed;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, plaintextDisposed) = await HandshakeWithStaplingServerAsync(pki, revoked, matchesSchannelBuild);
        }

        ActConnectResult(result);
        Diagnostics.Act("plaintext disposed", plaintextDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Diagnostics.Assert("error message", "SSL certificate revocation reason: keyCompromise (1)", result.ErrorMessage);
        Diagnostics.Assert("connection is null", true, result.Connection is null);
        Diagnostics.Assert("plaintext disposed", true, plaintextDisposed);
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
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, RequireCertificateStatus: true");
        Diagnostics.Arrange("server", "TLS 1.3 staples nothing");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithStaplingServerAsync(pki, null, matchesSchannelBuild);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Diagnostics.Assert("error message", "No OCSP response received", result.ErrorMessage);
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
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("stapled status", status);
        Diagnostics.Arrange("expected status line", expectedLine);
        Diagnostics.Arrange("expected exit code", expectedExitCode);
        Diagnostics.Bytes("stapled response", response);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithStaplingServerAsync(pki, response, OpenSslBuild, events);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Act("handshake events", events.Handshakes.Count);
        Diagnostics.Assert("exit code", expectedExitCode, result.ExitCode);
        Assert.AreEqual(expectedExitCode, result.ExitCode, result.ErrorMessage);
        Diagnostics.Assert("status line", expectedLine, string.Join(" | ", events.Info));
        Assert.AreEqual(expectedLine, events.Info.Single());
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("handshake failed", status != OcspStapleStatus.Good, handshake.Failed);
        Diagnostics.Assert("protocol version", SslProtocols.Tls13, handshake.ProtocolVersion);
        Assert.AreEqual(status != OcspStapleStatus.Good, handshake.Failed);
        Assert.AreEqual(SslProtocols.Tls13, handshake.ProtocolVersion);
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
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("options", "Insecure: true, RequireCertificateStatus: true");
        Diagnostics.Arrange("server", "TLS 1.3 staples nothing");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithStaplingServerAsync(pki, null, SchannelBuild, events);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", events.Info.Count);
        Diagnostics.Assert("exit code", CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Diagnostics.Assert("info lines", 0, events.Info.Count);
        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Assert.IsEmpty(events.Info);
    }

    // The TLS 1.2 client asks too: a server-side SslStream staples nothing.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusOverTls12AndNoStapledResponse_FailsWithExit91()
    {
        var provider = Provider(Tls12Only(new TlsClientOptions(Insecure: true, RequireCertificateStatus: true)), OpenSslBuild);
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, RequireCertificateStatus: true, MaximumVersion: Tls12");
        Diagnostics.Arrange("server", "TLS 1.2 SslStream staples nothing");
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        bool plaintextDisposed;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, plaintextDisposed) = await HandshakeAsync(provider, CertificateHost);
        }

        ActConnectResult(result);
        Diagnostics.Act("plaintext disposed", plaintextDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslInvalidCertStatus, result.ExitCode);
        Diagnostics.Assert("error message", "No OCSP response received", result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, plaintextDisposed);
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
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, AutoClientCertificate: true");
        Diagnostics.Arrange("server", "TLS 1.3 requests a client certificate");
        ArrangeCertificate("personal store certificate", s_clientCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithTestServerAsync(provider, testServer);
        }

        ActConnectResult(result);
        Diagnostics.Act("client certificates the server saw", testServer.ClientCertificates.Count);
        Diagnostics.Act("stores opened", store.Opened.Count);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Diagnostics.Diff("presented certificate", s_clientCertificate.RawData, testServer.ClientCertificates.Single());
        CollectionAssert.AreEqual(s_clientCertificate.RawData, testServer.ClientCertificates.Single());
        Diagnostics.Assert("store opened", 1, store.Opened.Count);
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
