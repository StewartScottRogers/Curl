using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-228 (BL-3757): in the Schannel build a refused server certificate ends the handshake
/// before the client's final flight reaches the server, so the server sees the transport end
/// instead of a completed handshake, as curl's Schannel build leaves it.
/// </summary>
[TestClass]
public sealed class Cgr228Tests
{
    private static X509Certificate2 s_serverCertificate = null!;

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

    // Held back is a TLS 1.3 client's final flight, which follows the certificate's judgement.
    // macOS's SslStream negotiates TLS 1.2 at most, whose client sends its final flight before
    // the server's Finished and judges the certificate after it, so the server completes there.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsClientAsync_ServerCertificateRefused_ServerHandshakeNeverCompletes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), matchesSchannelBuild: true);
        var serverTask = Task.Run(async () =>
        {
            using var serverStream = new SslStream(server, leaveInnerStreamOpen: true);
            await serverStream.AuthenticateAsServerAsync(s_serverCertificate);
        });

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, new IPEndPoint(IPAddress.Loopback, 443)), "localhost", CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode, result.ErrorMessage);
        await Assert.ThrowsAsync<IOException>(async () => await serverTask.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [TestMethod]
    public async Task WriteAsync_AfterTheFirstReadWhileHolding_SendsNothingUntilReleased()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null)) { HoldsWritesAfterFirstRead = true };
        await stream.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        var received = new byte[8];
        Assert.AreEqual(1, await server.ReadAsync(received, CancellationToken.None));
        await server.WriteAsync(new byte[] { 9 }, CancellationToken.None);
        Assert.AreEqual(1, await stream.ReadAsync(received, CancellationToken.None));

        await stream.WriteAsync(new byte[] { 2, 3 }, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 4 }, 0, 1, CancellationToken.None);
        await stream.ReleaseHeldWritesAsync(CancellationToken.None);
        await stream.ReleaseHeldWritesAsync(CancellationToken.None);

        Assert.AreEqual(3, await server.ReadAsync(received, CancellationToken.None));
        CollectionAssert.AreEqual(new byte[] { 2, 3, 4 }, received[..3]);
    }

    [TestMethod]
    public async Task ReadAsync_WithWritesHeld_SendsThemFirst()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null)) { HoldsWritesAfterFirstRead = true };
        var buffer = new byte[8];
        await server.WriteAsync(new byte[] { 9, 9 }, CancellationToken.None);
        Assert.IsGreaterThan(0, await stream.ReadAsync(buffer, CancellationToken.None));
        await stream.WriteAsync(new byte[] { 5 }, CancellationToken.None);
        await server.WriteAsync(new byte[] { 9 }, CancellationToken.None);

        Assert.IsGreaterThan(0, await stream.ReadAsync(buffer, CancellationToken.None));

        Assert.AreEqual(1, await server.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(5, buffer[0]);
    }

    [TestMethod]
    public async Task WriteAsync_AfterReleaseWithNothingHeld_WritesThrough()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ConnectionStream(new StreamConnection(client, null)) { HoldsWritesAfterFirstRead = true };
        var buffer = new byte[8];
        await server.WriteAsync(new byte[] { 9 }, CancellationToken.None);
        Assert.IsGreaterThan(0, await stream.ReadAsync(buffer, CancellationToken.None));
        await stream.ReleaseHeldWritesAsync(CancellationToken.None);

        await stream.WriteAsync(new byte[] { 7 }, CancellationToken.None);

        Assert.AreEqual(1, await server.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(7, buffer[0]);
    }
}
