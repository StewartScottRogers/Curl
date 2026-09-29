using System.Security.Cryptography;
using static Curl.Tls.Tls12PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Runs <see cref="Tls12ClientConnection" /> over a pipe to the in-memory TLS 1.2 server:
/// whole exchanges in TLS 1.2, 1.1 and 1.0 with AEAD and CBC suites, TLS 1.0's empty
/// fragment, a flight split across records, resumption, and every way a handshake over
/// records fails - an alert sent, an alert received and a transport that closes.
/// </summary>
[TestClass]
public sealed class Tls12ClientConnectionTests
{
    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0xc02b, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0xcca9, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0xc009, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls11, (ushort)0xc009, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls10, (ushort)0xc013, "rsa")]
    public async Task ExchangeCrossesBothWaysAndClosesWithCloseNotifyEachWay(TlsProtocolVersion version, int cipherSuite, string credential)
    {
        Tls12TestServer testServer = new(Credential(credential)) { Version = version, CipherSuite = (ushort)cipherSuite };
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync(testServer);
        await using Tls12ClientStream stream = client;
        byte[] request = RandomNumberGenerator.GetBytes(40000);
        byte[] response = RandomNumberGenerator.GetBytes(20000);

        await stream.WriteAsync(request);
        byte[] requested = await server.ReceiveApplicationDataAsync(request.Length);
        await server.SendAsync(TlsContentType.ApplicationData, response);
        byte[] responded = new byte[response.Length];
        await stream.ReadExactlyAsync(responded);
        await stream.ShutdownAsync();
        Tls12OutgoingMessage clientClose = (await server.ReceiveAsync())!;
        await server.SendAsync(TlsContentType.Alert, [1, 0]);
        int end = await stream.ReadAsync(new byte[10]);

        CollectionAssert.AreEqual(request, requested);
        CollectionAssert.AreEqual(response, responded);
        Assert.AreEqual(TlsContentType.Alert, clientClose.ContentType);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, clientClose.Bytes);
        Assert.AreEqual(0, end);
        Assert.IsTrue(stream.CloseNotifyReceived);
        Assert.AreEqual(version, stream.Handshake.Version);
        Assert.AreEqual(cipherSuite, stream.Handshake.CipherSuite!.Code);
        Assert.AreEqual((ushort)0x0301, server.RecordVersions[0]);
        Assert.IsTrue(server.RecordVersions.Skip(1).All(recordVersion => recordVersion == (ushort)version));
    }

    [TestMethod]
    [DataRow(true, new[] { 0, 3 })]
    [DataRow(false, new[] { 3 })]
    public async Task TlsOneZeroCbcWritesAnEmptyRecordFirstUnlessTurnedOff(bool insertEmptyFragment, int[] recordLengths)
    {
        Tls12TestServer testServer = new(Credential("rsa")) { Version = TlsProtocolVersion.Tls10, CipherSuite = 0x002f };
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync(testServer, DefaultSettings with { InsertEmptyFragment = insertEmptyFragment });
        await using Tls12ClientStream stream = client;

        await stream.WriteAsync("abc"u8.ToArray());
        byte[] received = await server.ReceiveApplicationDataAsync(3);
        await server.SendAsync(TlsContentType.ApplicationData, "reply"u8.ToArray());
        byte[] reply = new byte[5];
        await stream.ReadExactlyAsync(reply);

        CollectionAssert.AreEqual("abc"u8.ToArray(), received);
        CollectionAssert.AreEqual(recordLengths, server.ApplicationDataLengths);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
    }

    [TestMethod]
    public async Task ServerFlightSplitAcrossRecordsCompletesTheHandshake()
    {
        (Tls12ClientStream client, _, _) = await ConnectAsync(handshakeRecordLength: 7);
        await using Tls12ClientStream stream = client;

        Assert.IsTrue(stream.Handshake.IsComplete);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SecondConnectionResumesTheFirstSessionAndExchangesData(bool issueTicket)
    {
        Tls12TestSessionCache sessions = new();
        (Tls12ClientStream first, _, _) = await ConnectAsync(new Tls12TestServer(TestServerCredential.Ed25519()) { Sessions = sessions, IssueTicket = issueTicket });
        Tls12Session session = first.Handshake.Session!;
        await first.DisposeAsync();

        (Tls12ClientStream second, Tls12RecordTestServer server, _) = await ConnectAsync(
            new Tls12TestServer(TestServerCredential.Ed25519()) { Sessions = sessions, IssueTicket = issueTicket },
            DefaultSettings with { SessionToResume = session });
        await using Tls12ClientStream stream = second;
        await stream.WriteAsync("up"u8.ToArray());
        byte[] up = await server.ReceiveApplicationDataAsync(2);
        await server.SendAsync(TlsContentType.ApplicationData, "down"u8.ToArray());
        byte[] down = new byte[4];
        await stream.ReadExactlyAsync(down);

        Assert.AreEqual(issueTicket, session.Ticket is not null);
        Assert.IsTrue(stream.Handshake.IsResumed);
        CollectionAssert.AreEqual("up"u8.ToArray(), up);
        CollectionAssert.AreEqual("down"u8.ToArray(), down);
    }

    [TestMethod]
    public async Task RejectedChainSendsTheAlertAndReturnsTheRejection()
    {
        object reason = new();
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));
        await server.SendFlightAsync(await server.AnswerClientHelloAsync());

        Tls12ConnectResult result = await pending;

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Stream);
        Assert.AreSame(reason, result.Failure!.CertificateRejection);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(result.Failure.Alert, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task ServerAlertDuringTheHandshakeIsReceived()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        await server.SendAsync(TlsContentType.Alert, [2, 40]);

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
    }

    [TestMethod]
    public async Task TransportClosingAfterTheServerFlightFailsTheHandshake()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, Stream serverEnd) = Start();
        await server.SendFlightAsync(await server.AnswerClientHelloAsync());
        await serverEnd.DisposeAsync();

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
        Assert.AreEqual(TlsAlertDescription.CloseNotify, result.Failure.Alert);
    }

    [TestMethod]
    public async Task TransportClosingInsideARecordFailsTheHandshake()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, Stream serverEnd) = Start();
        await server.AnswerClientHelloAsync();
        await server.SendRawAsync([0x16, 0x03, 0x03, 0x00, 0x10, 0x02]);
        await serverEnd.DisposeAsync();

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
    }

    [TestMethod]
    public async Task RecordOfAnotherMajorVersionBeforeTheServerHelloIsAProtocolVersionError()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        await server.SendRawAsync([0x16, 0x02, 0x00, 0x00, 0x00]);

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task RecordOfAnotherVersionAfterTheServerHelloIsAProtocolVersionError()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        List<Tls12OutgoingMessage> flight = await server.AnswerClientHelloAsync();
        await server.SendFlightAsync(flight.Take(1));
        await server.SendRawAsync([0x16, 0x03, 0x01, 0x00, 0x00]);

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task ApplicationDataDuringTheHandshakeIsUnexpected()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [1]);

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task ChangeCipherSpecBeforeTheServerHelloIsUnexpected()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        await server.SendAsync(TlsContentType.ChangeCipherSpec, [1]);

        Tls12ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task MissingArgumentsAreRefusedBeforeAnythingIsSent()
    {
        ScriptedTransport transport = new([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls12ClientConnection.ConnectAsync(
            null!, DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls12ClientConnection.ConnectAsync(
            transport, null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));

        Assert.IsEmpty(transport.Written);
        Assert.IsFalse(transport.IsDisposed);
    }

    private static TestServerCredential Credential(string name) => name == "rsa"
        ? TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)
        : TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);
}
