using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="IConnection.ShutDownSendingAsync" /> on both TLS connections (BL-3179,
/// ADR-0770): <see cref="SslStreamConnection" /> and <see cref="HandBuiltTlsConnection" /> send
/// <c>close_notify</c>, which the server-side <see cref="SslStream" /> over an
/// <see cref="InMemoryDuplexStream" /> pair reads as the end of the stream, then shut the plaintext
/// connection under them down.
/// </summary>
[TestClass]
public sealed class TlsConnectionShutDownSendingTests
{
    private const string CertificateHost = "localhost";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 990);

    private static X509Certificate2 s_serverCertificate = null!;

    [ClassInitialize]
    public static void CreateCertificate(TestContext context)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(CertificateHost);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        s_serverCertificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    [ClassCleanup]
    public static void DisposeCertificate() => s_serverCertificate.Dispose();

    [TestMethod]
    public Task ShutDownSendingAsync_OverSslStream_SendsCloseNotify() =>
        AssertServerReadsCloseNotifyAsync(client => new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild: false)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None));

    [TestMethod]
    public Task ShutDownSendingAsync_OverTheHandBuiltClient_SendsCloseNotify() =>
        AssertServerReadsCloseNotifyAsync(client => new HandBuiltTlsProvider(
                new TlsClientOptions(Insecure: true) { MaximumVersion = TlsVersion.Tls12 },
                matchesSchannelBuild: false,
                TimeProvider.System,
                new FakeClientCertificateStore(),
                SystemTlsRandomSource.Instance)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None));

    private static async Task AssertServerReadsCloseNotifyAsync(Func<InMemoryDuplexStream, ValueTask<ConnectResult>> connect)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server, leaveInnerStreamOpen: true);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = s_serverCertificate,
                EnabledSslProtocols = SslProtocols.Tls12,
            });
            return await sslStream.ReadAsync(new byte[16]);
        });
        var result = await connect(client);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;

        await connection.ShutDownSendingAsync(CancellationToken.None);

        Assert.AreEqual(0, await serverTask, "The server reads close_notify as the end of the stream.");
    }
}
