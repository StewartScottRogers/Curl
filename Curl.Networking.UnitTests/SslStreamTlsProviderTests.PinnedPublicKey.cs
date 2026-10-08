using System.Security.Authentication;
using System.Security.Cryptography;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with <c>--pinnedpubkey</c>
/// (<see cref="TlsClientOptions.PinnedPublicKey" />): after the handshake, under <c>-k</c> too,
/// a server key the pin does not name is exit 90 with the same message in both builds
/// (ADR-0193). Measured with curl 8.21.0's Schannel build and curl 8.18.0's OpenSSL build
/// against Record-CurlExchange.ps1 -Tls, 2026-09-29 (BL-608).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    private const string WrongPin = "sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private static string ServerKeyPin => PinnedPublicKey.HashPrefix + PinnedPublicKey.HashOf(s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashPinned_Succeeds(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey sha256//<right> https://127.0.0.1:18608/ -> exit 0.
        Diagnostics.Arrange("pinned public key", "the server key's hash");
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: ServerKeyPin), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashSecondInTheList_Succeeds(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey "sha256//<wrong>;sha256//<right>" ... -> exit 0.
        Diagnostics.Arrange("pinned public key", $"{WrongPin};<the server key's hash>");
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: $"{WrongPin};{ServerKeyPin}"), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinned_FailsWithExit90AndClosesTheConnection(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey sha256//<wrong> ... -> exit 90,
        // "curl: (90) SSL: public key does not match pinned public key" in both builds.
        Diagnostics.Arrange("pinned public key", WrongPin);
        var (result, plaintextDisposed) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: WrongPin), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Diagnostics.Assert("error message", "SSL: public key does not match pinned public key", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual("SSL: public key does not match pinned public key", result.ErrorMessage);
        Assert.IsTrue(plaintextDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysPemFilePinned_Succeeds(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey key.pem ... -> exit 0.
        var path = Path.Combine(_caFileDirectory, "key.pem");
        File.WriteAllText(path, PemEncoding.WriteString("PUBLIC KEY", s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo()));
        Diagnostics.Arrange("pinned public key", "<test folder>/key.pem, the server key as PEM");

        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: path), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAMissingKeyFilePinned_FailsWithExit90(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey nope.pem ... -> exit 90, the same message.
        Diagnostics.Arrange("pinned public key", "<test folder>/nope.pem, missing");
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: Path.Combine(_caFileDirectory, "nope.pem")), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(TlsFailureMessages.PinnedPublicKeyMismatch, result.ErrorMessage);
    }

    // Without -k the certificate is judged first: curl -sS --pinnedpubkey sha256//<right> with
    // the untrusted self-signed certificate is exit 60, not 90.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPinAndAnUntrustedCertificate_FailsVerificationFirst()
    {
        Diagnostics.Arrange("pinned public key", WrongPin);
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(PinnedPublicKey: WrongPin), CertificateHost, SslProtocols.Tls12, SchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashPinned_ReportsTheHashInTheHandshakeEvent(bool matchesSchannelBuild)
    {
        // curl -v -k --pinnedpubkey sha256//<right> ... -> "*  public key hash: sha256//<right>" (BL-877).
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(Insecure: true, PinnedPublicKey: $"{WrongPin};{ServerKeyPin}"), events, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Diagnostics.Assert("pinned public key hash", ServerKeyPin, events.Handshakes.SingleOrDefault()?.PinnedPublicKeyHash);
        Assert.AreEqual(ServerKeyPin, Assert.ContainsSingle(events.Handshakes).PinnedPublicKeyHash);
        Assert.IsEmpty(events.Info);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysFilePinned_ReportsNoHash()
    {
        // curl -v -k --pinnedpubkey key.pem ... prints no "public key hash" line (BL-877).
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllBytes(path, s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("pinned public key", "a temporary file holding the server key as DER");
        try
        {
            var result = await PinReportingHandshakeAsync(new TlsClientOptions(Insecure: true, PinnedPublicKey: path), events, SchannelBuild);

            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
            Diagnostics.Assert("pinned public key hash", null, events.Handshakes.SingleOrDefault()?.PinnedPublicKeyHash);
            Assert.IsNull(Assert.ContainsSingle(events.Handshakes).PinnedPublicKeyHash);
            await result.Connection!.DisposeAsync();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(SchannelBuild, 2)]
    [DataRow(OpenSslBuild, 1)]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinned_ReportsAFailedHandshakeWithTheHashThenEachBuildsMismatchLines(bool matchesSchannelBuild, int mismatchLines)
    {
        // curl -v -k --pinnedpubkey sha256//<wrong> ... -> "*  public key hash: sha256//<right>", then
        // "* SSL: public key does not match pinned public key" twice (Schannel) or once (OpenSSL)
        // (BL-877); the hash line now comes from the failed handshake's event (BL-1149).
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(Insecure: true, PinnedPublicKey: WrongPin), events, matchesSchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Diagnostics.Assert("mismatch lines", mismatchLines, events.Info.Count);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        CollectionAssert.AreEqual(
            Enumerable.Repeat("SSL: public key does not match pinned public key", mismatchLines).ToArray(),
            events.Info);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsTrue(handshake.Failed);
        Assert.AreEqual(ServerKeyPin, handshake.PinnedPublicKeyHash);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinnedInTheSchannelBuild_ReportsTheAlpnOfferInTheFailedHandshake()
    {
        // curl 8.21.0 Schannel -v -k --pinnedpubkey sha256//<wrong>: "* ALPN: curl offers http/1.1"
        // comes before "*  public key hash:" (measured 2026-10-02, BL-1149).
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(Insecure: true, PinnedPublicKey: WrongPin), events, SchannelBuild, ["http/1.1"]);

        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("offered ALPN", "http/1.1", string.Join(",", handshake.OfferedApplicationProtocols));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, handshake.OfferedApplicationProtocols.ToArray());
        Assert.IsTrue(handshake.Failed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinnedInTheOpenSslBuild_ReportsTheCertificateDetailsInTheFailedHandshake()
    {
        // curl 8.18.0 OpenSSL -v -k --pinnedpubkey sha256//<wrong>: "SSL connection using", the
        // server certificate and the verify result come before "*  public key hash:" (BL-1149).
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(Insecure: true, PinnedPublicKey: WrongPin), events, OpenSslBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("server certificate thumbprint", s_serverCertificate.Thumbprint, handshake.ServerCertificate?.Thumbprint);
        Assert.AreEqual(s_serverCertificate.Thumbprint, handshake.ServerCertificate?.Thumbprint);
        Assert.IsFalse(handshake.CertificateVerified);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedCertificateInTheSchannelBuild_ReportsTheAlpnOfferInAFailedHandshake()
    {
        // curl 8.21.0 Schannel -v, untrusted self-signed certificate: "* ALPN: curl offers
        // http/1.1" before the SEC_E_UNTRUSTED_ROOT failure, exit 60 (measured 2026-10-02, BL-1149).
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(), events, SchannelBuild, ["http/1.1"]);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsTrue(handshake.Failed);
        Assert.IsNull(handshake.PinnedPublicKeyHash);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedCertificateInTheOpenSslBuild_ReportsTheCertificateDetailsInAFailedHandshake()
    {
        // curl 8.18.0 OpenSSL -v, untrusted self-signed certificate: "SSL connection using", the
        // ALPN answer, "Server certificate:" and its details, then the verify result as the
        // exit 60 message (measured 2026-10-02, BL-1178).
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(), events, OpenSslBuild, ["http/1.1"]);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.IsTrue(handshake.Failed);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.IsNotNull(handshake.CipherSuite);
        Assert.AreEqual(s_serverCertificate.Thumbprint, handshake.ServerCertificate?.Thumbprint);
        Assert.AreEqual(CertificateHost, handshake.VerifiedHostName);
        Assert.IsFalse(handshake.CertificateVerified);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerHangsUpInTheOpenSslBuild_ReportsAFailedHandshakeThatNegotiatedNothing()
    {
        // curl 8.18.0 OpenSSL -v -k --tlsv1.3 against a TLS 1.2 server: "ALPN: curl offers
        // h2,http/1.1" before the ClientHello, then exit 35 with no "SSL connection using"
        // (measured 2026-10-02, BL-1178).
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await server.DisposeAsync();
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true);
        ArrangeOptions(options);
        Diagnostics.Arrange("server", "hung up before the handshake, OpenSSL build, ALPN http/1.1");

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await new SslStreamTlsProvider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, ["http/1.1"], CancellationToken.None);
        }

        ActResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.None, handshake.ProtocolVersion);
        Assert.IsTrue(handshake.Failed);
        Assert.AreEqual(SslProtocols.None, handshake.ProtocolVersion);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, handshake.OfferedApplicationProtocols.ToArray());
    }

    /// <summary>
    /// Writes the TLS settings, build and ALPN offer as ARRANGE, runs the handshake against a
    /// TLS 1.2 echo server inside a <c>handshake</c> PHASE, and writes the info lines and
    /// handshake events it reported as ACT.
    /// </summary>
    private async Task<ConnectResult> PinReportingHandshakeAsync(TlsClientOptions options, RecordingTransferEvents events, bool matchesSchannelBuild, IReadOnlyList<string>? applicationProtocols = null)
    {
        ArrangeOptions(options);
        Diagnostics.Arrange("build", BuildName(matchesSchannelBuild));
        Diagnostics.Arrange("application protocols", string.Join(",", applicationProtocols ?? []));
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await new SslStreamTlsProvider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, applicationProtocols ?? [], CancellationToken.None);
        }

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Act("handshakes", string.Join(", ", events.Handshakes.Select(handshake => handshake.Failed ? "failed" : "completed")));
        if (result.Connection is null)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return result;
    }
}
