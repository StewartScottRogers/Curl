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
    private const string LongHostName = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.example";

    private static readonly byte[] HandshakeFailureAlert = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28];

    // What Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 wrote for --curves '*brainpoolP256r1',
    // '?bogus' and --sigalgs RSA+SHA1 before exit 35 (measured 2026-10-01, BL-1087).
    private static readonly byte[] InternalErrorAlert = [0x15, 0x03, 0x01, 0x00, 0x02, 0x02, 0x50];

    [TestMethod]
    [DataRow(OpenSslBuild, "X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow(OpenSslBuild, "P-384:X25519", new ushort[] { 0x0018, 0x001d }, new ushort[] { 0x0018 })]
    [DataRow(OpenSslBuild, "X25519MLKEM768", new ushort[] { 0x11ec }, new ushort[] { 0x11ec })]
    [DataRow(OpenSslBuild, "*P-256:*X25519:P-384", new ushort[] { 0x0017, 0x001d, 0x0018 }, new ushort[] { 0x0017, 0x001d })]
    [DataRow(OpenSslBuild, "brainpoolP256r1:X25519", new ushort[] { 0x001a, 0x001d }, new ushort[] { 0x001d })]
    [DataRow(OpenSslBuild, "*brainpoolP256r1:*P-384", new ushort[] { 0x001a, 0x0018 }, new ushort[] { 0x0018 })]
    [DataRow(OpenSslBuild, "SecP256r1MLKEM768", new ushort[] { 0x11eb }, new ushort[] { 0x11eb })]
    [DataRow(OpenSslBuild, "SecP384r1MLKEM1024", new ushort[] { 0x11ed }, new ushort[] { 0x11ed })]
    [DataRow(OpenSslBuild, "MLKEM512", new ushort[] { 0x0200 }, new ushort[] { 0x0200 })]
    [DataRow(OpenSslBuild, "MLKEM768", new ushort[] { 0x0201 }, new ushort[] { 0x0201 })]
    [DataRow(OpenSslBuild, "MLKEM1024", new ushort[] { 0x0202 }, new ushort[] { 0x0202 })]
    [DataRow(OpenSslBuild, "brainpoolP256r1tls13", new ushort[] { 0x001f }, new ushort[] { 0x001f })]
    [DataRow(OpenSslBuild, "brainpoolP384r1tls13", new ushort[] { 0x0020 }, new ushort[] { 0x0020 })]
    [DataRow(OpenSslBuild, "brainpoolP512r1tls13", new ushort[] { 0x0021 }, new ushort[] { 0x0021 })]
    [DataRow(OpenSslBuild, "X25519:MLKEM768", new ushort[] { 0x001d, 0x0201 }, new ushort[] { 0x001d })]
    [DataRow(OpenSslBuild, "MLKEM1024:brainpoolP512r1tls13:*SecP256r1MLKEM768", new ushort[] { 0x0202, 0x0021, 0x11eb }, new ushort[] { 0x11eb })]
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
        CollectionAssert.AreEqual(
            profile.ExtensionOrder.Where(type => type != TlsExtensionType.EcPointFormats).ToArray(),
            ExtensionTypes(hello).Where(type => type is not TlsExtensionType.EcPointFormats and not TlsExtensionType.Padding).ToArray());
    }

    // The OpenSSL build's hello under --curves, as measured (BL-709 Notes, Ubuntu curl 8.18.0,
    // OpenSSL 3.5.5): ec_point_formats only while an EC group remains, padding last when the
    // hello is 256 to 511 bytes (OpenSSL's tls_construct_ctos_padding), and brainpool TLS 1.2
    // groups kept beside TLS 1.3 (BL-1048).
    [TestMethod]
    [DataRow("X25519", new ushort[] { 0xff01, 0x0000, 0x000b, 0x000a, 0x0010, 0x0016, 0x0017, 0x0031, 0x000d, 0x002b, 0x002d, 0x0033, 0x001b, 0x0015 }, new ushort[] { 0x001d })]
    [DataRow("P-384:X25519", new ushort[] { 0xff01, 0x0000, 0x000b, 0x000a, 0x0010, 0x0016, 0x0017, 0x0031, 0x000d, 0x002b, 0x002d, 0x0033, 0x001b, 0x0015 }, new ushort[] { 0x0018, 0x001d })]
    [DataRow("X25519MLKEM768", new ushort[] { 0xff01, 0x0000, 0x000a, 0x0010, 0x0016, 0x0017, 0x0031, 0x000d, 0x002b, 0x002d, 0x0033, 0x001b }, new ushort[] { 0x11ec })]
    [DataRow("brainpoolP256r1:X25519", new ushort[] { 0xff01, 0x0000, 0x000b, 0x000a, 0x0010, 0x0016, 0x0017, 0x0031, 0x000d, 0x002b, 0x002d, 0x0033, 0x001b, 0x0015 }, new ushort[] { 0x001a, 0x001d })]
    public async Task AuthenticateAsClientAsync_WithCurvesInTheOpenSslBuild_SendsTheMeasuredExtensionsAndGroups(string curves, ushort[] extensionTypes, ushort[] groups)
    {
        var record = await CaptureClientHelloAsync(new TlsClientOptions(Curves: curves), OpenSslBuild, ProfileHost, Http11);
        var hello = DecodeClientHello(record);

        CollectionAssert.AreEqual(extensionTypes, ExtensionTypes(hello).Select(type => (ushort)type).ToArray());
        CollectionAssert.AreEqual(groups, SupportedGroupsExtension.Decode(ExtensionData(hello, TlsExtensionType.SupportedGroups)).Value.ToArray());
        if (extensionTypes[^1] == (ushort)TlsExtensionType.Padding)
        {
            Assert.AreEqual(512, hello.Encode().Length);
        }
    }

    [TestMethod]
    [DataRow(OpenSslBuild, "ECDSA+SHA256", new ushort[] { 0x0403 })]
    [DataRow(OpenSslBuild, "rsa_pss_rsae_sha256:ECDSA+SHA256", new ushort[] { 0x0804, 0x0403 })]
    [DataRow(OpenSslBuild, "RSA+SHA256:RSA-PSS+SHA256:ECDSA+SHA384:ed25519:mldsa65:RSA+SHA1", new ushort[] { 0x0401, 0x0804, 0x0503, 0x0807, 0x0905 })]
    [DataRow(OpenSslBuild, "mldsa44", new ushort[] { 0x0904 })]
    [DataRow(OpenSslBuild, "mldsa65", new ushort[] { 0x0905 })]
    [DataRow(OpenSslBuild, "mldsa87", new ushort[] { 0x0906 })]
    [DataRow(OpenSslBuild, "ed448", new ushort[] { 0x0808 })]
    [DataRow(OpenSslBuild, "ecdsa_brainpoolP256r1tls13_sha256", new ushort[] { 0x081a })]
    [DataRow(OpenSslBuild, "ecdsa_brainpoolP384r1tls13_sha384", new ushort[] { 0x081b })]
    [DataRow(OpenSslBuild, "ecdsa_brainpoolP512r1tls13_sha512", new ushort[] { 0x081c })]
    [DataRow(OpenSslBuild, "ed25519:mldsa65", new ushort[] { 0x0807, 0x0905 })]
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

    // The OpenSSL build's TLS 1.2-ceiling hello under --curves X25519, as measured (BL-1156,
    // Ubuntu curl 8.18.0, OpenSSL 3.5.5): a short hello is not padded; one of 256 to 511 bytes,
    // here for a 75-character host name, ends in padding that brings it to 512.
    [TestMethod]
    [DataRow(ProfileHost, new ushort[] { 0xff01, 0x0000, 0x000b, 0x000a, 0x0010, 0x0016, 0x0017, 0x000d })]
    [DataRow(LongHostName, new ushort[] { 0xff01, 0x0000, 0x000b, 0x000a, 0x0010, 0x0016, 0x0017, 0x000d, 0x0015 })]
    public async Task AuthenticateAsClientAsync_WithCurvesUnderATls12CeilingInTheOpenSslBuild_SendsTheMeasuredExtensions(string host, ushort[] extensionTypes)
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions(Curves: "X25519")), OpenSslBuild, host, Http11));

        CollectionAssert.AreEqual(extensionTypes, ExtensionTypes(hello).Select(type => (ushort)type).ToArray());
        Assert.AreEqual(extensionTypes[^1] == (ushort)TlsExtensionType.Padding, hello.Encode().Length == 512);
    }

    // The TLS 1.2 half of a TLS 1.3 hello is never padded on its own: the TLS 1.3 order places padding.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCurvesAndALongHostInTheOpenSslBuild_SendsOnePadding()
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Curves: "X25519"), OpenSslBuild, LongHostName, Http11));

        Assert.AreEqual(1, hello.Extensions.Count(extension => extension.Type == TlsExtensionType.Padding));
    }

    // The failures come before a ClientHello is sent, in the OpenSSL build's order (measured;
    // stars on TLS 1.2-only groups alone, BL-1082, measured 2026-10-01), and are
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
    [DataRow(OpenSslBuild, "*brainpoolP256r1:P-384", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000065:SSL routines::no suitable key share")]
    [DataRow(SchannelBuild, "*brainpoolP256r1:P-384", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000065:SSL routines::no suitable key share")]
    [DataRow(OpenSslBuild, "*brainpoolP256r1:X25519", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000065:SSL routines::no suitable key share")]
    [DataRow(OpenSslBuild, "brainpoolP256r1:*brainpoolP384r1:X25519", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000065:SSL routines::no suitable key share")]
    [DataRow(OpenSslBuild, "*brainpoolP256r1", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000127:SSL routines::no suitable groups")]
    [DataRow(OpenSslBuild, "brainpoolP256r1", null, CurlExitCode.SslConnectError, "TLS connect error: error:0A000127:SSL routines::no suitable groups")]
    [DataRow(OpenSslBuild, "*brainpoolP256r1", "RSA+SHA1", CurlExitCode.SslConnectError, "TLS connect error: error:0A000127:SSL routines::no suitable groups")]
    [DataRow(OpenSslBuild, "*brainpoolP256r1:P-384", "RSA+SHA1", CurlExitCode.SslConnectError, "TLS connect error: error:0A000076:SSL routines::no suitable signature algorithm")]
    public async Task AuthenticateAsClientAsync_WithCurvesOrSigalgsLeavingNothingToOffer_FailsWithTheMeasuredLine(
        bool matchesSchannelBuild,
        string? curves,
        string? signatureAlgorithms,
        CurlExitCode exitCode,
        string expected)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();

        var result = await Provider(new TlsClientOptions(Curves: curves, SignatureAlgorithms: signatureAlgorithms), matchesSchannelBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
        Assert.IsTrue(client.IsDisposed);
        // A refused list (exit 59) fails before connecting; nothing left to offer (exit 35) is
        // announced with OpenSSL's internal_error alert in both builds (ADR-0303).
        CollectionAssert.AreEqual(exitCode == CurlExitCode.SslCipher ? Array.Empty<byte>() : InternalErrorAlert, await ReadUntilClosedAsync(server));
    }

    // Under a TLS 1.2 ceiling, a list leaving no group sends the ClientHello without
    // ec_point_formats and supported_groups, as Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 did for
    // '?bogus' and X25519MLKEM768 (measured 2026-10-01, BL-1094).
    [TestMethod]
    [DataRow(SchannelBuild, "?bogus")]
    [DataRow(OpenSslBuild, "?bogus")]
    [DataRow(OpenSslBuild, "X25519MLKEM768")]
    public async Task AuthenticateAsClientAsync_WithCurvesLeavingNoGroupUnderATls12Ceiling_SendsTheHelloWithoutGroups(bool matchesSchannelBuild, string curves)
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions(Curves: curves)), matchesSchannelBuild, ProfileHost, Http11));

        var types = ExtensionTypes(hello);
        CollectionAssert.DoesNotContain(types, TlsExtensionType.SupportedGroups);
        CollectionAssert.DoesNotContain(types, TlsExtensionType.EcPointFormats);
        CollectionAssert.Contains(types, TlsExtensionType.SignatureAlgorithms);
    }

    // Under a TLS 1.2 ceiling, a list leaving no scheme TLS 1.2 can check fails with OpenSSL's
    // "no ciphers available" and its internal_error alert, whatever --curves leaves (measured
    // 2026-10-01, BL-1094).
    [TestMethod]
    [DataRow(SchannelBuild, null, "RSA+SHA1")]
    [DataRow(OpenSslBuild, null, "RSA+SHA1")]
    [DataRow(OpenSslBuild, null, "mldsa65")]
    [DataRow(OpenSslBuild, "?bogus", "RSA+SHA1")]
    public async Task AuthenticateAsClientAsync_WithSigalgsLeavingNoTls12SchemeUnderATls12Ceiling_FailsWithNoCiphersAvailable(
        bool matchesSchannelBuild,
        string? curves,
        string signatureAlgorithms)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();

        var result = await Provider(Tls12Only(new TlsClientOptions(Curves: curves, SignatureAlgorithms: signatureAlgorithms)), matchesSchannelBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A0000B5:SSL routines::no ciphers available", result.ErrorMessage);
        Assert.IsTrue(client.IsDisposed);
        CollectionAssert.AreEqual(InternalErrorAlert, await ReadUntilClosedAsync(server));
    }

    // A peer already gone when the alert is written leaves the failure as it was.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNothingToOfferAndAWriteThatFails_StillFailsWithTheMeasuredLine()
    {
        var plaintext = new WriteFailingConnection(new IOException("Unable to write data to the transport connection."));

        var result = await Provider(new TlsClientOptions(Curves: "?bogus"), OpenSslBuild)
            .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A000127:SSL routines::no suitable groups", result.ErrorMessage);
        Assert.IsTrue(plaintext.IsDisposed);
    }

    // Cancelled while the alert is written, the connection is still disposed and the
    // cancellation escapes, as ITlsProvider lets it.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNothingToOfferAndTheAlertWriteCancelled_DisposesAndThrows()
    {
        var plaintext = new WriteFailingConnection(new OperationCanceledException());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await Provider(new TlsClientOptions(Curves: "?bogus"), OpenSslBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None));

        Assert.IsTrue(plaintext.IsDisposed);
    }

    private static async Task<byte[]> ReadUntilClosedAsync(InMemoryDuplexStream server)
    {
        using var received = new MemoryStream();
        await server.CopyToAsync(received);
        return received.ToArray();
    }

    // A connection every write to fails with the given exception.
    private sealed class WriteFailingConnection(Exception writeFailure) : IConnection
    {
        public bool IsDisposed { get; private set; }

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.FromException(writeFailure);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    // One starred group TLS 1.3 can share is enough (measured 2026-10-01, BL-1082): it alone
    // gets the key share.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithATls12OnlyAndATls13GroupStarred_SharesTheTls13Group()
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Curves: "*brainpoolP256r1:*P-384"), OpenSslBuild, ProfileHost, Http11));

        CollectionAssert.AreEqual(new ushort[] { 0x0018 }, KeySharesOf(hello).Select(share => share.Group).ToArray());
    }

    // A TLS 1.2 ceiling sends no key share, so stars on TLS 1.2-only groups alone do not fail
    // it: the TLS 1.2 ClientHello offers the groups (measured 2026-10-01, BL-1082).
    [TestMethod]
    [DataRow("*brainpoolP256r1:P-384", new ushort[] { 0x001a, 0x0018 })]
    [DataRow("*brainpoolP256r1:X25519", new ushort[] { 0x001a, 0x001d })]
    [DataRow("*brainpoolP256r1", new ushort[] { 0x001a })]
    public async Task AuthenticateAsClientAsync_WithOnlyTls12OnlyGroupsStarredUnderATls12Ceiling_OffersTheGroups(string curves, ushort[] groups)
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions(Curves: curves)), OpenSslBuild, ProfileHost, Http11));

        CollectionAssert.AreEqual(groups, SupportedGroupsExtension.Decode(ExtensionData(hello, TlsExtensionType.SupportedGroups)).Value.ToArray());
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.KeyShare));
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
