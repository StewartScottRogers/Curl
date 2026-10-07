using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandBuiltTlsProvider" /> with <c>--pinnedpubkey</c>, as
/// <see cref="SslStreamTlsProviderTests" /> pins it for the other provider: exit 90 when the
/// server's key is not the pinned one (ADR-0193, BL-608).
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private static string ServerKeyPin => PinnedPublicKey.HashPrefix + PinnedPublicKey.HashOf(s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashPinned_Succeeds(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MaximumVersion: Tls12, PinnedPublicKey: the server key hash");
        Diagnostics.Arrange("pinned public key", ServerKeyPin);
        Diagnostics.Arrange("server TLS protocol", "TLS 1.2");
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(
                Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: ServerKeyPin)), matchesSchannelBuild), CertificateHost);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysDerFilePinned_Succeeds(bool matchesSchannelBuild)
    {
        var path = WriteFile("key.der", s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MaximumVersion: Tls12, PinnedPublicKey: a DER file");
        Diagnostics.Arrange("pinned public key file", Path.GetFileName(path));
        Diagnostics.Bytes("pinned public key file contents", s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());
        Diagnostics.Arrange("server TLS protocol", "TLS 1.2");
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(
                Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: path)), matchesSchannelBuild), CertificateHost);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinned_FailsWithExit90AndClosesTheConnection(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MaximumVersion: Tls12, PinnedPublicKey: sha256//AAAA");
        Diagnostics.Arrange("server TLS protocol", "TLS 1.2");
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        bool plaintextDisposed;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, plaintextDisposed) = await HandshakeAsync(
                Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA")), matchesSchannelBuild), CertificateHost);
        }

        ActConnectResult(result);
        Diagnostics.Act("plaintext disposed", plaintextDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Diagnostics.Assert("error message", "SSL: public key does not match pinned public key", result.ErrorMessage);
        Assert.AreEqual("SSL: public key does not match pinned public key", result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, plaintextDisposed);
        Assert.IsTrue(plaintextDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashPinned_ReportsTheHashInTheHandshakeEvent(bool matchesSchannelBuild)
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MaximumVersion: Tls12, PinnedPublicKey: the server key hash");
        Diagnostics.Arrange("pinned public key", ServerKeyPin);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(
                Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: ServerKeyPin)), matchesSchannelBuild), CertificateHost, events);
        }

        ActConnectResult(result);
        Diagnostics.Act("handshake events", events.Handshakes.Count);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var reportedHash = Assert.ContainsSingle(events.Handshakes).PinnedPublicKeyHash;
        Diagnostics.Assert("pinned public key hash in the handshake event", ServerKeyPin, reportedHash);
        Assert.AreEqual(ServerKeyPin, reportedHash);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinnedInTheSchannelBuild_ReportsAFailedHandshakeWithTheAlpnOfferAndHashThenTwoMismatchLines()
    {
        // curl 8.21.0 Schannel: "ALPN: curl offers http/1.1", "public key hash:", then the
        // mismatch line twice (BL-877, BL-1149).
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("options", "Insecure: true, MaximumVersion: Tls12, PinnedPublicKey: sha256//AAAA");
        Diagnostics.Arrange("offered application protocols", "http/1.1");
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(
                Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA")), SchannelBuild), CertificateHost, events, ["http/1.1"]);
        }

        ActConnectResult(result);
        var publicKeyLines = events.Info.Where(line => line.Contains("public key", StringComparison.Ordinal)).ToArray();
        Diagnostics.Act("public key info lines", string.Join(" | ", publicKeyLines));
        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("handshake failed", true, handshake.Failed);
        Assert.IsTrue(handshake.Failed);
        Diagnostics.Assert("pinned public key hash", ServerKeyPin, handshake.PinnedPublicKeyHash);
        Assert.AreEqual(ServerKeyPin, handshake.PinnedPublicKeyHash);
        Diagnostics.Assert("offered application protocols", "http/1.1", string.Join(",", handshake.OfferedApplicationProtocols));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, handshake.OfferedApplicationProtocols.ToArray());
        Diagnostics.Assert("public key info line count", 2, publicKeyLines.Length);
        CollectionAssert.AreEqual(
            Enumerable.Repeat("SSL: public key does not match pinned public key", 2).ToArray(),
            events.Info.Where(line => line.Contains("public key", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinnedInTheOpenSslBuild_ReportsAFailedHandshakeWithWhatWasNegotiatedThenOneMismatchLine()
    {
        // curl 8.18.0 OpenSSL -v -k --pinnedpubkey sha256//<wrong>: "SSL connection using", the
        // certificate, the verify result and " public key hash:", then the mismatch line once
        // (BL-1149, BL-1178).
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MaximumVersion: Tls12, PinnedPublicKey: sha256//AAAA");
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(
                Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA")), OpenSslBuild), CertificateHost, events);
        }

        ActConnectResult(result);
        var publicKeyLines = events.Info.Where(line => line.Contains("public key", StringComparison.Ordinal)).ToArray();
        Diagnostics.Act("public key info lines", string.Join(" | ", publicKeyLines));
        Diagnostics.Assert("exit code", CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("handshake failed", true, handshake.Failed);
        Assert.IsTrue(handshake.Failed);
        Diagnostics.Assert("protocol version", SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        Diagnostics.Assert("cipher suite reported", true, handshake.CipherSuite is not null);
        Assert.IsNotNull(handshake.CipherSuite);
        Diagnostics.Assert("server certificate reported", true, handshake.ServerCertificate is not null);
        Assert.IsNotNull(handshake.ServerCertificate);
        Diagnostics.Assert("pinned public key hash", ServerKeyPin, handshake.PinnedPublicKeyHash);
        Assert.AreEqual(ServerKeyPin, handshake.PinnedPublicKeyHash);
        Diagnostics.Assert("public key info line count", 1, publicKeyLines.Length);
        CollectionAssert.AreEqual(
            new[] { "SSL: public key does not match pinned public key" },
            events.Info.Where(line => line.Contains("public key", StringComparison.Ordinal)).ToArray());
    }
}
