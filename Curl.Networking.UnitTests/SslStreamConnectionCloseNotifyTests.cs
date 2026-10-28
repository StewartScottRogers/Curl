using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SslStreamConnection.DisposeAsync" />'s <c>close_notify</c> (BL-3984,
/// ADR-0957): closing an https connection sends the alert first, as curl 8.18.0's OpenSSL build
/// and curl 8.22.0's Schannel build both do, but not a second time after TLS has ended, not after
/// the server has closed the transport, and a failure to send it does not escape. The server is a
/// server-side <see cref="SslStream" /> over an <see cref="InMemoryDuplexStream" /> pair, which
/// reads what the client sends after the handshake from the raw stream.
/// </summary>
[TestClass]
public sealed class SslStreamConnectionCloseNotifyTests
{
    private const string CertificateHost = "localhost";

    private const byte AlertRecordType = 0x15;

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 443);

    private static X509Certificate2 s_serverCertificate = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisposeAsync_AfterATransfer_SendsCloseNotify(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("matches Schannel build", matchesSchannelBuild);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            _ = await HandshakeAsync(server);
            return await ReadToEndAsync(server);
        });
        var connection = await ConnectAsync(client, matchesSchannelBuild);

        using (Diagnostics.Phase("dispose the connection"))
        {
            await connection.DisposeAsync();
        }

        var sent = await serverTask;

        Diagnostics.Bytes("bytes the server read after the handshake", sent);
        Diagnostics.Assert("first record type", AlertRecordType, sent.FirstOrDefault());

        Assert.IsNotEmpty(sent, "The client closed without close_notify.");
        Assert.AreEqual(AlertRecordType, sent[0]);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterTlsWasCleared_SendsNoSecondCloseNotify()
    {
        Diagnostics.Arrange("TLS cleared first", true);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var sslStream = await HandshakeAsync(server);
            await sslStream.ShutdownAsync();
            return await ReadToEndAsync(server);
        });
        var connection = await ConnectAsync(client, matchesSchannelBuild: false);
        var plaintext = await connection.ClearTlsAsync(sendCloseNotifyFirst: false, CancellationToken.None);
        Assert.IsNotNull(plaintext);

        using (Diagnostics.Phase("dispose the connection"))
        {
            await connection.DisposeAsync();
        }

        var sent = await serverTask;

        Diagnostics.Bytes("bytes the server read after its close_notify", sent);
        Diagnostics.Assert("bytes the server read after its close_notify", 0, sent.Length);

        Assert.IsEmpty(sent, "A cleared connection sent a TLS record when it closed.");
    }

    [TestMethod]
    public async Task DisposeAsync_AfterTheServerEndedTheTransport_DoesNotThrow()
    {
        Diagnostics.Arrange("server ends the transport", true);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            _ = await HandshakeAsync(server);
            await server.DisposeAsync();
        });
        var connection = await ConnectAsync(client, matchesSchannelBuild: false);
        await serverTask;
        await Assert.ThrowsExactlyAsync<MissingCloseNotifyException>(
            async () => await connection.ReadAsync(new byte[16], CancellationToken.None));

        using (Diagnostics.Phase("dispose the connection"))
        {
            await connection.DisposeAsync();
        }

        Diagnostics.Act("transport ended under the client", true);
        Diagnostics.Assert("dispose completed", true, true);
    }

    [TestMethod]
    public async Task DisposeAsync_WhenCloseNotifyCannotBeSent_DoesNotThrow()
    {
        Diagnostics.Arrange("client stream disposed first", true);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () => _ = await HandshakeAsync(server));
        var connection = await ConnectAsync(client, matchesSchannelBuild: false);
        await serverTask;
        await client.DisposeAsync();

        using (Diagnostics.Phase("dispose the connection"))
        {
            await connection.DisposeAsync();
        }

        Diagnostics.Act("client stream disposed", client.IsDisposed);
        Diagnostics.Assert("client stream disposed", true, client.IsDisposed);

        Assert.IsTrue(client.IsDisposed);
    }

    private static async Task<byte[]> ReadToEndAsync(InMemoryDuplexStream server)
    {
        var received = new List<byte>();
        var buffer = new byte[256];
        int read;
        while ((read = await server.ReadAsync(buffer)) > 0)
        {
            received.AddRange(buffer[..read]);
        }

        return [.. received];
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
}
