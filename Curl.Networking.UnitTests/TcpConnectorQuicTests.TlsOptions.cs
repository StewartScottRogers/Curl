using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <see cref="QuicDialer" /> with <c>--cert</c>, <c>--ciphers</c> and <c>--tls13-ciphers</c>, and
/// the OpenSSL build's ClientHello (BL-847, ADR-0140): the handshake runs against the in-memory
/// server, which records the ClientHello and any certificate the client presents.
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    private static readonly ECDsa s_clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static readonly X509Certificate2 s_clientCertificate =
        new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Curl QUIC Client", s_clientKey, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

    private readonly string _certificateDirectory = Path.Combine(Path.GetTempPath(), $"bl847-{Guid.NewGuid():N}");

    private void DeleteCertificateFiles()
    {
        if (Directory.Exists(_certificateDirectory))
        {
            Directory.Delete(_certificateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithCertWhenTheServerAsksForOne_PresentsTheCertificate()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(requestClientCertificate: true) };
        var options = new TlsClientOptions(
            Insecure: true,
            ClientCertificate: Escaped(WriteFile("client.pem", s_clientCertificate.ExportCertificatePem())),
            PrivateKey: WriteFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem()));

        var result = await Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild: false).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        var presented = opener.Opened.Single().Server!.Tls!.ClientCertificate!.CertificateList.Single();
        CollectionAssert.AreEqual(s_clientCertificate.RawData, presented.CertificateData);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_InTheWindowsBuildWithCertInACertificateFile_PresentsTheCertificate()
    {
        // curl.se's LibreSSL build reads --cert as OpenSSL does, keeping a drive letter's colon.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(requestClientCertificate: true) };
        var file = WriteFile("both.pem", s_clientCertificate.ExportCertificatePem() + "\n" + s_clientKey.ExportPkcs8PrivateKeyPem());
        var options = new TlsClientOptions(Insecure: true, ClientCertificate: file);

        var result = await Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild: true).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Assert.IsNotNull(opener.Opened.Single().Server!.Tls!.ClientCertificate);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithACertFileThatDoesNotExist_FailsWithExit58BeforeOpeningAChannel()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var options = new TlsClientOptions(Insecure: true, ClientCertificate: Escaped(Path.Combine(_certificateDirectory, "missing.pem")));

        var result = await Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild: false).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.IsEmpty(opener.Opened);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WithTls13Ciphers_OffersOnlyTheSuitesItNames()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(cipherSuite: 0x1302) };
        var options = new TlsClientOptions(Insecure: true, Tls13Ciphers: "TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256");

        var result = await Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild: false).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303 }, opener.Opened.Single().Server!.Tls!.ClientHello!.CipherSuites.ToArray());
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_InTheWindowsBuildWithCiphers_OffersTheDefaultTls13SuitesRatherThanRefusing()
    {
        // The Schannel build refuses --ciphers over TCP; curl.se's LibreSSL build, which dials QUIC, does not.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var options = new TlsClientOptions(Insecure: true, Ciphers: "ECDHE-RSA-AES128-GCM-SHA256");

        var result = await Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild: true).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, opener.Opened.Single().Server!.Tls!.ClientHello!.CipherSuites.ToArray());
    }

    [TestMethod]
    [DataRow("NO-SUCH-SUITE", DisplayName = "no suite it knows")]
    [DataRow("TLS_AES_128_CCM_8_SHA256", DisplayName = "only a suite QUIC cannot protect packets with")]
    public async Task ConnectMultiplexedAsync_WithTls13CiphersThatLeaveNoSuite_FailsWithExit59AndTheHandBuiltProvidersMessage(string tls13Ciphers)
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, Tls13Ciphers: tls13Ciphers);

        var result = await Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild: false).ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual(TlsFailureMessages.OpenSslTls13CipherSuiteUnusable(tls13Ciphers), result.ErrorMessage);
        Assert.IsEmpty(opener.Opened);
        Assert.IsEmpty(events.TlsEvents);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_InTheOpenSslBuild_SendsTheOpenSslProfilesTls13ClientHello()
    {
        // ADR-0140 and BL-847: ClientHelloProfile.OpenSsl (BL-787's capture) cut to its TLS 1.3
        // parts, in its extension order, with quic_transport_parameters last and no session ID.
        var profile = ClientHelloProfile.OpenSsl;
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };

        var result = await Connector(opener, new ManualTimeProvider(), matchesSchannelBuild: false).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        var hello = opener.Opened.Single().Server!.Tls!.ClientHello!;
        Assert.IsEmpty(hello.LegacySessionId);
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                TlsExtensionType.ServerName, TlsExtensionType.SupportedGroups, TlsExtensionType.ApplicationLayerProtocolNegotiation,
                TlsExtensionType.SignatureAlgorithms, TlsExtensionType.SupportedVersions, TlsExtensionType.PskKeyExchangeModes,
                TlsExtensionType.KeyShare, TlsExtensionType.CompressCertificate, TlsExtensionType.QuicTransportParameters,
            },
            hello.Extensions.Select(extension => extension.Type).ToArray());
        CollectionAssert.AreEqual(SupportedGroupsExtension.Encode(profile.SupportedGroups).Data, Data(hello, TlsExtensionType.SupportedGroups));
        CollectionAssert.AreEqual(PskKeyExchangeModesExtension.Encode(profile.PskKeyExchangeModes).Data, Data(hello, TlsExtensionType.PskKeyExchangeModes));
        CollectionAssert.AreEqual(CompressCertificateExtension.Encode(profile.CertificateCompressionAlgorithms).Data, Data(hello, TlsExtensionType.CompressCertificate));
        CollectionAssert.AreEqual(ApplicationLayerProtocolNegotiationExtension.Encode(["h3", "h3-29"]).Data, Data(hello, TlsExtensionType.ApplicationLayerProtocolNegotiation));
        CollectionAssert.AreEqual(
            profile.KeyShareGroups.ToArray(),
            KeyShareExtension.DecodeClientShares(Data(hello, TlsExtensionType.KeyShare)).Value!.Select(share => share.Group).ToArray());
        var expectedSchemes = profile.SignatureAlgorithms.Where(scheme => TlsSignatureScheme.IsCertificateVerifyScheme(scheme) || TlsSignatureScheme.IsTls12Scheme(scheme)).ToArray();
        CollectionAssert.AreEqual(SignatureAlgorithmsExtension.Encode(expectedSchemes).Data, Data(hello, TlsExtensionType.SignatureAlgorithms));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_InTheWindowsBuild_SendsCurlSesLibreSslClientHello()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };

        var result = await Connector(opener, new ManualTimeProvider(), matchesSchannelBuild: true).ConnectMultiplexedAsync(Target(), CancellationToken.None);

        await using var connection = result.Connection!;
        var hello = opener.Opened.Single().Server!.Tls!.ClientHello!;
        CollectionAssert.AreEqual(
            QuicClientSettings.CreateLibreSslTlsSettings("quic.test").ExtensionOrder.ToArray(),
            hello.Extensions.Select(extension => extension.Type).ToArray());
    }

    private static byte[] Data(ClientHello hello, TlsExtensionType type) => hello.Extensions.Single(extension => extension.Type == type).Data;

    // The OpenSSL build splits --cert at an unescaped colon, so a Windows path's drive letter is escaped.
    private static string Escaped(string path) => path.Replace(":", @"\:", StringComparison.Ordinal);

    private string WriteFile(string fileName, string content)
    {
        Directory.CreateDirectory(_certificateDirectory);
        var path = Path.Combine(_certificateDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
