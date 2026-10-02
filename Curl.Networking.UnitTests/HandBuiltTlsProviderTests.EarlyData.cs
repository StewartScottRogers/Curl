using System.Security.Cryptography;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>The provider's <c>--tls-earlydata</c>: the request as 0-RTT early data on a resumed <c>--ssl-sessions</c> session (BL-1105).</summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private const string EarlyRequest = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";

    private static readonly string[] Http2AndHttp11 = ["h2", "http/1.1"];

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndAResumableSession_SendsTheRequestAsEarlyData()
    {
        var run = await RunEarlyDataAsync(new EarlyDataCase());

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.AreEqual("http/1.1", run.Result.ApplicationProtocol);
        Assert.IsTrue(run.Server.IsResumed);
        Assert.IsTrue(run.Server.EarlyDataAccepted);
        Assert.AreEqual(EarlyRequest, Encoding.ASCII.GetString([.. run.Records.EarlyData]));
        CollectionAssert.AreEqual(
            new[]
            {
                "SSL session allows 16384 bytes of early data, reusing ALPN 'http/1.1'",
                $"SSL sending {EarlyRequest.Length} bytes of early data",
                $"Server accepted {EarlyRequest.Length} bytes of TLS early data.",
            },
            run.Events.Info.Where(line => line.Contains("early data", StringComparison.Ordinal)).ToArray());
        Assert.AreEqual(1, run.Events.Handshakes.Count);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataTheServerRejects_SendsTheRequestAfterTheHandshake()
    {
        var run = await RunEarlyDataAsync(new EarlyDataCase { AcceptEarlyData = false });

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.IsTrue(run.Server.IsResumed);
        Assert.IsFalse(run.Server.EarlyDataAccepted);
        Assert.AreEqual(1, run.Records.SkippedRecords, "the rejected early data record");
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
        Assert.AreEqual("Server rejected TLS early data.", run.Events.Info[^1]);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataLongerThanTheSessionAllows_SendsTheRestAfterTheHandshake()
    {
        var run = await RunEarlyDataAsync(new EarlyDataCase { MaxEarlyDataSize = 4 });

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.AreEqual("GET ", Encoding.ASCII.GetString([.. run.Records.EarlyData]));
        Assert.AreEqual(EarlyRequest[4..], run.ReceivedAfterHandshake);
        CollectionAssert.Contains(run.Events.Info, "SSL sending 4 bytes of early data");
        CollectionAssert.Contains(run.Events.Info, "Server accepted 4 bytes of TLS early data.");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndNoSession_SendsTheRequestAfterAFullHandshake()
    {
        var run = await RunEarlyDataAsync(new EarlyDataCase { SeedSession = false });

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.IsFalse(run.Server.IsResumed);
        Assert.IsFalse(run.Server.EarlyDataOffered);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
        Assert.IsFalse(run.Events.Info.Any(line => line.Contains("early data", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndASessionOfAnotherAlpn_OffersNoEarlyData()
    {
        var run = await RunEarlyDataAsync(new EarlyDataCase { SessionApplicationProtocol = "h3" });

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.IsTrue(run.Server.IsResumed);
        Assert.IsFalse(run.Server.EarlyDataOffered);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutEarlyDataAndAResumableSession_ResumesWithoutEarlyData()
    {
        var run = await RunEarlyDataAsync(new EarlyDataCase { AllowEarlyData = false });

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.IsTrue(run.Server.IsResumed);
        Assert.IsFalse(run.Server.EarlyDataOffered);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndAFailingDeferredHandshake_ThrowsTheConnectFailure()
    {
        var sessions = SessionsHolding(EarlySession(RandomNumberGenerator.GetBytes(32), [1, 2, 3], "http/1.1", 16384));
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var events = new RecordingTransferEvents();

        var result = await EarlyDataProvider(sessions, allowEarlyData: true).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, Http2AndHttp11, CancellationToken.None);
        await server.DisposeAsync();

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, "the connect is deferred to the first write");
        var thrown = await Assert.ThrowsExactlyAsync<DeferredTlsHandshakeFailedException>(
            async () => await result.Connection!.WriteAsync(Encoding.ASCII.GetBytes(EarlyRequest), CancellationToken.None));
        Assert.AreNotEqual(CurlExitCode.Ok, thrown.ExitCode);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public void EarlyDataAccepted_OfAStreamThatIsNotTls13_IsFalse() =>
        Assert.IsFalse(HandBuiltTlsProvider.EarlyDataAccepted(new MemoryStream()));

    // A resumed connection's run: the server's state, what it read, and the client's lines.
    private sealed record EarlyDataRun(
        ConnectResult Result,
        Tls13TestServer Server,
        Tls13RecordTestServer Records,
        string ReceivedAfterHandshake,
        RecordingTransferEvents Events);

    private sealed record EarlyDataCase
    {
        public bool SeedSession { get; init; } = true;

        public bool AllowEarlyData { get; init; } = true;

        public bool AcceptEarlyData { get; init; } = true;

        public uint MaxEarlyDataSize { get; init; } = 16384;

        public string? SessionApplicationProtocol { get; init; } = "http/1.1";
    }

    // Connects with the session the case seeds, writes the request, and reads what reached the
    // server: early data, and whatever came after the handshake.
    private static async Task<EarlyDataRun> RunEarlyDataAsync(EarlyDataCase testCase)
    {
        using var pki = new OcspTestPki();
        byte[] ticket = [7, 7, 7, 7];
        byte[] preSharedKey = RandomNumberGenerator.GetBytes(32);
        var sessions = testCase.SeedSession
            ? SessionsHolding(EarlySession(preSharedKey, ticket, testCase.SessionApplicationProtocol, testCase.MaxEarlyDataSize))
            : new TlsSessionCache(TimeProvider.System);
        var server = new Tls13TestServer(pki.LeafCredential)
        {
            ApplicationProtocol = "http/1.1",
            Tickets = new() { [Convert.ToHexString(ticket)] = preSharedKey },
            AcceptEarlyData = testCase.AcceptEarlyData,
        };
        var (client, serverStream) = InMemoryDuplexStream.CreatePair();
        var records = new Tls13RecordTestServer(serverStream, server);
        int afterHandshake = testCase.AcceptEarlyData && testCase.SeedSession && testCase.AllowEarlyData && testCase.MaxEarlyDataSize > 0 && testCase.SessionApplicationProtocol == "http/1.1"
            ? EarlyRequest.Length - (int)Math.Min(testCase.MaxEarlyDataSize, (uint)EarlyRequest.Length)
            : EarlyRequest.Length;
        var serverTask = Task.Run(async () =>
        {
            await records.HandshakeAsync();
            return afterHandshake == 0 ? [] : await records.ReceiveApplicationDataAsync(afterHandshake);
        });
        var events = new RecordingTransferEvents();

        var result = await EarlyDataProvider(sessions, testCase.AllowEarlyData).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, Http2AndHttp11, CancellationToken.None);
        await result.Connection!.WriteAsync(Encoding.ASCII.GetBytes(EarlyRequest), CancellationToken.None);
        byte[] received = await serverTask;
        await result.Connection.DisposeAsync();
        return new EarlyDataRun(result, server, records, Encoding.ASCII.GetString(received), events);
    }

    private static HandBuiltTlsProvider EarlyDataProvider(TlsSessionCache sessions, bool allowEarlyData) => new(
        new TlsClientOptions(Insecure: true, AllowEarlyData: allowEarlyData),
        OpenSslBuild,
        TimeProvider.System,
        new FakeClientCertificateStore(),
        SystemTlsRandomSource.Instance,
        sessions);

    private static TlsSessionRecord EarlySession(byte[] preSharedKey, byte[] ticket, string? applicationProtocol, uint maxEarlyDataSize) =>
        new(0x0304, Tls13CipherSuite.Aes128GcmSha256.Code, [], preSharedKey, ticket, 7200, 0, maxEarlyDataSize, DateTimeOffset.UtcNow)
        {
            ServerName = CertificateHost,
            ApplicationProtocol = applicationProtocol,
            Group = TlsNamedGroup.X25519,
        };

    // A cache holding the session for the test server's peer, as --ssl-sessions loads one.
    private static TlsSessionCache SessionsHolding(TlsSessionRecord session)
    {
        var sessions = new TlsSessionCache(TimeProvider.System);
        var options = new TlsClientOptions(Insecure: true, AllowEarlyData: true);
        sessions.Track(TlsSessionCache.PeerKey(CertificateHost, ServerEndPoint.Port, options), () => [session]);
        Assert.AreNotEqual(string.Empty, sessions.Export());
        return sessions;
    }
}
