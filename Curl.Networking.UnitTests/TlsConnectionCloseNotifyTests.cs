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
/// Pins how a secure connection from either TLS provider ends (ADR-0213): a read returns 0
/// once the server's <c>close_notify</c> arrives, and fails with
/// <see cref="MissingCloseNotifyException" /> and the matched build's text when the transport
/// ends without one, at a record boundary or inside a record. The server is a server-side
/// <see cref="SslStream" /> over an <see cref="InMemoryDuplexStream" /> pair.
/// </summary>
[TestClass]
public sealed class TlsConnectionCloseNotifyTests
{
    private const string CertificateHost = "localhost";

    private const string SchannelText = "schannel: server closed abruptly (missing close_notify)";

    private const string OpenSslText =
        "OpenSSL SSL_read: OpenSSL/3.5.7: error:0A000126:SSL routines::unexpected eof while reading, errno 0";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 443);

    private static X509Certificate2 s_serverCertificate = null!;

    /// <summary>How the test server ends the connection after its application data.</summary>
    public enum ServerEnding
    {
        /// <summary>Sends <c>close_notify</c>, then closes.</summary>
        CloseNotify,

        /// <summary>Closes the transport at a record boundary.</summary>
        BareEnd,

        /// <summary>Sends the first bytes of a record, then closes the transport.</summary>
        EndInsideARecord,
    }

    /// <summary>The TLS provider under test.</summary>
    public enum ProviderKind
    {
        /// <summary><see cref="SslStreamTlsProvider" />.</summary>
        SslStream,

        /// <summary><see cref="HandBuiltTlsProvider" /> over TLS 1.2.</summary>
        HandBuiltTls12,

        /// <summary><see cref="HandBuiltTlsProvider" /> over TLS 1.3.</summary>
        HandBuiltTls13,
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
    [DataRow(ProviderKind.SslStream)]
    [DataRow(ProviderKind.HandBuiltTls12)]
    public Task ReadAsync_AfterCloseNotify_ReturnsZero(ProviderKind kind) =>
        AssertEndsQuietlyAsync(kind);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public Task ReadAsync_AfterCloseNotifyOverTls13_ReturnsZero() =>
        AssertEndsQuietlyAsync(ProviderKind.HandBuiltTls13);

    [TestMethod]
    [DataRow(ProviderKind.SslStream, ServerEnding.BareEnd, true, SchannelText)]
    [DataRow(ProviderKind.SslStream, ServerEnding.BareEnd, false, OpenSslText)]
    [DataRow(ProviderKind.SslStream, ServerEnding.EndInsideARecord, true, SchannelText)]
    [DataRow(ProviderKind.SslStream, ServerEnding.EndInsideARecord, false, OpenSslText)]
    [DataRow(ProviderKind.HandBuiltTls12, ServerEnding.BareEnd, true, SchannelText)]
    [DataRow(ProviderKind.HandBuiltTls12, ServerEnding.BareEnd, false, OpenSslText)]
    [DataRow(ProviderKind.HandBuiltTls12, ServerEnding.EndInsideARecord, false, OpenSslText)]
    public Task ReadAsync_WhenTheTransportEndsWithoutCloseNotify_ThrowsMissingCloseNotifyWithTheBuildsText(
        ProviderKind kind, ServerEnding ending, bool matchesSchannelBuild, string expected) =>
        AssertMissingCloseNotifyAsync(kind, ending, matchesSchannelBuild, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(ServerEnding.BareEnd)]
    [DataRow(ServerEnding.EndInsideARecord)]
    public Task ReadAsync_WhenTheTransportEndsWithoutCloseNotifyOverTls13_ThrowsMissingCloseNotify(ServerEnding ending) =>
        AssertMissingCloseNotifyAsync(ProviderKind.HandBuiltTls13, ending, false, OpenSslText);

    [TestMethod]
    public async Task ReadAsync_WhenTheTransportIsResetBeforeItEnds_ThrowsTheResetNotMissingCloseNotify()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunServerAsync(server, SslProtocols.Tls12, ending: null);
        var plaintext = new ResetOnDemandConnection(new StreamConnection(client, ServerEndPoint));

        var result = await Provider(ProviderKind.SslStream, true).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        await using var connection = result.Connection!;
        await ReadHelloAsync(connection);
        plaintext.Reset();

        var exception = await Assert.ThrowsAsync<IOException>(() => connection.ReadAsync(new byte[16], CancellationToken.None).AsTask());

        Assert.IsNotInstanceOfType<MissingCloseNotifyException>(exception);
        await client.DisposeAsync(); // Ends the server's waiting read.
        await IgnoreFailureAsync(serverTask);
    }

    private static async Task AssertEndsQuietlyAsync(ProviderKind kind)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunServerAsync(server, ProtocolsFor(kind), ServerEnding.CloseNotify);

        var result = await Provider(kind, true).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        await using var connection = result.Connection!;
        await ReadHelloAsync(connection);

        Assert.AreEqual(0, await connection.ReadAsync(new byte[16], CancellationToken.None));
        await IgnoreFailureAsync(serverTask);
    }

    private static async Task AssertMissingCloseNotifyAsync(ProviderKind kind, ServerEnding ending, bool matchesSchannelBuild, string expected)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunServerAsync(server, ProtocolsFor(kind), ending);

        var result = await Provider(kind, matchesSchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        await using var connection = result.Connection!;
        await ReadHelloAsync(connection);

        var exception = await Assert.ThrowsExactlyAsync<MissingCloseNotifyException>(
            () => connection.ReadAsync(new byte[16], CancellationToken.None).AsTask());

        Assert.AreEqual(expected, exception.Message);
        await IgnoreFailureAsync(serverTask);
    }

    // An empty read first (it returns 0 without failing), then the server's five bytes.
    private static async Task ReadHelloAsync(IConnection connection)
    {
        Assert.AreEqual(0, await connection.ReadAsync(Memory<byte>.Empty, CancellationToken.None));
        var buffer = new byte[5];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(total), CancellationToken.None);
            Assert.AreNotEqual(0, read, "The server closed before sending hello.");
            total += read;
        }

        Assert.AreEqual("hello", Encoding.ASCII.GetString(buffer));
    }

    private static ITlsProvider Provider(ProviderKind kind, bool matchesSchannelBuild)
    {
        var options = new TlsClientOptions(Insecure: true);
        return kind switch
        {
            ProviderKind.SslStream => new SslStreamTlsProvider(options, matchesSchannelBuild),
            ProviderKind.HandBuiltTls12 => HandBuilt(options with { MaximumVersion = TlsVersion.Tls12 }, matchesSchannelBuild),
            _ => HandBuilt(options with { MinimumVersion = TlsVersion.Tls13 }, matchesSchannelBuild),
        };
    }

    private static HandBuiltTlsProvider HandBuilt(TlsClientOptions options, bool matchesSchannelBuild) =>
        new(options, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);

    private static SslProtocols ProtocolsFor(ProviderKind kind) =>
        kind == ProviderKind.HandBuiltTls13 ? SslProtocols.Tls13 : SslProtocols.Tls12;

    // Handshakes, sends "hello", then ends as asked; with no ending, waits for the client to go.
    private static Task RunServerAsync(InMemoryDuplexStream server, SslProtocols protocols, ServerEnding? ending) => Task.Run(async () =>
    {
        var sslStream = new SslStream(server, leaveInnerStreamOpen: true);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = protocols,
        });
        await sslStream.WriteAsync("hello"u8.ToArray());
        await sslStream.FlushAsync();
        switch (ending)
        {
            case ServerEnding.CloseNotify:
                await sslStream.ShutdownAsync();
                break;
            case ServerEnding.EndInsideARecord:
                await server.WriteAsync(new byte[] { 0x17, 0x03, 0x03, 0x00, 0x40, 0x01, 0x02 });
                break;
            case null:
                _ = await sslStream.ReadAsync(new byte[1]);
                break;
        }

        await server.DisposeAsync();
    });

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
