using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandBuiltTlsProvider" /> with <c>--pinnedpubkey</c>, as
/// <see cref="SslStreamTlsProviderTests" /> pins it for the other provider: exit 90 when the
/// server's key is not the pinned one (ADR-0192, BL-608).
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
}
