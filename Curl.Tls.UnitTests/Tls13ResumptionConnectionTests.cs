using System.Text;

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

    [TestMethod]
    public async Task ExportedSessionResumesAndCarriesTheRequestAsEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = TlsSessionCodec.Decode(TlsSessionCodec.Encode(await FirstSessionAsync(tickets)))!;
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), Request);

        Tls13ClientHandshake handshake = result.Stream!.Handshake;
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
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets, AcceptEarlyData = false };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), Request);

        Assert.IsTrue(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataAccepted);
        Assert.AreEqual(1, server.SkippedRecords);
        Assert.IsEmpty(server.EarlyData);
        CollectionAssert.AreEqual(Request, await server.ReceiveApplicationDataAsync(Request.Length));
    }

    [TestMethod]
    public async Task ServerThatDoesNotResumeRunsAFullHandshakeAndGetsTheRequestAfterIt()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets, AcceptResumption = false };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), Request);

        Assert.IsFalse(result.Stream!.Handshake.IsResumed);
        Assert.IsTrue(result.Stream.Handshake.EarlyDataOffered);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataAccepted);
        Assert.HasCount(1, result.Stream.Handshake.ServerCertificates);
        CollectionAssert.AreEqual(Request, await server.ReceiveApplicationDataAsync(Request.Length));
    }

    [TestMethod]
    public async Task ExpiredTicketIsNotOfferedAndTheHandshakeIsAFullOne()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        Tls13ClientSettings settings = Resuming(session) with { TimeProvider = new FixedTimeProvider(session.ReceivedAt.AddSeconds(session.TicketLifetime)) };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, settings, Request);

        Assert.IsFalse(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataOffered);
        Assert.IsFalse(testServer.EarlyDataOffered);
        Assert.IsFalse(testServer.IsResumed);
        CollectionAssert.AreEqual(Request, await server.ReceiveApplicationDataAsync(Request.Length));
    }

    [TestMethod]
    public async Task EarlyDataBeyondTheTicketLimitIsSentAfterTheHandshake()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets, MaxEarlyDataSize = 4 });
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), Request);

        Assert.IsTrue(result.Stream!.Handshake.EarlyDataAccepted);
        Assert.AreEqual(4u, result.Stream.Handshake.MaxEarlyDataSize);
        CollectionAssert.AreEqual(Request[..4], server.EarlyData.ToArray());
        CollectionAssert.AreEqual(Request[4..], await server.ReceiveApplicationDataAsync(Request.Length - 4));
    }

    [TestMethod]
    public async Task OfferedEarlyDataWithNothingToSendStillEndsWithEndOfEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), []);

        Assert.IsTrue(result.Stream!.Handshake.EarlyDataAccepted);
        Assert.IsEmpty(server.EarlyData);
        await AssertApplicationDataFlowsAsync(result.Stream, server);
    }

    [TestMethod]
    public async Task MiddleboxCompatibilitySendsChangeCipherSpecBeforeTheEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session) with { SendLegacySessionId = true }, Request);

        Assert.IsTrue(result.Stream!.Handshake.EarlyDataAccepted);
        Assert.AreEqual(1, server.ChangeCipherSpecsReceived);
        CollectionAssert.AreEqual(Request, server.EarlyData.ToArray());
    }

    [TestMethod]
    public async Task HelloRetryRequestDropsTheEarlyDataAndResumesWithANewBinder()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets, Group = TlsNamedGroup.Secp256r1 };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), Request);

        Assert.IsTrue(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(result.Stream.Handshake.EarlyDataAccepted);
        Assert.IsTrue(testServer.IsResumed);
        Assert.IsFalse(testServer.EarlyDataOffered);
        Assert.AreEqual(1, server.SkippedRecords);
        CollectionAssert.AreEqual(Request, await server.ReceiveApplicationDataAsync(Request.Length));
    }

    [TestMethod]
    public async Task HelloRetryRequestForASuiteOfAnotherHashDropsTheTicket()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519())
        {
            Tickets = tickets,
            Group = TlsNamedGroup.Secp256r1,
            CipherSuite = Tls13CipherSuite.Aes256GcmSha384.Code,
        };

        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(session), Request);

        Assert.IsFalse(result.Stream!.Handshake.IsResumed);
        Assert.IsFalse(testServer.IsResumed);
        CollectionAssert.AreEqual(Request, await server.ReceiveApplicationDataAsync(Request.Length));
    }

    [TestMethod]
    public async Task ResumedConnectionRecordsItsNewTicketWithTheFirstServerCertificate()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord first = await FirstSessionAsync(tickets);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        (Tls13ConnectResult result, Tls13RecordTestServer server) = await ResumeAsync(testServer, Resuming(first), Request);

        await server.SendNewSessionTicketAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [0x2a]);
        await Tls13PipeDriver.ReadAsync(result.Stream!, 1);

        TlsSessionRecord second = result.Stream!.Handshake.ReceivedSessions.Single();
        CollectionAssert.AreEqual(first.PeerCertificate, second.PeerCertificate);
        CollectionAssert.AreNotEqual(first.Ticket, second.Ticket);
    }

    [TestMethod]
    public async Task ConnectWithEarlyDataWithoutASessionSendsTheDataAfterTheHandshake()
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, new Tls13TestServer(TestServerCredential.Ed25519()));
        Task serverHandshake = server.HandshakeAsync();

        Tls13ConnectResult result = await Tls13ClientConnection.ConnectWithEarlyDataAsync(
            clientEnd, Settings with { OfferEarlyData = true }, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), Request, CancellationToken.None);
        await serverHandshake;

        Assert.IsFalse(result.Stream!.Handshake.EarlyDataOffered);
        CollectionAssert.AreEqual(Request, await server.ReceiveApplicationDataAsync(Request.Length));
    }

    [TestMethod]
    public async Task ConnectWithEarlyDataChecksItsTransport() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ClientConnection.ConnectWithEarlyDataAsync(
            null!, Settings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), Request, CancellationToken.None));

    [TestMethod]
    public async Task FailedResumptionReturnsTheFailureWithoutSendingTheRequest()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = await FirstSessionAsync(tickets);
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets });
        Task<Tls13ConnectResult> pending = Tls13ClientConnection.ConnectWithEarlyDataAsync(
            clientEnd, Resuming(session), SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), Request, CancellationToken.None);

        await server.ReceiveClientHelloAsync();
        await serverEnd.DisposeAsync();
        Tls13ConnectResult result = await pending;

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
