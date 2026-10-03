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

    [TestMethod]
    public void CreateOpenSslTlsSettings_SendsTheCapturedExtensionOrder()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("example.com");

        CollectionAssert.AreEqual(CapturedExtensionOrder, settings.ExtensionOrder.ToArray());
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_SendsTheCapturedFixedExtensions()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("example.com");

        // As captured: ec_point_formats 03 00 01 02, three empty extensions, psk_dhe_ke.
        CollectionAssert.AreEqual(
            new[] { TlsExtensionType.EcPointFormats, TlsExtensionType.EncryptThenMac, TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.PostHandshakeAuth, TlsExtensionType.PskKeyExchangeModes },
            settings.FixedExtensions.Select(extension => extension.Type).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x00, 0x01, 0x02 }, settings.FixedExtensions[0].Data);
        Assert.IsTrue(settings.FixedExtensions.Skip(1).Take(3).All(extension => extension.Data.Length == 0));
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x01 }, settings.FixedExtensions[4].Data);
    }

    // OpenSSL 3.5.5's own QUIC client with SSL_OP_ALL ("openssl s_client -quic -alpn h3 -bugs
    // -groups X25519" for a 75-character host name, BL-1156) sent a 335-byte hello with no
    // padding, where its TCP hello of the same options was padded to 512: QUIC hellos are
    // never padded, whatever their length.
    [TestMethod]
    public void CreateOpenSslTlsSettings_WithX25519AndAHelloOf256To511Bytes_SendsNoPadding()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.example") with
        {
            SupportedGroups = [TlsNamedGroup.X25519],
            KeyShareGroups = [TlsNamedGroup.X25519],
        };

        byte[] helloBytes = new Tls13ClientHandshake(settings, new QuicTestRandomSource(), new QuicTestVerifier()).Start().BytesToSend[0].Bytes;
        ClientHello hello = ClientHello.Decode(helloBytes[4..]).Value!;

        Assert.IsTrue(helloBytes.Length is >= 256 and < 512, $"The hello is {helloBytes.Length} bytes.");
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Padding));
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_TakesTheOpenSslProfilesSuitesGroupsAndProtocols()
    {
        ClientHelloProfile profile = ClientHelloProfile.OpenSsl;

        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("example.com");

        Assert.AreEqual("example.com", settings.ServerName);
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, settings.CipherSuites.ToArray());
        CollectionAssert.AreEqual(profile.SupportedGroups.ToArray(), settings.SupportedGroups.ToArray());
        CollectionAssert.AreEqual(profile.KeyShareGroups.ToArray(), settings.KeyShareGroups.ToArray());
        CollectionAssert.AreEqual(profile.CertificateCompressionAlgorithms.ToArray(), settings.CertificateCompressionAlgorithms.ToArray());
        CollectionAssert.AreEqual(new[] { "h3", "h3-29" }, settings.ApplicationProtocols.ToArray());
        Assert.IsFalse(settings.SendLegacySessionId);
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_CutsTheProfilesSignatureSchemesAsTheCaptureCutsTcpsOnes()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings(null);

        Assert.IsNull(settings.ServerName);
        CollectionAssert.AreEqual(
            CapturedQuicSignatureAlgorithms,
            CapturedTcpSignatureAlgorithms.Where(scheme => scheme is not (TlsSignatureScheme.EcdsaSha224 or TlsSignatureScheme.RsaPkcs1Sha224)).ToArray());
        CollectionAssert.AreEqual(
            new ushort[]
            {
                0x0905, 0x0906, 0x0904, 0x0403, 0x0503, 0x0603, 0x0807, 0x0808, 0x081a, 0x081b,
                0x081c, 0x0809, 0x080a, 0x080b, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601,
            },
            settings.SignatureAlgorithms.ToArray());
    }

    [TestMethod]
    public void CreateLibreSslTlsSettings_PutsQuicTransportParametersFirstAsMeasured()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateLibreSslTlsSettings("example.com");

        Assert.AreEqual(TlsExtensionType.QuicTransportParameters, settings.ExtensionOrder[0]);
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, settings.CipherSuites.ToArray());
        Assert.IsTrue(settings.OfferEmptyRenegotiationInfoScsv);
    }
}
