using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// CGR-094 of the third-party gap report (BL-3623): OpenSSL 3's default security level allows
/// no version below TLS 1.2, so curl's OpenSSL build raises a <c>--tlsv1.0</c> or
/// <c>--tlsv1.1</c> minimum to TLS 1.2 under a TLS 1.2 or later ceiling. A server speaking only
/// TLS 1.1 then alerts <c>protocol_version</c> to the hello, or its TLS 1.1 ServerHello is
/// refused, unless <c>--ciphers</c> ends at <c>@SECLEVEL=0</c> (measured against curl 8.18.0
/// OpenSSL 3.5.5, 2026-10-26).
/// </summary>
[TestClass]
public sealed class Cgr094Tests
{
    private const string CertificateHost = "localhost";

    private const bool OpenSslBuild = false;

    private const bool SchannelBuild = true;

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 443);

    // openssl s_server -tls1_1's answer to a hello offering only TLS 1.2 and 1.3.
    private static readonly byte[] ProtocolVersionAlert = [0x15, 0x03, 0x01, 0x00, 0x02, 0x02, 0x46];

    private static X509Certificate2 s_serverCertificate = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [ClassInitialize]
    public static void CreateCertificate(TestContext context)
    {
        using var key = RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        s_serverCertificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    [ClassCleanup]
    public static void DisposeCertificate() => s_serverCertificate.Dispose();

    [TestMethod]
    [DataRow(TlsVersion.Tls10)]
    [DataRow(TlsVersion.Tls11)]
    public async Task AuthenticateAsClientAsync_WithALegacyMinimumInTheOpenSslBuild_OffersNoVersionBelowTls12AndReportsTheServersAlert(TlsVersion minimum)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: minimum);
        Diagnostics.Arrange("options", options);
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var header = new byte[5];
            await server.ReadExactlyAsync(header);
            var body = new byte[(header[3] << 8) | header[4]];
            await server.ReadExactlyAsync(body);
            await server.WriteAsync(ProtocolVersionAlert);
            return body;
        });

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        var hello = ClientHello.Decode((await serverTask)[4..]).Value;
        var offered = SupportedVersionsExtension.DecodeOffered(hello.Extensions.Single(extension => extension.Type == TlsExtensionType.SupportedVersions).Data).Value;
        Diagnostics.Act("offered versions", string.Join(",", offered.Select(version => version.ToString("x4", System.Globalization.CultureInfo.InvariantCulture))));
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        CollectionAssert.AreEqual(new ushort[] { 0x0304, 0x0303 }, offered.ToArray());
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version", result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithATls11MinimumAndATls12CeilingInTheOpenSslBuild_RefusesATls11ServerHello()
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls11, MaximumVersion: TlsVersion.Tls12);

        var result = await HandshakeWithTls11ServerAsync(options, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A000102:SSL routines::unsupported protocol", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(OpenSslBuild, "DEFAULT@SECLEVEL=0")]
    [DataRow(OpenSslBuild, "DEFAULT@SECLEVEL=2:@SECLEVEL=0")]
    [DataRow(SchannelBuild, null)]
    public async Task AuthenticateAsClientAsync_WithATls11MinimumTheSecurityLevelAllows_CompletesWithATls11Server(bool matchesSchannelBuild, string? ciphers)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls11, Ciphers: ciphers);

        var result = await HandshakeWithTls11ServerAsync(options, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow("DEFAULT@SECLEVEL=0:@SECLEVEL=1")]
    [DataRow("DEFAULT@SECLEVEL=1")]
    [DataRow("DEFAULT")]
    public async Task AuthenticateAsClientAsync_WithATls11MinimumAndCiphersLeavingTheSecurityLevelAboveZero_FailsWithATls11Server(string ciphers)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls11, Ciphers: ciphers);

        var result = await HandshakeWithTls11ServerAsync(options, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode, result.ErrorMessage);
    }

    private async Task<ConnectResult> HandshakeWithTls11ServerAsync(TlsClientOptions options, bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", options);
        using var rsaKey = s_serverCertificate.GetRSAPrivateKey()!;
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(() => new LegacyTlsTestServer(server, TlsProtocolVersion.Tls11, rsaKey, s_serverCertificate.RawData).HandshakeAsync());

        var result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        if (result.ExitCode != CurlExitCode.Ok)
        {
            await server.DisposeAsync();
        }

        await Task.WhenAny(serverTask);
        return result;
    }

    private static HandBuiltTlsProvider Provider(TlsClientOptions options, bool matchesSchannelBuild) =>
        new(options, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);
}
