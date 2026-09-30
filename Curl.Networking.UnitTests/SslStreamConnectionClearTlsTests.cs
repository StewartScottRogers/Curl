using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SslStreamConnection.ClearTlsAsync" />, which FTP's <c>CCC</c> uses (BL-636,
/// ADR-0280): matching the OpenSSL build it sends <c>close_notify</c> first only when asked,
/// reads the server's, and hands back the plaintext connection, over which later bytes travel
/// unencrypted; matching the Schannel build it sends <c>close_notify</c> and fails; data or an
/// end in place of the server's <c>close_notify</c> fails it. The server is a server-side
/// <see cref="SslStream" /> over an <see cref="InMemoryDuplexStream" /> pair, which reads what
/// follows its <c>close_notify</c> from the raw stream.
/// </summary>
[TestClass]
public sealed class SslStreamConnectionClearTlsTests
{
    private const string CertificateHost = "localhost";

    private const byte AlertRecordType = 0x15;

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
    public async Task ClearTlsAsync_ActiveAsTheOpenSslBuild_SendsCloseNotifyThenPlainText()
    {
        var sent = await ClearAndSendPwdAsync(sendCloseNotifyFirst: true);

        Assert.AreEqual(AlertRecordType, sent[0], "close_notify goes first.");
        Assert.AreEqual("PWD\r\n", Encoding.ASCII.GetString(sent[^5..]));
    }

    [TestMethod]
    public async Task ClearTlsAsync_PassiveAsTheOpenSslBuild_SendsOnlyThePlainText()
    {
        var sent = await ClearAndSendPwdAsync(sendCloseNotifyFirst: false);

        Assert.AreEqual("PWD\r\n", Encoding.ASCII.GetString(sent));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ClearTlsAsync_AsTheSchannelBuild_SendsCloseNotifyAndReturnsNull(bool sendCloseNotifyFirst)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            _ = await HandshakeAsync(server);
            var first = new byte[1];
            _ = await server.ReadAsync(first);
            return first[0];
        });
        await using var connection = await ConnectAsync(client, matchesSchannelBuild: true);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst, CancellationToken.None);

        Assert.IsNull(plaintext);
        Assert.AreEqual(AlertRecordType, await serverTask);
    }

    [TestMethod]
    [DataRow(ServerAnswer.Data)]
    [DataRow(ServerAnswer.BareEnd)]
    [DataRow(ServerAnswer.EndInsideARecord)]
    public async Task ClearTlsAsync_WhenTheServerSendsNoCloseNotify_ReturnsNull(ServerAnswer answer)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var sslStream = await HandshakeAsync(server);
            switch (answer)
            {
                case ServerAnswer.Data:
                    await sslStream.WriteAsync("x"u8.ToArray());
                    await sslStream.FlushAsync();
                    return;
                case ServerAnswer.EndInsideARecord:
                    await server.WriteAsync(new byte[] { 0x17, 0x03, 0x03, 0x00, 0x40, 0x01, 0x02 });
                    break;
            }

            await server.DisposeAsync();
        });
        await using var connection = await ConnectAsync(client, matchesSchannelBuild: false);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst: false, CancellationToken.None);

        Assert.IsNull(plaintext);
        await IgnoreFailureAsync(serverTask);
    }

    // Clears TLS matching the OpenSSL build, sends "PWD\r\n" over the plaintext connection
    // and reads the server's plain "257\r\n"; returns the raw bytes the server read after its
    // own close_notify.
    private static async Task<byte[]> ClearAndSendPwdAsync(bool sendCloseNotifyFirst)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var sslStream = await HandshakeAsync(server);
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
        await using var connection = await ConnectAsync(client, matchesSchannelBuild: false);

        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst, CancellationToken.None);
        Assert.IsNotNull(plaintext);
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

    private static async Task<IConnection> ConnectAsync(InMemoryDuplexStream client, bool matchesSchannelBuild)
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild);
        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        return result.Connection!;
    }

    private static async Task<SslStream> HandshakeAsync(InMemoryDuplexStream server)
    {
        var sslStream = new SslStream(server, leaveInnerStreamOpen: true);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = SslProtocols.Tls12,
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
