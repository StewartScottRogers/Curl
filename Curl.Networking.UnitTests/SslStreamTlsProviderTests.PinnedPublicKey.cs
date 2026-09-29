using System.Security.Authentication;
using System.Security.Cryptography;

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
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: ServerKeyPin), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTheServerKeysHashSecondInTheList_Succeeds(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey "sha256//<wrong>;sha256//<right>" ... -> exit 0.
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: $"{WrongPin};{ServerKeyPin}"), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

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
        var (result, plaintextDisposed) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: WrongPin), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

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

        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: path), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAMissingKeyFilePinned_FailsWithExit90(bool matchesSchannelBuild)
    {
        // curl -sS -k --pinnedpubkey nope.pem ... -> exit 90, the same message.
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: Path.Combine(_caFileDirectory, "nope.pem")), CertificateHost, SslProtocols.Tls12, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual(TlsFailureMessages.PinnedPublicKeyMismatch, result.ErrorMessage);
    }

    // Without -k the certificate is judged first: curl -sS --pinnedpubkey sha256//<right> with
    // the untrusted self-signed certificate is exit 60, not 90.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPinAndAnUntrustedCertificate_FailsVerificationFirst()
    {
        var (result, _) = await HandshakeAsync(
            new TlsClientOptions(PinnedPublicKey: WrongPin), CertificateHost, SslProtocols.Tls12, SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
    }
}
