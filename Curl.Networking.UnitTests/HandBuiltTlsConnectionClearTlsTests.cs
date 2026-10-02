using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="HandBuiltTlsConnection.ClearTlsAsync" />, which FTP's <c>CCC</c> uses under
/// <c>--tls-max 1.0</c>/<c>1.1</c> or <c>--cert-status</c> (BL-1042, ADR-0280), as
/// <see cref="SslStreamConnectionClearTlsTests" /> pins <see cref="SslStreamConnection" />:
/// matching the OpenSSL build it sends <c>close_notify</c> first only when asked, reads the
/// server's, and hands back the plaintext connection; matching the Schannel build it sends
/// <c>close_notify</c> and fails; data, an end or a corrupt record in place of the server's
/// <c>close_notify</c> fails it. The server is a server-side <see cref="SslStream" /> over an
/// <see cref="InMemoryDuplexStream" /> pair; TLS 1.3 runs where the platform's server has it.
/// </summary>
[TestClass]
public sealed class HandBuiltTlsConnectionClearTlsTests
{
    private const string CertificateHost = "localhost";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 990);

    private static X509Certificate2 s_serverCertificate = null!;

    /// <summary>What the test server does after the handshake instead of sending <c>close_notify</c>.</summary>
    public enum ServerAnswer
    {
        /// <summary>Sends application data.</summary>
        Data,

        /// <summary>Closes the transport at a record boundary.</summary>
        BareEnd,

        /// <summary>Sends the first bytes of a record, then closes the transport.</summary>
        EndInsideARecord,

        /// <summary>Sends a whole record that does not decrypt.</summary>
        CorruptRecord,
    }

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
    public Task ClearTlsAsync_ActiveOverTls12AsTheOpenSslBuild_SendsCloseNotifyThenPlainText() =>
        AssertActiveSendsCloseNotifyThenPlainTextAsync(SslProtocols.Tls12);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public Task ClearTlsAsync_ActiveOverTls13AsTheOpenSslBuild_SendsCloseNotifyThenPlainText() =>
        AssertActiveSendsCloseNotifyThenPlainTextAsync(SslProtocols.Tls13);

    [TestMethod]
    public Task ClearTlsAsync_PassiveOverTls12AsTheOpenSslBuild_SendsOnlyThePlainText() =>
        AssertPassiveSendsOnlyThePlainTextAsync(SslProtocols.Tls12);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public Task ClearTlsAsync_PassiveOverTls13AsTheOpenSslBuild_SendsOnlyThePlainText() =>
        AssertPassiveSendsOnlyThePlainTextAsync(SslProtocols.Tls13);

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public Task ClearTlsAsync_OverTls12AsTheSchannelBuild_SendsCloseNotifyAndReturnsNull(bool sendCloseNotifyFirst) =>
        AssertSchannelBuildSendsCloseNotifyAndReturnsNullAsync(SslProtocols.Tls12, sendCloseNotifyFirst);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(true)]
    [DataRow(false)]
    public Task ClearTlsAsync_OverTls13AsTheSchannelBuild_SendsCloseNotifyAndReturnsNull(bool sendCloseNotifyFirst) =>
        AssertSchannelBuildSendsCloseNotifyAndReturnsNullAsync(SslProtocols.Tls13, sendCloseNotifyFirst);

    [TestMethod]
    [DataRow(ServerAnswer.Data)]
    [DataRow(ServerAnswer.BareEnd)]
    [DataRow(ServerAnswer.EndInsideARecord)]
    [DataRow(ServerAnswer.CorruptRecord)]
    public Task ClearTlsAsync_OverTls12WhenTheServerSendsNoCloseNotify_ReturnsNull(ServerAnswer answer) =>
        AssertNoCloseNotifyReturnsNullAsync(SslProtocols.Tls12, answer);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(ServerAnswer.Data)]
    [DataRow(ServerAnswer.BareEnd)]
    [DataRow(ServerAnswer.EndInsideARecord)]
    [DataRow(ServerAnswer.CorruptRecord)]
    public Task ClearTlsAsync_OverTls13WhenTheServerSendsNoCloseNotify_ReturnsNull(ServerAnswer answer) =>
        AssertNoCloseNotifyReturnsNullAsync(SslProtocols.Tls13, answer);

    private static async Task AssertActiveSendsCloseNotifyThenPlainTextAsync(SslProtocols protocol)
    {
        var sent = await ClearAndSendPwdAsync(protocol, sendCloseNotifyFirst: true);

        Assert.AreEqual(CloseNotifyRecordType(protocol), sent[0], "close_notify goes first.");
        Assert.IsGreaterThan(5, sent.Length);
        Assert.AreEqual("PWD\r\n", Encoding.ASCII.GetString(sent[^5..]));
    }

    private static async Task AssertPassiveSendsOnlyThePlainTextAsync(SslProtocols protocol)
    {
        var sent = await ClearAndSendPwdAsync(protocol, sendCloseNotifyFirst: false);

        Assert.AreEqual("PWD\r\n", Encoding.ASCII.GetString(sent));
    }

    private static async Task AssertSchannelBuildSendsCloseNotifyAndReturnsNullAsync(SslProtocols protocol, bool sendCloseNotifyFirst)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            _ = await HandshakeAsync(server, protocol);
            var first = new byte[1];
            _ = await server.ReadAsync(first);
            return first[0];
        });
        await using var connection = await ConnectAsync(client, protocol, matchesSchannelBuild: true);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst, CancellationToken.None);

        Assert.IsNull(plaintext);
        Assert.AreEqual(CloseNotifyRecordType(protocol), await serverTask);
    }

    private static async Task AssertNoCloseNotifyReturnsNullAsync(SslProtocols protocol, ServerAnswer answer)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var sslStream = await HandshakeAsync(server, protocol);
            switch (answer)
            {
                case ServerAnswer.Data:
                    await sslStream.WriteAsync("x"u8.ToArray());
                    await sslStream.FlushAsync();
                    return;
                case ServerAnswer.EndInsideARecord:
                    await server.WriteAsync(new byte[] { 0x17, 0x03, 0x03, 0x00, 0x40, 0x01, 0x02 });
                    break;
                case ServerAnswer.CorruptRecord:
                    await server.WriteAsync((byte[])[0x17, 0x03, 0x03, 0x00, 0x40, .. new byte[0x40]]);
                    return;
            }

            await server.DisposeAsync();
        });
        await using var connection = await ConnectAsync(client, protocol, matchesSchannelBuild: false);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst: false, CancellationToken.None);

        Assert.IsNull(plaintext);
        await IgnoreFailureAsync(serverTask);
    }

    // Clears TLS matching the OpenSSL build, sends "PWD\r\n" over the plaintext connection
    // and reads the server's plain "257\r\n"; returns the raw bytes the server read after its
    // own close_notify.
    private static async Task<byte[]> ClearAndSendPwdAsync(SslProtocols protocol, bool sendCloseNotifyFirst)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var sslStream = await HandshakeAsync(server, protocol);
            await sslStream.ShutdownAsync();
            var received = new List<byte>();
            var buffer = new byte[256];
            while (!Encoding.ASCII.GetString([.. received]).EndsWith("PWD\r\n", StringComparison.Ordinal))
            {
                var read = await server.ReadAsync(buffer);
                Assert.AreNotEqual(0, read, "The client ended before sending PWD.");
                received.AddRange(buffer[..read]);
            }

            await server.WriteAsync("257\r\n"u8.ToArray());
            return received.ToArray();
        });
        await using var connection = await ConnectAsync(client, protocol, matchesSchannelBuild: false);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst, CancellationToken.None);
        Assert.IsNotNull(plaintext);
        Assert.IsFalse(plaintext.IsSecure);
        await plaintext.WriteAsync("PWD\r\n"u8.ToArray(), CancellationToken.None);
        var reply = new byte[5];
        var total = 0;
        while (total < reply.Length)
        {
            total += await plaintext.ReadAsync(reply.AsMemory(total), CancellationToken.None);
        }

        Assert.AreEqual("257\r\n", Encoding.ASCII.GetString(reply));
        return await serverTask;
    }

    // TLS 1.3 sends every record after the handshake as application_data (RFC 8446 section 5.2).
    private static byte CloseNotifyRecordType(SslProtocols protocol) => protocol == SslProtocols.Tls13 ? (byte)0x17 : (byte)0x15;

    private static async Task<IConnection> ConnectAsync(InMemoryDuplexStream client, SslProtocols protocol, bool matchesSchannelBuild)
    {
        var options = new TlsClientOptions(Insecure: true);
        if (protocol == SslProtocols.Tls12)
        {
            options = options with { MaximumVersion = TlsVersion.Tls12 };
        }

        var provider = new HandBuiltTlsProvider(options, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);
        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsInstanceOfType<HandBuiltTlsConnection>(result.Connection);
        return result.Connection!;
    }

    private static async Task<SslStream> HandshakeAsync(InMemoryDuplexStream server, SslProtocols protocol)
    {
        var sslStream = new SslStream(server, leaveInnerStreamOpen: true);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = protocol,
        });
        return sslStream;
    }

    private static async Task IgnoreFailureAsync(Task serverTask)
    {
        try
        {
            await serverTask;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or ObjectDisposedException)
        {
            // The server side fails when the client goes first.
        }
    }
}
