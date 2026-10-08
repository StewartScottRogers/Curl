using System.Text;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Resumes TLS 1.3 sessions over a pipe against <see cref="Tls13RecordTestServer" />: the
/// session the first connection records from its NewSessionTicket is exported with
/// <see cref="TlsSessionCodec" />, read back and offered by the second connection, which
/// sends its request as 0-RTT early data; early data the server rejects is sent again
/// after the handshake, and an expired ticket gets a full handshake.
/// </summary>
[TestClass]
public sealed class Tls13ResumptionConnectionTests
{
    private static readonly byte[] Request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");

    private static readonly Tls13ClientSettings Settings = Tls13PipeDriver.DefaultSettings with
    {
        ExtensionOrder = [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.EarlyData, TlsExtensionType.PskKeyExchangeModes],
    };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExportedSessionResumesAndCarriesTheRequestAsEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        Diagnostics.Arrange("session", "exported with TlsSessionCodec and read back");
        TlsSessionRecord session = TlsSessionCodec.Decode(TlsSessionCodec.Encode(await TimedFirstSessionAsync(tickets)))!;
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), Request);

        Tls13ClientHandshake handshake = result.Stream!.Handshake;
        WriteResumption(handshake, server);
        Diagnostics.Diff("early data", Request, server.EarlyData.ToArray());
        Assert.IsTrue(handshake.IsResumed);
        Assert.IsTrue(handshake.EarlyDataOffered);
        Assert.IsTrue(handshake.EarlyDataAccepted);
        Assert.IsTrue(testServer.IsResumed);
        CollectionAssert.AreEqual(Request, server.EarlyData.ToArray());
        Assert.AreEqual(0, server.SkippedRecords);
        await AssertApplicationDataFlowsAsync(result.Stream, server);
    }

    [TestMethod]
    public async Task EarlyDataTheServerRejectsIsSentAgainAfterTheHandshake()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets, AcceptEarlyData = false };
        Diagnostics.Arrange("server", "accepts resumption, rejects early data");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), Request);

        WriteResumption(result.Stream!.Handshake, server);
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length);
        Diagnostics.Diff("request after the handshake", Request, afterHandshake);
        Assert.IsTrue(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataAccepted);
        Assert.AreEqual(1, server.SkippedRecords);
        Assert.IsEmpty(server.EarlyData);
        CollectionAssert.AreEqual(Request, afterHandshake);
    }

    [TestMethod]
    public async Task ServerThatDoesNotResumeRunsAFullHandshakeAndGetsTheRequestAfterIt()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets, AcceptResumption = false };
        Diagnostics.Arrange("server", "refuses resumption");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), Request);

        WriteResumption(result.Stream!.Handshake, server);
        Diagnostics.Act("server certificates", result.Stream.Handshake.ServerCertificates.Count);
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length);
        Diagnostics.Diff("request after the handshake", Request, afterHandshake);
        Assert.IsFalse(result.Stream!.Handshake.IsResumed);
        Assert.IsTrue(result.Stream.Handshake.EarlyDataOffered);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataAccepted);
        Assert.HasCount(1, result.Stream.Handshake.ServerCertificates);
        CollectionAssert.AreEqual(Request, afterHandshake);
    }

    [TestMethod]
    public async Task ExpiredTicketIsNotOfferedAndTheHandshakeIsAFullOne()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        Tls13ClientSettings settings = Resuming(session) with { TimeProvider = new FixedTimeProvider(session.ReceivedAt.AddSeconds(session.TicketLifetime)) };
        Diagnostics.Arrange("clock", $"ticket lifetime {session.TicketLifetime} s after it was received");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, settings, Request);

        WriteResumption(result.Stream!.Handshake, server);
        Diagnostics.Act("server saw early data offered, resumed", $"{testServer.EarlyDataOffered}, {testServer.IsResumed}");
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length);
        Diagnostics.Diff("request after the handshake", Request, afterHandshake);
        Assert.IsFalse(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataOffered);
        Assert.IsFalse(testServer.EarlyDataOffered);
        Assert.IsFalse(testServer.IsResumed);
        CollectionAssert.AreEqual(Request, afterHandshake);
    }

    [TestMethod]
    public async Task EarlyDataBeyondTheTicketLimitIsSentAfterTheHandshake()
    {
        Tls13TestTicketCache tickets = new();
        Diagnostics.Arrange("ticket max_early_data_size", 4);
        Diagnostics.Arrange("request length", Request.Length);
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets, MaxEarlyDataSize = 4 });
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), Request);

        WriteResumption(result.Stream!.Handshake, server);
        Diagnostics.Act("max early data size", result.Stream.Handshake.MaxEarlyDataSize);
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length - 4);
        Diagnostics.Diff("early data", Request[..4], server.EarlyData.ToArray());
        Diagnostics.Diff("request after the handshake", Request[4..], afterHandshake);
        Assert.IsTrue(result.Stream!.Handshake.EarlyDataAccepted);
        Assert.AreEqual(4u, result.Stream.Handshake.MaxEarlyDataSize);
        CollectionAssert.AreEqual(Request[..4], server.EarlyData.ToArray());
        CollectionAssert.AreEqual(Request[4..], afterHandshake);
    }

    [TestMethod]
    public async Task OfferedEarlyDataWithNothingToSendStillEndsWithEndOfEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        Diagnostics.Arrange("early data", "empty");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), []);

        WriteResumption(result.Stream!.Handshake, server);
        Diagnostics.Assert("early data the server received", 0, server.EarlyData.Count);
        Assert.IsTrue(result.Stream!.Handshake.EarlyDataAccepted);
        Assert.IsEmpty(server.EarlyData);
        await AssertApplicationDataFlowsAsync(result.Stream, server);
    }

    [TestMethod]
    public async Task MiddleboxCompatibilitySendsChangeCipherSpecBeforeTheEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        Diagnostics.Arrange("client", "sends a legacy session ID (middlebox compatibility)");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session) with { SendLegacySessionId = true }, Request);

        WriteResumption(result.Stream!.Handshake, server);
        Diagnostics.Act("change cipher specs received", server.ChangeCipherSpecsReceived);
        Diagnostics.Diff("early data", Request, server.EarlyData.ToArray());
        Assert.IsTrue(result.Stream!.Handshake.EarlyDataAccepted);
        Assert.AreEqual(1, server.ChangeCipherSpecsReceived);
        CollectionAssert.AreEqual(Request, server.EarlyData.ToArray());
    }

    [TestMethod]
    public async Task HelloRetryRequestDropsTheEarlyDataAndResumesWithANewBinder()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets, Group = TlsNamedGroup.Secp256r1 };
        Diagnostics.Arrange("server group", "secp256r1 (forces a HelloRetryRequest)");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), Request);

        WriteResumption(result.Stream!.Handshake, server);
        Diagnostics.Act("server resumed, saw early data offered", $"{testServer.IsResumed}, {testServer.EarlyDataOffered}");
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length);
        Diagnostics.Diff("request after the handshake", Request, afterHandshake);
        Assert.IsTrue(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataAccepted);
        Assert.IsTrue(testServer.IsResumed);
        Assert.IsFalse(testServer.EarlyDataOffered);
        Assert.AreEqual(1, server.SkippedRecords);
        CollectionAssert.AreEqual(Request, afterHandshake);
    }

    [TestMethod]
    public async Task HelloRetryRequestForASuiteOfAnotherHashDropsTheTicket()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519())
        {
            Tickets = tickets,
            Group = TlsNamedGroup.Secp256r1,
            CipherSuite = Tls13CipherSuite.Aes256GcmSha384.Code,
        };
        Diagnostics.Arrange("server", "secp256r1 and TLS_AES_256_GCM_SHA384 after a SHA-256 ticket");

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(session), Request);

        WriteResumption(result.Stream!.Handshake, server);
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length);
        Diagnostics.Diff("request after the handshake", Request, afterHandshake);
        Assert.IsFalse(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(testServer.IsResumed);
        CollectionAssert.AreEqual(Request, afterHandshake);
    }

    [TestMethod]
    public async Task ResumedConnectionRecordsItsNewTicketWithTheFirstServerCertificate()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord first = await TimedFirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        Diagnostics.Arrange("first ticket length", first.Ticket.Length);
        (Tls13ConnectResult result, Tls13RecordTestServer server) = await TimedResumeAsync(testServer, Resuming(first), Request);

        using (Diagnostics.Phase("new session ticket"))
        {
            await server.SendNewSessionTicketAsync();
            await server.SendAsync(TlsContentType.ApplicationData, [0x2a]);
            await Tls13PipeDriver.ReadAsync(result.Stream!, 1);
        }

        TlsSessionRecord second = result.Stream!.Handshake.ReceivedSessions.Single();
        Diagnostics.Act("second ticket length", second.Ticket.Length);
        Diagnostics.Diff("peer certificate", first.PeerCertificate, second.PeerCertificate);
        CollectionAssert.AreEqual(first.PeerCertificate, second.PeerCertificate);
        CollectionAssert.AreNotEqual(first.Ticket, second.Ticket);
    }

    [TestMethod]
    public async Task ConnectWithEarlyDataWithoutASessionSendsTheDataAfterTheHandshake()
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, new Tls13TestServer(TestServerCredential.Ed25519()));
        Diagnostics.Arrange("client", "offers early data with no session to resume");
        Task serverHandshake = server.HandshakeAsync();

        Tls13ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await Tls13ClientConnection.ConnectWithEarlyDataAsync(
                clientEnd, Settings with { OfferEarlyData = true }, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), Request, CancellationToken.None);
            await serverHandshake;
        }

        Diagnostics.Act("early data offered", result.Stream!.Handshake.EarlyDataOffered);
        byte[] afterHandshake = await server.ReceiveApplicationDataAsync(Request.Length);
        Diagnostics.Diff("request after the handshake", Request, afterHandshake);
        Assert.IsFalse(result.Stream!.Handshake.EarlyDataOffered);
        CollectionAssert.AreEqual(Request, afterHandshake);
    }

    [TestMethod]
    public async Task ConnectWithEarlyDataChecksItsTransport()
    {
        Diagnostics.Arrange("transport", "null");

        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ClientConnection.ConnectWithEarlyDataAsync(
            null!, Settings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), Request, CancellationToken.None));
        Diagnostics.Act("parameter", exception.ParamName);

        Diagnostics.Assert("exception type", typeof(ArgumentNullException), exception.GetType());
    }

    [TestMethod]
    public async Task FailedResumptionReturnsTheFailureWithoutSendingTheRequest()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await TimedFirstSessionAsync(tickets);
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets });
        Diagnostics.Arrange("server", "closes the transport after the ClientHello");
        Task<Tls13ConnectResult> pending = Tls13ClientConnection.ConnectWithEarlyDataAsync(
            clientEnd, Resuming(session), SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), Request, CancellationToken.None);

        await server.ReceiveClientHelloAsync();
        await serverEnd.DisposeAsync();
        Tls13ConnectResult result = await pending;
        Diagnostics.Act("failure origin", result.Failure!.Origin);

        Diagnostics.Assert("failure origin", TlsHandshakeFailureOrigin.TransportClosed, result.Failure.Origin);
        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
    }

    /// <summary>Runs a full handshake with a server that keeps its tickets in <paramref name="tickets" />, and returns the session its first ticket records.</summary>
    internal static async Task<TlsSessionRecord> FirstSessionAsync(Tls13TestTicketCache tickets, Tls13TestServer? testServer = null)
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, Stream _) = await Tls13PipeDriver.ConnectAsync(
            testServer ?? new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets }, Settings);
        await server.SendNewSessionTicketAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [0x2a]);
        await Tls13PipeDriver.ReadAsync(client, 1);
        return client.Handshake.ReceivedSessions.Single();
    }

    private static Tls13ClientSettings Resuming(TlsSessionRecord session) => Settings with { ResumptionSession = session, OfferEarlyData = true };

    /// <summary>Runs <see cref="FirstSessionAsync" /> inside a <c>first session</c> phase.</summary>
    private async Task<TlsSessionRecord> TimedFirstSessionAsync(Tls13TestTicketCache tickets, Tls13TestServer? testServer = null)
    {
        using (Diagnostics.Phase("first session"))
        {
            return await FirstSessionAsync(tickets, testServer);
        }
    }

    /// <summary>Runs <see cref="ResumeAsync" /> inside a <c>resumption handshake</c> phase.</summary>
    private async Task<(Tls13ConnectResult Result, Tls13RecordTestServer Server)> TimedResumeAsync(Tls13TestServer testServer, Tls13ClientSettings settings, byte[] earlyData)
    {
        using (Diagnostics.Phase("resumption handshake"))
        {
            return await ResumeAsync(testServer, settings, earlyData);
        }
    }

    /// <summary>Writes the resumed connection's outcome as ACT lines.</summary>
    private void WriteResumption(Tls13ClientHandshake handshake, Tls13RecordTestServer server)
    {
        Diagnostics.Act("resumed, early data offered, early data accepted", $"{handshake.IsResumed}, {handshake.EarlyDataOffered}, {handshake.EarlyDataAccepted}");
        Diagnostics.Act("early data the server received, records it skipped", $"{server.EarlyData.Count}, {server.SkippedRecords}");
    }

    private static async Task<(Tls13ConnectResult Result, Tls13RecordTestServer Server)> ResumeAsync(Tls13TestServer testServer, Tls13ClientSettings settings, byte[] earlyData)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, testServer);
        Task serverHandshake = server.HandshakeAsync();
        Tls13ConnectResult result = await Tls13ClientConnection.ConnectWithEarlyDataAsync(
            clientEnd, settings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), earlyData, CancellationToken.None);
        await serverHandshake;
        Assert.IsNull(result.Failure);
        return (result, server);
    }

    private static async Task AssertApplicationDataFlowsAsync(Tls13ClientStream client, Tls13RecordTestServer server)
    {
        await client.WriteAsync("ping"u8.ToArray());
        CollectionAssert.AreEqual("ping"u8.ToArray(), await server.ReceiveApplicationDataAsync(4));
        await server.SendAsync(TlsContentType.ApplicationData, "pong"u8.ToArray());
        CollectionAssert.AreEqual("pong"u8.ToArray(), await Tls13PipeDriver.ReadAsync(client, 4));
    }
}
