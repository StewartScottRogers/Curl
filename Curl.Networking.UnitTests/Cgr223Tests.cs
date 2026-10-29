using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-223 (BL-3752): a fatal TLS alert after the handshake (certificate required, unknown CA)
/// fails the read with curl's own text, <c>OpenSSL SSL_read: ... alert ...</c> or Schannel's
/// <c>failed to read data from server: SEC_E_ILLEGAL_MESSAGE</c>, not the generic receive failure.
/// </summary>
[TestClass]
public sealed class Cgr223Tests
{
    private const int SecEIllegalMessage = unchecked((int)0x80090326);

    private static X509Certificate2 s_serverCertificate = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [ClassInitialize]
    public static void CreateCertificate(TestContext context)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        s_serverCertificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    [ClassCleanup]
    public static void DisposeCertificate() => s_serverCertificate.Dispose();

    [TestMethod]
    [DataRow(false, "OpenSSL SSL_read: OpenSSL/3.5.5: error:0A00045C:SSL routines::tlsv13 alert certificate required, errno 0")]
    [DataRow(true, "OpenSSL SSL_read: OpenSSL/3.5.5: error:0A000418:SSL routines::tlsv1 alert unknown ca, errno 0")]
    public void AlertReadFailure_InTheOpenSslBuild_NamesTheAlertTheClientCertificateImplies(bool presentedClientCertificate, string expected)
    {
        var failure = new IOException("read failed", new InvalidOperationException("SSL_ERROR_SSL"));

        var message = TlsFailureMessages.AlertReadFailure(failure, matchesSchannelBuild: false, presentedClientCertificate);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    public void AlertReadFailure_InTheOpenSslBuildWithoutAnInnerFailure_IsNull()
    {
        Assert.IsNull(TlsFailureMessages.AlertReadFailure(new IOException("x"), matchesSchannelBuild: false, presentedClientCertificate: false));
    }

    [TestMethod]
    public void AlertReadFailure_InTheOpenSslBuildForASocketError_IsNull()
    {
        var failure = new IOException("x", new System.Net.Sockets.SocketException(10054));

        Assert.IsNull(TlsFailureMessages.AlertReadFailure(failure, matchesSchannelBuild: false, presentedClientCertificate: false));
    }

    [TestMethod]
    [DataRow(SecEIllegalMessage)]
    [DataRow(unchecked((int)0x80090330))]
    public void AlertReadFailure_InTheSchannelBuildForAnAlertStatus_ReportsTheFailedReadWithIllegalMessage(int status)
    {
        var failure = new IOException("x", new Win32Exception(status));

        var message = TlsFailureMessages.AlertReadFailure(failure, matchesSchannelBuild: true, presentedClientCertificate: false);

        Assert.IsNotNull(message);
        Assert.StartsWith("schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received", message);
    }

    [TestMethod]
    public void AlertReadFailure_InTheSchannelBuildForAnyOtherFailure_IsNull()
    {
        Assert.IsNull(TlsFailureMessages.AlertReadFailure(new IOException("x"), matchesSchannelBuild: true, presentedClientCertificate: false));
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText()
    {
        var matchesSchannelBuild = OperatingSystem.IsWindows();
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild);
        var serverTask = Task.Run(async () =>
        {
            var serverStream = new SslStream(server, leaveInnerStreamOpen: true);
            await serverStream.AuthenticateAsServerAsync(s_serverCertificate);
            await server.WriteAsync(new byte[] { 0x63, 0x03, 0x03, 0x00, 0x05, 1, 2, 3, 4, 5 });
            return serverStream;
        });

        var result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, new IPEndPoint(IPAddress.Loopback, 443)), "localhost", CancellationToken.None);
        using var serverSide = await serverTask;
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);

        var failure = await Assert.ThrowsExactlyAsync<MissingCloseNotifyException>(
            async () => await result.Connection!.ReadAsync(new byte[16], CancellationToken.None));

        Diagnostics.Act("message", failure.Message);
        var expectedStart = matchesSchannelBuild ? "schannel: failed to read data from server: " : "OpenSSL SSL_read: OpenSSL/3.5.5: error:0A00045C";
        Assert.StartsWith(expectedStart, failure.Message);
    }
}
