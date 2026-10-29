using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="HandBuiltTlsConnection.DisposeAsync" />'s <c>close_notify</c> (BL-3987,
/// ADR-0959), as <see cref="SslStreamConnectionCloseNotifyTests" /> pins
/// <see cref="SslStreamConnection" />'s: closing an https connection sends the alert first, as
/// curl's OpenSSL and Schannel builds both do, but not after TLS was cleared, and a failure to
/// send it does not escape. The server is a server-side <see cref="SslStream" /> over an
/// <see cref="InMemoryDuplexStream" /> pair, which reads what the client sends after the
/// handshake from the raw stream; TLS 1.3 runs where the platform's server has it.
/// </summary>
[TestClass]
public sealed class HandBuiltTlsConnectionCloseNotifyTests
{
    private const string CertificateHost = "localhost";

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
    public Task DisposeAsync_OverTls12AfterATransfer_SendsCloseNotify(bool matchesSchannelBuild) =>
        AssertDisposeSendsCloseNotifyAsync(SslProtocols.Tls12, matchesSchannelBuild);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(false)]
    [DataRow(true)]
    public Task DisposeAsync_OverTls13AfterATransfer_SendsCloseNotify(bool matchesSchannelBuild) =>
        AssertDisposeSendsCloseNotifyAsync(SslProtocols.Tls13, matchesSchannelBuild);

    [TestMethod]
    public async Task DisposeAsync_AfterTlsWasCleared_SendsNoCloseNotify()
    {
        Diagnostics.Arrange("TLS cleared first", true);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var sslStream = await HandshakeAsync(server, SslProtocols.Tls12);
            await sslStream.ShutdownAsync();
            return await ReadToEndAsync(server);
        });
        var connection = await ConnectAsync(client, SslProtocols.Tls12, matchesSchannelBuild: false);
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
    public async Task DisposeAsync_WhenTheTransportIsGone_DoesNotThrow()
    {
        Diagnostics.Arrange("client stream disposed first", true);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () => _ = await HandshakeAsync(server, SslProtocols.Tls12));
        var connection = await ConnectAsync(client, SslProtocols.Tls12, matchesSchannelBuild: false);
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

    [TestMethod]
    public async Task DisposeAsync_AfterTheServerSentACorruptRecord_DoesNotThrow()
    {
        Diagnostics.Arrange("server after the handshake", "a record that does not decrypt");

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            _ = await HandshakeAsync(server, SslProtocols.Tls12);
            await server.WriteAsync((byte[])[0x17, 0x03, 0x03, 0x00, 0x40, .. new byte[0x40]]);
        });
        var connection = await ConnectAsync(client, SslProtocols.Tls12, matchesSchannelBuild: false);
        await serverTask;
        await Assert.ThrowsAsync<IOException>(
            async () => await connection.ReadAsync(new byte[16], CancellationToken.None));

        using (Diagnostics.Phase("dispose the connection"))
        {
            await connection.DisposeAsync();
        }

        Diagnostics.Act("client stream disposed", client.IsDisposed);
        Diagnostics.Assert("client stream disposed", true, client.IsDisposed);

        Assert.IsTrue(client.IsDisposed);
    }

    private async Task AssertDisposeSendsCloseNotifyAsync(SslProtocols protocol, bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("protocol", protocol);
        Diagnostics.Arrange("matches Schannel build", matchesSchannelBuild);

        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            _ = await HandshakeAsync(server, protocol);
            return await ReadToEndAsync(server);
        });
        var connection = await ConnectAsync(client, protocol, matchesSchannelBuild);

        using (Diagnostics.Phase("dispose the connection"))
        {
            await connection.DisposeAsync();
        }

        var sent = await serverTask;

        Diagnostics.Bytes("bytes the server read after the handshake", sent);
        Diagnostics.Assert("first record type", $"0x{CloseNotifyRecordType(protocol):x2}", $"0x{sent.FirstOrDefault():x2}");

        Assert.IsNotEmpty(sent, "The client closed without close_notify.");
        Assert.AreEqual(CloseNotifyRecordType(protocol), sent[0]);
    }

    // TLS 1.3 sends every record after the handshake as application_data (RFC 8446 section 5.2).
    private static byte CloseNotifyRecordType(SslProtocols protocol) => protocol == SslProtocols.Tls13 ? (byte)0x17 : (byte)0x15;

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
}
