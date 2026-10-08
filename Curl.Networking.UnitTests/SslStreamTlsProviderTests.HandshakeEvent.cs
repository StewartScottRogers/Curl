using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the <see cref="TlsHandshakeEvent" /> a successful handshake reports, with the facts
/// curl's OpenSSL build prints for <c>-v</c> (ADR-0085, BL-404).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_SelfSignedServerUnderInsecure_ReportsTheHandshakeWithVerifyResult18()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("verify result", 18L, handshake.CertificateVerifyResult);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.ServerCertificate!.RawData);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.PeerCertificateChain[0].RawData);
        Assert.AreEqual(18L, handshake.CertificateVerifyResult);
        Assert.IsFalse(handshake.CertificateVerified);
        Assert.AreNotEqual(SslProtocols.None, handshake.ProtocolVersion);
        Assert.IsNotNull(handshake.CipherSuite);
        Assert.IsNull(handshake.NegotiatedApplicationProtocol);
        Assert.IsEmpty(handshake.OfferedApplicationProtocols);
        Assert.IsNull(handshake.NegotiatedGroupName);
        Assert.IsNull(handshake.PeerSignatureTypeName);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_ServerTrustedThroughCaCertificateFile_ReportsTheVerifiedChainWithVerifyResult0()
    {
        var caFile = Path.Combine(_caFileDirectory, "ca.pem");
        await File.WriteAllTextAsync(caFile, s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("verify result", 0L, handshake.CertificateVerifyResult);
        Assert.AreEqual(0L, handshake.CertificateVerifyResult);
        Assert.IsTrue(handshake.CertificateVerified);
        var chained = Assert.ContainsSingle(handshake.PeerCertificateChain);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, chained.RawData);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheCertificateIsRefused_ReportsAFailedHandshake()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(), events);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsTrue(Assert.ContainsSingle(events.Handshakes).Failed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNullEvents_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("events", "null");
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, null!, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_UnderInsecure_ReportsTrustWithoutVerificationBeforeTheHandshakeAndNoCheckedHostName()
    {
        // curl -v -k https://host.docker.internal:28405/: "SSL Trust: peer verification disabled"
        // before the handshake lines (curl 8.21.0 OpenSSL, measured, BL-405).
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("TLS event count", 2, events.TlsEvents.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(2, events.TlsEvents);
        var trust = Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Assert.IsFalse(trust.VerifiesPeer);
        var handshake = Assert.IsInstanceOfType<TlsHandshakeEvent>(events.TlsEvents[1]);
        Assert.IsNull(handshake.VerifiedHostName);
        Assert.IsFalse(handshake.IsProxy);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFile_ReportsTheFileAsTrustBeforeTheHandshakeAndTheCheckedHostName()
    {
        // curl -v --cacert /w/cert.pem https://localhost:28405/: "SSL Trust Anchors:" /
        // "  CAfile: /w/cert.pem", and the SAN line names "localhost" (measured, BL-405).
        var caFile = Path.Combine(_caFileDirectory, "ca.pem");
        await File.WriteAllTextAsync(caFile, s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile, CaCertificateDirectory: _caFileDirectory), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("TLS event count", 2, events.TlsEvents.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(2, events.TlsEvents);
        var trust = Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Assert.IsTrue(trust.VerifiesPeer);
        Assert.IsFalse(trust.HasCaCertificateBlob);
        Assert.AreEqual(caFile, trust.CaCertificateFile);
        Assert.AreEqual(_caFileDirectory, trust.CaCertificateDirectory);
        var handshake = Assert.IsInstanceOfType<TlsHandshakeEvent>(events.TlsEvents[1]);
        Assert.AreEqual(CertificateHost, handshake.VerifiedHostName);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutCaCertificateFile_ReportsTheDefaultBundleAsTrustEvenWhenVerificationFails()
    {
        // With no CA option the reference build prints "  CAfile: /cacert.pem" (measured, BL-405).
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(), events);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        var trust = Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Diagnostics.Assert("CA file", "/cacert.pem", trust.CaCertificateFile);
        Assert.AreEqual("/cacert.pem", trust.CaCertificateFile);
        Assert.IsNull(trust.CaCertificateDirectory);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheCipherListIsRefused_ReportsNoTrust()
    {
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Ciphers: "AES128-SHA");
        var provider = new SslStreamTlsProvider(options, SchannelBuild);
        ArrangeOptions(options);
        Diagnostics.Arrange("build", BuildName(SchannelBuild));

        var result = await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, events, CancellationToken.None);

        ActResult(result);
        Diagnostics.Act("TLS event count", events.TlsEvents.Count);
        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Assert.IsEmpty(events.TlsEvents);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAProxy_ReportsTheHandshakeAsTheProxys()
    {
        // curl -sv --proxy-insecure -x https://host.docker.internal:28405 http://example.test/
        // says "Proxy certificate:" for the proxy's handshake (measured, BL-405).
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events, isProxy: true);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(Assert.ContainsSingle(events.Handshakes).IsProxy);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow("example.com", false, "example.com")]
    [DataRow("A.Example.test", false, "A.Example.test")]
    [DataRow("127.0.0.1", false, "127.0.0.1")]
    [DataRow("[::1]", false, "::1")]
    [DataRow("[::1", false, "[::1")]
    [DataRow("example.com", true, null)]
    public void VerifiedHostName_ForTheHost_IsTheHostAsTypedWithoutBracketsOrNullUnderInsecure(
        string targetHost,
        bool insecure,
        string? expected)
    {
        Diagnostics.Arrange("target host", targetHost);
        Diagnostics.Arrange("insecure", insecure);

        var verifiedHostName = SslStreamTlsProvider.VerifiedHostName(targetHost, insecure);

        Diagnostics.Act("verified host name", verifiedHostName);
        Diagnostics.Assert("verified host name", expected, verifiedHostName);
        Assert.AreEqual(expected, verifiedHostName);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEventsThroughITlsProvider_ReportsTheTrustAndTheHandshake()
    {
        // A protocol handler's STARTTLS upgrade holds only an ITlsProvider (BL-1058).
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var options = new TlsClientOptions(Insecure: true);
        ITlsProvider provider = new SslStreamTlsProvider(options, OpenSslBuild);
        var events = new RecordingTransferEvents();
        ArrangeOptions(options);
        Diagnostics.Arrange("provider", "through ITlsProvider, OpenSSL build");

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActResult(result);
        Diagnostics.Act("TLS event count", events.TlsEvents.Count);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Assert.IsEmpty(Assert.ContainsSingle(events.Handshakes).OfferedApplicationProtocols);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    /// <summary>
    /// Writes the TLS settings, target host and proxy flag as ARRANGE, runs an OpenSSL-build
    /// handshake against the echo server inside a <c>handshake</c> PHASE, and writes its
    /// outcome and the reported TLS events as ACT.
    /// </summary>
    private async Task<ConnectResult> ReportingHandshakeAsync(
        TlsClientOptions options,
        RecordingTransferEvents events,
        bool isProxy = false,
        string targetHost = CertificateHost)
    {
        ArrangeOptions(options);
        Diagnostics.Arrange("target host", targetHost);
        Diagnostics.Arrange("proxy", isProxy);
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var provider = new SslStreamTlsProvider(options, OpenSslBuild);

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = isProxy
                ? await provider.AuthenticateAsClientAsync(
                    new StreamConnection(client, ServerEndPoint), targetHost, events, isProxy: true, CancellationToken.None)
                : await provider.AuthenticateAsClientAsync(
                    new StreamConnection(client, ServerEndPoint), targetHost, events, CancellationToken.None);
        }

        ActResult(result);
        Diagnostics.Act("TLS events", string.Join(", ", events.TlsEvents.Select(tlsEvent => tlsEvent.GetType().Name)));
        if (result.Connection is null)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return result;
    }
}
