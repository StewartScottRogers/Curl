using System.Globalization;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// The two QUIC ClientHellos (ADR-0144 section 5, ADR-0140, ADR-0291): curl.se's LibreSSL
/// build's on Windows and the OpenSSL build's, measured by BL-957, elsewhere.
/// </summary>
[TestClass]
public sealed class QuicClientSettingsTests
{
    // BL-957's capture: the first Initial of Fedora's curl 8.18.0 (OpenSSL 3.5.7, ngtcp2 1.22.1,
    // nghttp3 1.18.0) running "curl --http3-only https://host.docker.internal:4433/" against
    // Record-CurlExchange.ps1 -UdpSink, its Initial protection removed (RFC 9001 section 5.2).
    // No Ubuntu curl speaks HTTP/3, so Fedora's build stands in; its crypto policy sets its own
    // groups, suites and signature schemes, which is why the lists below are compared with the
    // same build's TCP hello rather than with ClientHelloProfile.OpenSsl's.
    private static readonly TlsExtensionType[] CapturedExtensionOrder =
    [
        TlsExtensionType.QuicTransportParameters, TlsExtensionType.ServerName, TlsExtensionType.EcPointFormats,
        TlsExtensionType.SupportedGroups, TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.EncryptThenMac,
        TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.PostHandshakeAuth, TlsExtensionType.SignatureAlgorithms,
        TlsExtensionType.SupportedVersions, TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.KeyShare,
        TlsExtensionType.CompressCertificate,
    ];

    // The same build's TCP hello's signature_algorithms, and its QUIC hello's: the QUIC hello
    // drops ecdsa_sha224 (0303) and rsa_pkcs1_sha224 (0301) and keeps the ML-DSA schemes.
    private static readonly ushort[] CapturedTcpSignatureAlgorithms =
    [
        0x0904, 0x0905, 0x0906, 0x0403, 0x0503, 0x0603, 0x0807, 0x0808, 0x0809, 0x080a,
        0x080b, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601, 0x0303, 0x0301,
    ];

    private static readonly ushort[] CapturedQuicSignatureAlgorithms =
    [
        0x0904, 0x0905, 0x0906, 0x0403, 0x0503, 0x0603, 0x0807, 0x0808, 0x0809, 0x080a,
        0x080b, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601,
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CreateOpenSslTlsSettings_SendsTheCapturedExtensionOrder()
    {
        Diagnostics.Arrange("server name", "example.com");
        Diagnostics.Arrange("captured extension order", Names(CapturedExtensionOrder));
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("example.com");

        Diagnostics.Act("extension order", Names(settings.ExtensionOrder.ToArray()));
        Diagnostics.Assert("extension order", Names(CapturedExtensionOrder), Names(settings.ExtensionOrder.ToArray()));
        CollectionAssert.AreEqual(CapturedExtensionOrder, settings.ExtensionOrder.ToArray());
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_SendsTheCapturedFixedExtensions()
    {
        Diagnostics.Arrange("server name", "example.com");
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("example.com");

        // As captured: ec_point_formats 03 00 01 02, three empty extensions, psk_dhe_ke.
        TlsExtensionType[] expectedTypes = [TlsExtensionType.EcPointFormats, TlsExtensionType.EncryptThenMac, TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.PostHandshakeAuth, TlsExtensionType.PskKeyExchangeModes];
        TlsExtensionType[] actualTypes = settings.FixedExtensions.Select(extension => extension.Type).ToArray();
        Diagnostics.Act("fixed extension types", Names(actualTypes));
        Diagnostics.Assert("fixed extension types", Names(expectedTypes), Names(actualTypes));
        CollectionAssert.AreEqual(expectedTypes, actualTypes);
        Diagnostics.Bytes("ec_point_formats data", settings.FixedExtensions[0].Data);
        Diagnostics.Diff("ec_point_formats data", new byte[] { 0x03, 0x00, 0x01, 0x02 }, settings.FixedExtensions[0].Data);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x00, 0x01, 0x02 }, settings.FixedExtensions[0].Data);
        bool emptyExtensions = settings.FixedExtensions.Skip(1).Take(3).All(extension => extension.Data.Length == 0);
        Diagnostics.Assert("the three middle extensions are empty", true, emptyExtensions);
        Assert.IsTrue(emptyExtensions);
        Diagnostics.Bytes("psk_key_exchange_modes data", settings.FixedExtensions[4].Data);
        Diagnostics.Diff("psk_key_exchange_modes data", new byte[] { 0x01, 0x01 }, settings.FixedExtensions[4].Data);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x01 }, settings.FixedExtensions[4].Data);
    }

    // OpenSSL 3.5.5's own QUIC client with SSL_OP_ALL ("openssl s_client -quic -alpn h3 -bugs
    // -groups X25519" for a 75-character host name, BL-1156) sent a 335-byte hello with no
    // padding, where its TCP hello of the same options was padded to 512: QUIC hellos are
    // never padded, whatever their length.
    [TestMethod]
    public void CreateOpenSslTlsSettings_WithX25519AndAHelloOf256To511Bytes_SendsNoPadding()
    {
        Diagnostics.Arrange("host name", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.example");
        Diagnostics.Arrange("groups", "X25519 only");
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.example") with
        {
            SupportedGroups = [TlsNamedGroup.X25519],
            KeyShareGroups = [TlsNamedGroup.X25519],
        };

        byte[] helloBytes = new Tls13ClientHandshake(settings, new QuicTestRandomSource(), new QuicTestVerifier()).Start().BytesToSend[0].Bytes;
        ClientHello hello = ClientHello.Decode(helloBytes[4..]).Value!;

        Diagnostics.Bytes("ClientHello handshake message", helloBytes);
        Diagnostics.Act("hello length", helloBytes.Length);
        Diagnostics.Act("extensions", Names(hello.Extensions.Select(extension => extension.Type)));
        Diagnostics.Assert("hello length is in 256..511", true, helloBytes.Length is >= 256 and < 512);
        Assert.IsTrue(helloBytes.Length is >= 256 and < 512, $"The hello is {helloBytes.Length} bytes.");
        bool padded = hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Padding);
        Diagnostics.Assert("hello has a padding extension", false, padded);
        Assert.IsFalse(padded);
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_TakesTheOpenSslProfilesSuitesGroupsAndProtocols()
    {
        Diagnostics.Arrange("server name", "example.com");
        ClientHelloProfile profile = ClientHelloProfile.OpenSsl;

        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("example.com");

        Diagnostics.Act("server name", settings.ServerName);
        Diagnostics.Assert("server name", "example.com", settings.ServerName);
        Assert.AreEqual("example.com", settings.ServerName);
        Diagnostics.Act("cipher suites", Hex(settings.CipherSuites.ToArray()));
        Diagnostics.Assert("cipher suites", "1302 1303 1301", Hex(settings.CipherSuites.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, settings.CipherSuites.ToArray());
        Diagnostics.Assert("supported groups", Names(profile.SupportedGroups.ToArray()), Names(settings.SupportedGroups.ToArray()));
        CollectionAssert.AreEqual(profile.SupportedGroups.ToArray(), settings.SupportedGroups.ToArray());
        Diagnostics.Assert("key share groups", Names(profile.KeyShareGroups.ToArray()), Names(settings.KeyShareGroups.ToArray()));
        CollectionAssert.AreEqual(profile.KeyShareGroups.ToArray(), settings.KeyShareGroups.ToArray());
        Diagnostics.Assert("certificate compression algorithms", Names(profile.CertificateCompressionAlgorithms.ToArray()), Names(settings.CertificateCompressionAlgorithms.ToArray()));
        CollectionAssert.AreEqual(profile.CertificateCompressionAlgorithms.ToArray(), settings.CertificateCompressionAlgorithms.ToArray());
        Diagnostics.Assert("application protocols", "h3, h3-29", Names(settings.ApplicationProtocols.ToArray()));
        CollectionAssert.AreEqual(new[] { "h3", "h3-29" }, settings.ApplicationProtocols.ToArray());
        Diagnostics.Assert("sends a legacy session id", false, settings.SendLegacySessionId);
        Assert.IsFalse(settings.SendLegacySessionId);
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_CutsTheProfilesSignatureSchemesAsTheCaptureCutsTcpsOnes()
    {
        Diagnostics.Arrange("server name", "null");
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings(null);

        Diagnostics.Act("server name", settings.ServerName ?? "null");
        Diagnostics.Assert("server name", "null", settings.ServerName ?? "null");
        Assert.IsNull(settings.ServerName);
        ushort[] cutTcpSchemes = CapturedTcpSignatureAlgorithms.Where(scheme => scheme is not (TlsSignatureScheme.EcdsaSha224 or TlsSignatureScheme.RsaPkcs1Sha224)).ToArray();
        Diagnostics.Assert("captured QUIC signature algorithms", Hex(CapturedQuicSignatureAlgorithms), Hex(cutTcpSchemes));
        CollectionAssert.AreEqual(CapturedQuicSignatureAlgorithms, cutTcpSchemes);
        ushort[] expectedSchemes =
        [
            0x0905, 0x0906, 0x0904, 0x0403, 0x0503, 0x0603, 0x0807, 0x0808, 0x081a, 0x081b,
            0x081c, 0x0809, 0x080a, 0x080b, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601,
        ];
        Diagnostics.Act("signature algorithms", Hex(settings.SignatureAlgorithms.ToArray()));
        Diagnostics.Assert("signature algorithms", Hex(expectedSchemes), Hex(settings.SignatureAlgorithms.ToArray()));
        CollectionAssert.AreEqual(expectedSchemes, settings.SignatureAlgorithms.ToArray());
    }

    [TestMethod]
    public void CreateLibreSslTlsSettings_PutsQuicTransportParametersFirstAsMeasured()
    {
        Diagnostics.Arrange("server name", "example.com");
        Tls13ClientSettings settings = QuicClientSettings.CreateLibreSslTlsSettings("example.com");

        Diagnostics.Act("extension order", Names(settings.ExtensionOrder));
        Diagnostics.Assert("first extension", TlsExtensionType.QuicTransportParameters, settings.ExtensionOrder[0]);
        Assert.AreEqual(TlsExtensionType.QuicTransportParameters, settings.ExtensionOrder[0]);
        Diagnostics.Assert("cipher suites", "1302 1303 1301", Hex(settings.CipherSuites.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, settings.CipherSuites.ToArray());
        Diagnostics.Assert("offers the empty renegotiation_info SCSV", true, settings.OfferEmptyRenegotiationInfoScsv);
        Assert.IsTrue(settings.OfferEmptyRenegotiationInfoScsv);
    }

    private static string Hex(IEnumerable<ushort> values) =>
        string.Join(' ', values.Select(value => value.ToString("x4", CultureInfo.InvariantCulture)));

    private static string Names<T>(IEnumerable<T> values) => string.Join(", ", values);
}
