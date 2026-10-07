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
        var testCase = new EarlyDataCase();
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("application protocol", "http/1.1", run.Result.ApplicationProtocol);
        Assert.AreEqual("http/1.1", run.Result.ApplicationProtocol);
        Diagnostics.Assert("server resumed", true, run.Server.IsResumed);
        Assert.IsTrue(run.Server.IsResumed);
        Diagnostics.Assert("early data accepted", true, run.Server.EarlyDataAccepted);
        Assert.IsTrue(run.Server.EarlyDataAccepted);
        Diagnostics.Assert("early data", EarlyRequest, Encoding.ASCII.GetString([.. run.Records.EarlyData]));
        Assert.AreEqual(EarlyRequest, Encoding.ASCII.GetString([.. run.Records.EarlyData]));
        var earlyDataLines = run.Events.Info.Where(line => line.Contains("early data", StringComparison.Ordinal)).ToArray();
        Diagnostics.Assert("early data info lines", 3, earlyDataLines.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "SSL session allows 16384 bytes of early data, reusing ALPN 'http/1.1'",
                $"SSL sending {EarlyRequest.Length} bytes of early data",
                $"Server accepted {EarlyRequest.Length} bytes of TLS early data.",
            },
            run.Events.Info.Where(line => line.Contains("early data", StringComparison.Ordinal)).ToArray());
        Diagnostics.Assert("handshake events", 1, run.Events.Handshakes.Count);
        Assert.AreEqual(1, run.Events.Handshakes.Count);
        Diagnostics.Assert("early data sent", EarlyRequest.Length, run.Events.EarlyDataSent.Single());
        CollectionAssert.AreEqual(new long[] { EarlyRequest.Length }, run.Events.EarlyDataSent, "%{tls_earlydata}");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataToAnHttpsProxy_ReportsNoEarlyDataCount()
    {
        var testCase = new EarlyDataCase { IsProxy = true };
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("early data accepted", true, run.Server.EarlyDataAccepted);
        Assert.IsTrue(run.Server.EarlyDataAccepted);
        Diagnostics.Assert("early data sent count", 0, run.Events.EarlyDataSent.Count);
        Assert.IsEmpty(run.Events.EarlyDataSent);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataTheServerRejects_SendsTheRequestAfterTheHandshake()
    {
        var testCase = new EarlyDataCase { AcceptEarlyData = false };
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("server resumed", true, run.Server.IsResumed);
        Assert.IsTrue(run.Server.IsResumed);
        Diagnostics.Assert("early data accepted", false, run.Server.EarlyDataAccepted);
        Assert.IsFalse(run.Server.EarlyDataAccepted);
        Diagnostics.Assert("skipped records", 1, run.Records.SkippedRecords);
        Assert.AreEqual(1, run.Records.SkippedRecords, "the rejected early data record");
        Diagnostics.Assert("received after handshake", EarlyRequest, run.ReceivedAfterHandshake);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
        Diagnostics.Assert("last info line", "Server rejected TLS early data.", run.Events.Info[^1]);
        Assert.AreEqual("Server rejected TLS early data.", run.Events.Info[^1]);
        Diagnostics.Assert("early data sent", -EarlyRequest.Length, run.Events.EarlyDataSent.Single());
        CollectionAssert.AreEqual(new long[] { -EarlyRequest.Length }, run.Events.EarlyDataSent, "negative when rejected");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataLongerThanTheSessionAllows_SendsTheRestAfterTheHandshake()
    {
        var testCase = new EarlyDataCase { MaxEarlyDataSize = 4 };
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("early data", "GET ", Encoding.ASCII.GetString([.. run.Records.EarlyData]));
        Assert.AreEqual("GET ", Encoding.ASCII.GetString([.. run.Records.EarlyData]));
        Diagnostics.Assert("received after handshake", EarlyRequest[4..], run.ReceivedAfterHandshake);
        Assert.AreEqual(EarlyRequest[4..], run.ReceivedAfterHandshake);
        Diagnostics.Assert("sending info line present", true, run.Events.Info.Contains("SSL sending 4 bytes of early data"));
        CollectionAssert.Contains(run.Events.Info, "SSL sending 4 bytes of early data");
        Diagnostics.Assert("accepted info line present", true, run.Events.Info.Contains("Server accepted 4 bytes of TLS early data."));
        CollectionAssert.Contains(run.Events.Info, "Server accepted 4 bytes of TLS early data.");
        Diagnostics.Assert("early data sent", 4L, run.Events.EarlyDataSent.Single());
        CollectionAssert.AreEqual(new long[] { 4 }, run.Events.EarlyDataSent);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndNoSession_SendsTheRequestAfterAFullHandshake()
    {
        var testCase = new EarlyDataCase { SeedSession = false };
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("server resumed", false, run.Server.IsResumed);
        Assert.IsFalse(run.Server.IsResumed);
        Diagnostics.Assert("early data offered", false, run.Server.EarlyDataOffered);
        Assert.IsFalse(run.Server.EarlyDataOffered);
        Diagnostics.Assert("received after handshake", EarlyRequest, run.ReceivedAfterHandshake);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
        Diagnostics.Assert("any early data info line", false, run.Events.Info.Any(line => line.Contains("early data", StringComparison.Ordinal)));
        Assert.IsFalse(run.Events.Info.Any(line => line.Contains("early data", StringComparison.Ordinal)));
        Diagnostics.Assert("early data sent count", 0, run.Events.EarlyDataSent.Count);
        Assert.IsEmpty(run.Events.EarlyDataSent);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndASessionOfAnotherAlpn_OffersNoEarlyData()
    {
        var testCase = new EarlyDataCase { SessionApplicationProtocol = "h3" };
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("server resumed", true, run.Server.IsResumed);
        Assert.IsTrue(run.Server.IsResumed);
        Diagnostics.Assert("early data offered", false, run.Server.EarlyDataOffered);
        Assert.IsFalse(run.Server.EarlyDataOffered);
        Diagnostics.Assert("received after handshake", EarlyRequest, run.ReceivedAfterHandshake);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutEarlyDataAndAResumableSession_ResumesWithoutEarlyData()
    {
        var testCase = new EarlyDataCase { AllowEarlyData = false };
        ArrangeEarlyDataCase(testCase);

        EarlyDataRun run;
        using (Diagnostics.Phase("TLS handshake"))
        {
            run = await RunEarlyDataAsync(testCase);
        }

        ActEarlyDataRun(run);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Diagnostics.Assert("server resumed", true, run.Server.IsResumed);
        Assert.IsTrue(run.Server.IsResumed);
        Diagnostics.Assert("early data offered", false, run.Server.EarlyDataOffered);
        Assert.IsFalse(run.Server.EarlyDataOffered);
        Diagnostics.Assert("received after handshake", EarlyRequest, run.ReceivedAfterHandshake);
        Assert.AreEqual(EarlyRequest, run.ReceivedAfterHandshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEarlyDataAndAFailingDeferredHandshake_ThrowsTheConnectFailure()
    {
        var sessions = SessionsHolding(EarlySession(RandomNumberGenerator.GetBytes(32), [1, 2, 3], "http/1.1", 16384));
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, AllowEarlyData: true");
        Diagnostics.Arrange("session", "seeded: ticket 01 02 03, ALPN http/1.1, max early data 16384");
        Diagnostics.Arrange("server", "closed before it answers");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await EarlyDataProvider(sessions, allowEarlyData: true).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, Http2AndHttp11, CancellationToken.None);
            await server.DisposeAsync();
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, "the connect is deferred to the first write");
        var thrown = await Assert.ThrowsExactlyAsync<DeferredTlsHandshakeFailedException>(
            async () => await result.Connection!.WriteAsync(Encoding.ASCII.GetBytes(EarlyRequest), CancellationToken.None));
        Diagnostics.Act("deferred failure exit code", thrown.ExitCode);
        Diagnostics.Assert("deferred failure exit code is not Ok", true, thrown.ExitCode != CurlExitCode.Ok);
        Assert.AreNotEqual(CurlExitCode.Ok, thrown.ExitCode);
        Diagnostics.Assert("plaintext disposed", true, client.IsDisposed);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public void EarlyDataAccepted_OfAStreamThatIsNotTls13_IsFalse()
    {
        Diagnostics.Arrange("stream", "an empty MemoryStream, not a TLS 1.3 stream");

        var accepted = HandBuiltTlsProvider.EarlyDataAccepted(new MemoryStream());

        Diagnostics.Act("early data accepted", accepted);
        Diagnostics.Assert("early data accepted", false, accepted);
        Assert.IsFalse(accepted);
    }

    private void ArrangeEarlyDataCase(EarlyDataCase testCase)
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("seed session", testCase.SeedSession);
        Diagnostics.Arrange("allow early data", testCase.AllowEarlyData);
        Diagnostics.Arrange("server accepts early data", testCase.AcceptEarlyData);
        Diagnostics.Arrange("max early data size", testCase.MaxEarlyDataSize);
        Diagnostics.Arrange("session ALPN", testCase.SessionApplicationProtocol ?? "(none)");
        Diagnostics.Arrange("is proxy", testCase.IsProxy);
        Diagnostics.Arrange("request", EarlyRequest);
    }

    private void ActEarlyDataRun(EarlyDataRun run)
    {
        ActConnectResult(run.Result);
        Diagnostics.Act("server resumed", run.Server.IsResumed);
        Diagnostics.Act("early data offered", run.Server.EarlyDataOffered);
        Diagnostics.Act("early data accepted", run.Server.EarlyDataAccepted);
        Diagnostics.Act("early data sent", string.Join(",", run.Events.EarlyDataSent));
        Diagnostics.Bytes("early data received", [.. run.Records.EarlyData]);
        Diagnostics.Bytes("received after handshake", Encoding.ASCII.GetBytes(run.ReceivedAfterHandshake));
    }

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

        public bool IsProxy { get; init; }
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
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: testCase.IsProxy, Http2AndHttp11, CancellationToken.None);
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
