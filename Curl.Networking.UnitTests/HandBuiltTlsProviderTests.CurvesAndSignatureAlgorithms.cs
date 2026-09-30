using System.Security.Authentication;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins <c>--curves</c> and <c>--sigalgs</c> in the hand-built client on every platform
/// (BL-709, ADR-0151, ADR-0284): the ClientHello's <c>supported_groups</c>, <c>key_share</c>
/// and <c>signature_algorithms</c> for each value, as Ubuntu's curl 8.18.0 with OpenSSL 3.5.5
/// sent them (captured 2026-09-30), in both builds' profiles, and the measured failures.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private static readonly byte[] HandshakeFailureAlert = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28];

    [TestMethod]
    [DataRow(OpenSslBuild, "X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow(OpenSslBuild, "P-384:X25519", new ushort[] { 0x0018, 0x001d }, new ushort[] { 0x0018 })]
    [DataRow(OpenSslBuild, "X25519MLKEM768", new ushort[] { 0x11ec }, new ushort[] { 0x11ec })]
    [DataRow(OpenSslBuild, "*P-256:*X25519:P-384", new ushort[] { 0x0017, 0x001d, 0x0018 }, new ushort[] { 0x0017, 0x001d })]
    [DataRow(OpenSslBuild, "brainpoolP256r1:X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow(SchannelBuild, "X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow(SchannelBuild, "P-384:X25519", new ushort[] { 0x0018, 0x001d }, new ushort[] { 0x0018 })]
    public async Task AuthenticateAsClientAsync_WithCurves_OffersTheMeasuredGroupsAndKeyShares(bool matchesSchannelBuild, string curves, ushort[] groups, ushort[] keyShares)
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Curves: curves), matchesSchannelBuild, ProfileHost, Http11));

        CollectionAssert.AreEqual(groups, SupportedGroupsExtension.Decode(ExtensionData(hello, TlsExtensionType.SupportedGroups)).Value.ToArray());
        CollectionAssert.AreEqual(keyShares, KeySharesOf(hello).Select(share => share.Group).ToArray());
        var profile = ProfileOf(matchesSchannelBuild);
        CollectionAssert.AreEqual(
            ClientHelloProfileMapping.CheckableSignatureAlgorithms(profile).ToArray(),
            SignatureAlgorithmsExtension.Decode(ExtensionData(hello, TlsExtensionType.SignatureAlgorithms)).Value.ToArray());
        CollectionAssert.AreEqual(profile.ExtensionOrder.ToArray(), ExtensionTypes(hello));
    }

    [TestMethod]
    [DataRow(OpenSslBuild, "ECDSA+SHA256", new ushort[] { 0x0403 })]
    [DataRow(OpenSslBuild, "rsa_pss_rsae_sha256:ECDSA+SHA256", new ushort[] { 0x0804, 0x0403 })]
    [DataRow(OpenSslBuild, "RSA+SHA256:RSA-PSS+SHA256:ECDSA+SHA384:ed25519:mldsa65:RSA+SHA1", new ushort[] { 0x0401, 0x0804, 0x0503, 0x0807 })]
    [DataRow(SchannelBuild, "ECDSA+SHA256", new ushort[] { 0x0403 })]
    public async Task AuthenticateAsClientAsync_WithSigalgs_OffersTheMeasuredSchemesAndTheProfilesGroups(bool matchesSchannelBuild, string signatureAlgorithms, ushort[] schemes)
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(SignatureAlgorithms: signatureAlgorithms), matchesSchannelBuild, ProfileHost, Http11));

        var profile = ProfileOf(matchesSchannelBuild);
        CollectionAssert.AreEqual(schemes, SignatureAlgorithmsExtension.Decode(ExtensionData(hello, TlsExtensionType.SignatureAlgorithms)).Value.ToArray());
        CollectionAssert.AreEqual(profile.SupportedGroups.ToArray(), SupportedGroupsExtension.Decode(ExtensionData(hello, TlsExtensionType.SupportedGroups)).Value.ToArray());
        CollectionAssert.AreEqual(profile.KeyShareGroups.ToArray(), KeySharesOf(hello).Select(share => share.Group).ToArray());
    }

    // Measured with --tls-max 1.2: --curves X25519 offers [x25519], --sigalgs ECDSA+SHA256 [0x0403].
    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCurvesAndSigalgsUnderATls12Ceiling_OffersThemInTheTls12Hello(bool matchesSchannelBuild)
    {
        var options = Tls12Only(new TlsClientOptions(Curves: "X25519", SignatureAlgorithms: "ECDSA+SHA256"));

        var hello = DecodeClientHello(await CaptureClientHelloAsync(options, matchesSchannelBuild, ProfileHost, Http11));

        CollectionAssert.AreEqual(new ushort[] { 0x001d }, SupportedGroupsExtension.Decode(ExtensionData(hello, TlsExtensionType.SupportedGroups)).Value.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x0403 }, SignatureAlgorithmsExtension.Decode(ExtensionData(hello, TlsExtensionType.SignatureAlgorithms)).Value.ToArray());
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.KeyShare));
    }

    // The failures come before a byte is sent, in the OpenSSL build's order (measured), and are
    // the same on every platform.
    [TestMethod]
    [DataRow(SchannelBuild, "bogus", null, CurlExitCode.SslCipher, "failed setting curves list: 'bogus'")]
    [DataRow(OpenSslBuild, "bogus", null, CurlExitCode.SslCipher, "failed setting curves list: 'bogus'")]
    [DataRow(OpenSslBuild, "bogus", "bogus", CurlExitCode.SslCipher, "failed setting curves list: 'bogus'")]
    [DataRow(SchannelBuild, null, "bogus", CurlExitCode.SslCipher, "failed setting signature algorithms: 'bogus'")]
    [DataRow(OpenSslBuild, "?bogus", "bogus", CurlExitCode.SslCipher, "failed setting signature algorithms: 'bogus'")]
    [DataRow(SchannelBuild, "?bogus", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000127:SSL routines::no suitable groups")]
    [DataRow(OpenSslBuild, "-X25519", "RSA+SHA1", CurlExitCode.SslConnectError, "TLS connect error: error:0A000127:SSL routines::no suitable groups")]
    [DataRow(SchannelBuild, null, "RSA+SHA1", CurlExitCode.SslConnectError, "TLS connect error: error:0A000076:SSL routines::no suitable signature algorithm")]
    [DataRow(OpenSslBuild, "X25519", "RSA+SHA1", CurlExitCode.SslConnectError, "TLS connect error: error:0A000076:SSL routines::no suitable signature algorithm")]
    public async Task AuthenticateAsClientAsync_WithCurvesOrSigalgsLeavingNothingToOffer_FailsWithTheMeasuredLine(
        bool matchesSchannelBuild,
        string? curves,
        string? signatureAlgorithms,
        CurlExitCode exitCode,
        string expected)
    {
        var (plaintext, stream) = Unanswered();

        var result = await Provider(new TlsClientOptions(Curves: curves, SignatureAlgorithms: signatureAlgorithms), matchesSchannelBuild)
            .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
        Assert.IsTrue(stream.IsDisposed);
    }

    // A server sharing no group or unable to sign answers handshake_failure: Windows prints what
    // the build applying the option prints (curl.se's LibreSSL build for --curves, the OpenSSL
    // build for --sigalgs, both measured 2026-09-30); elsewhere the OpenSSL build's line.
    [TestMethod]
    [DataRow(SchannelBuild, "X25519", null, "TLS connect error: error:14004410:SSL routines:CONNECT_CR_SRVR_HELLO:sslv3 alert handshake failure")]
    [DataRow(OpenSslBuild, "X25519", null, "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    [DataRow(SchannelBuild, null, "ECDSA+SHA256", "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    [DataRow(SchannelBuild, "X25519", "ECDSA+SHA256", "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    [DataRow(OpenSslBuild, null, "ECDSA+SHA256", "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    public async Task AuthenticateAsClientAsync_WithCurvesOrSigalgsAndAHandshakeFailureAlert_ReportsTheApplyingBuildsLine(
        bool matchesSchannelBuild,
        string? curves,
        string? signatureAlgorithms,
        string expected)
    {
        var options = new TlsClientOptions(Insecure: true, Curves: curves, SignatureAlgorithms: signatureAlgorithms);

        var result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, HandshakeFailureAlert, options);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }

    // Only the handshake_failure alert was measured from curl.se's build; the server closing
    // keeps the platform build's line.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCurvesInTheSchannelBuildWhenTheServerCloses_ReportsSchannelsLine()
    {
        var result = await HandshakeWithServerAnsweringAsync(SchannelBuild, answer: null, new TlsClientOptions(Insecure: true, Curves: "X25519"));

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", result.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCurvesAndSigalgsTheServerShares_ConnectsOverTls12(bool matchesSchannelBuild)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var options = Tls12Only(new TlsClientOptions(Insecure: true, Curves: "P-256:X25519", SignatureAlgorithms: "RSA+SHA256:rsa_pss_rsae_sha256"));

        var result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        Assert.AreEqual("ping", Encoding.ASCII.GetString(await EchoAsync(connection, "ping")));
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }
}
