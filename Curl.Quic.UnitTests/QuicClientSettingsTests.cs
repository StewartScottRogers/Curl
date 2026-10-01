using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// The two QUIC ClientHellos (ADR-0144 section 5, ADR-0140): curl.se's LibreSSL build's on
/// Windows and the OpenSSL build's TLS 1.3 parts of <see cref="ClientHelloProfile.OpenSsl" /> elsewhere.
/// </summary>
[TestClass]
public sealed class QuicClientSettingsTests
{
    [TestMethod]
    public void CreateOpenSslTlsSettings_TakesTheOpenSslProfilesTls13PartsWithQuicTransportParametersLast()
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
        CollectionAssert.AreEqual(
            new[]
            {
                TlsExtensionType.ServerName, TlsExtensionType.SupportedGroups, TlsExtensionType.ApplicationLayerProtocolNegotiation,
                TlsExtensionType.SignatureAlgorithms, TlsExtensionType.SupportedVersions, TlsExtensionType.PskKeyExchangeModes,
                TlsExtensionType.KeyShare, TlsExtensionType.CompressCertificate, TlsExtensionType.QuicTransportParameters,
            },
            settings.ExtensionOrder.ToArray());
        CollectionAssert.AreEqual(PskKeyExchangeModesExtension.Encode(profile.PskKeyExchangeModes).Data, settings.FixedExtensions.Single().Data);
    }

    [TestMethod]
    public void CreateOpenSslTlsSettings_OffersEverySignatureSchemeOfTheOpenSslProfile()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings(null);

        Assert.IsNull(settings.ServerName);
        Assert.Contains((ushort)0x0905, settings.SignatureAlgorithms);
        CollectionAssert.AreEqual(ClientHelloProfile.OpenSsl.SignatureAlgorithms.ToArray(), settings.SignatureAlgorithms.ToArray());
    }

    [TestMethod]
    public void CreateLibreSslTlsSettings_PutsQuicTransportParametersFirstAsMeasured()
    {
        Tls13ClientSettings settings = QuicClientSettings.CreateLibreSslTlsSettings("example.com");

        Assert.AreEqual(TlsExtensionType.QuicTransportParameters, settings.ExtensionOrder[0]);
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, settings.CipherSuites.ToArray());
    }
}
