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
        var (result, _) = await HandshakeAsync(
            Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: ServerKeyPin)), matchesSchannelBuild), CertificateHost);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysDerFilePinned_Succeeds(bool matchesSchannelBuild)
    {
        var path = WriteFile("key.der", s_serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());

        var (result, _) = await HandshakeAsync(
            Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: path)), matchesSchannelBuild), CertificateHost);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinned_FailsWithExit90AndClosesTheConnection(bool matchesSchannelBuild)
    {
        var (result, plaintextDisposed) = await HandshakeAsync(
            Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA")), matchesSchannelBuild), CertificateHost);

        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual("SSL: public key does not match pinned public key", result.ErrorMessage);
        Assert.IsTrue(plaintextDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashPinned_ReportsTheHashInTheHandshakeEvent(bool matchesSchannelBuild)
    {
        var events = new RecordingTransferEvents();

        var (result, _) = await HandshakeAsync(
            Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: ServerKeyPin)), matchesSchannelBuild), CertificateHost, events);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ServerKeyPin, Assert.ContainsSingle(events.Handshakes).PinnedPublicKeyHash);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinnedInTheSchannelBuild_ReportsAFailedHandshakeWithTheAlpnOfferAndHashThenTwoMismatchLines()
    {
        // curl 8.21.0 Schannel: "ALPN: curl offers http/1.1", "public key hash:", then the
        // mismatch line twice (BL-877, BL-1149).
        var events = new RecordingTransferEvents();

        var (result, _) = await HandshakeAsync(
            Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA")), SchannelBuild), CertificateHost, events, ["http/1.1"]);

        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsTrue(handshake.Failed);
        Assert.AreEqual(ServerKeyPin, handshake.PinnedPublicKeyHash);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, handshake.OfferedApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Repeat("SSL: public key does not match pinned public key", 2).ToArray(),
            events.Info.Where(line => line.Contains("public key", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnotherKeysHashPinnedInTheOpenSslBuild_ReportsTheHashThenOneMismatchLine()
    {
        var events = new RecordingTransferEvents();

        var (result, _) = await HandshakeAsync(
            Provider(Tls12Only(new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA")), OpenSslBuild), CertificateHost, events);

        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.IsEmpty(events.Handshakes);
        CollectionAssert.AreEqual(
            new[] { " public key hash: " + ServerKeyPin, "SSL: public key does not match pinned public key" },
            events.Info.Where(line => line.Contains("public key", StringComparison.Ordinal)).ToArray());
    }
}
