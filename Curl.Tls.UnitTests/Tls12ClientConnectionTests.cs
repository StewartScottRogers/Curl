using System.Security.Cryptography;
using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0xc02b, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0xcca9, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0xc009, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls11, (ushort)0xc009, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls10, (ushort)0xc013, "rsa")]
    public async Task ExchangeCrossesBothWaysAndClosesWithCloseNotifyEachWay(TlsProtocolVersion version, int cipherSuite, string credential)
    {
        Diagnostics.Arrange("server version", version);
        Diagnostics.Arrange("server cipher suite", $"0x{cipherSuite:x4}");
        Diagnostics.Arrange("server credential", credential);
        Tls12TestServer testServer = new(Credential(credential)) { Version = version, CipherSuite = (ushort)cipherSuite };
        Tls12ClientStream client;
        Tls12RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(testServer);
        }

        await using Tls12ClientStream stream = client;
        byte[] request = RandomNumberGenerator.GetBytes(40000);
        byte[] response = RandomNumberGenerator.GetBytes(20000);
        Diagnostics.Arrange("request length", request.Length);
        Diagnostics.Arrange("response length", response.Length);

        byte[] requested;
        byte[] responded = new byte[response.Length];
        Tls12OutgoingMessage clientClose;
        int end;
        using (Diagnostics.Phase("exchange and close"))
        {
            await stream.WriteAsync(request);
            requested = await server.ReceiveApplicationDataAsync(request.Length);
            await server.SendAsync(TlsContentType.ApplicationData, response);
            await stream.ReadExactlyAsync(responded);
            await stream.ShutdownAsync();
            clientClose = (await server.ReceiveAsync())!;
            await server.SendAsync(TlsContentType.Alert, [1, 0]);
            end = await stream.ReadAsync(new byte[10]);
        }

        Diagnostics.Act("negotiated version", stream.Handshake.Version);
        Diagnostics.Act("negotiated cipher suite", $"0x{stream.Handshake.CipherSuite!.Code:x4}");
        Diagnostics.Act("record versions", string.Join(", ", server.RecordVersions.Select(recordVersion => $"0x{recordVersion:x4}")));
        Diagnostics.Act("client's close", $"{clientClose.ContentType} {Convert.ToHexStringLower(clientClose.Bytes)}");
        Diagnostics.Act("read after close_notify", end);

        Diagnostics.Diff("request the server received", request, requested);
        Diagnostics.Diff("response the client read", response, responded);
        Diagnostics.Assert("close_notify received", true, stream.CloseNotifyReceived);
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
    public async Task ClientHelloRecordVersionIsTheVersionOfTheHellosRecord()
    {
        Tls12TestServer testServer = new(Credential("rsa")) { Version = TlsProtocolVersion.Tls12, CipherSuite = 0xc02f };
        Diagnostics.Arrange("ClientHello record version", TlsProtocolVersion.Tls12);

        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync(testServer, DefaultSettings with { ClientHelloRecordVersion = TlsProtocolVersion.Tls12 });
        await using Tls12ClientStream stream = client;
        Diagnostics.Act("first record version", $"0x{server.RecordVersions[0]:x4}");

        Diagnostics.Assert("first record version", "0x0303", $"0x{server.RecordVersions[0]:x4}");
        Assert.AreEqual((ushort)0x0303, server.RecordVersions[0]);
    }

    [TestMethod]
    [DataRow(true, new[] { 0, 3 })]
    [DataRow(false, new[] { 3 })]
    public async Task TlsOneZeroCbcWritesAnEmptyRecordFirstUnlessTurnedOff(bool insertEmptyFragment, int[] recordLengths)
    {
        Tls12TestServer testServer = new(Credential("rsa")) { Version = TlsProtocolVersion.Tls10, CipherSuite = 0x002f };
        Diagnostics.Arrange("insert empty fragment", insertEmptyFragment);
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync(testServer, DefaultSettings with { InsertEmptyFragment = insertEmptyFragment });
        await using Tls12ClientStream stream = client;

        await stream.WriteAsync("abc"u8.ToArray());
        byte[] received = await server.ReceiveApplicationDataAsync(3);
        await server.SendAsync(TlsContentType.ApplicationData, "reply"u8.ToArray());
        byte[] reply = new byte[5];
        await stream.ReadExactlyAsync(reply);
        Diagnostics.Act("application data record lengths", string.Join(", ", server.ApplicationDataLengths));

        Diagnostics.Assert("application data record lengths", string.Join(", ", recordLengths), string.Join(", ", server.ApplicationDataLengths));
        Diagnostics.Diff("data the server received", "abc"u8, received);
        Diagnostics.Diff("reply the client read", "reply"u8, reply);
        CollectionAssert.AreEqual("abc"u8.ToArray(), received);
        CollectionAssert.AreEqual(recordLengths, server.ApplicationDataLengths);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
    }

    [TestMethod]
    public async Task ServerFlightSplitAcrossRecordsCompletesTheHandshake()
    {
        Diagnostics.Arrange("handshake record length", 7);

        (Tls12ClientStream client, _, _) = await ConnectAsync(handshakeRecordLength: 7);
        await using Tls12ClientStream stream = client;
        Diagnostics.Act("handshake complete", stream.Handshake.IsComplete);

        Diagnostics.Assert("handshake complete", true, stream.Handshake.IsComplete);
        Assert.IsTrue(stream.Handshake.IsComplete);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SecondConnectionResumesTheFirstSessionAndExchangesData(bool issueTicket)
    {
        Tls12TestSessionCache sessions = new();
        Diagnostics.Arrange("server issues a ticket", issueTicket);
        Tls12ClientStream first;
        using (Diagnostics.Phase("first handshake"))
        {
            (first, _, _) = await ConnectAsync(new Tls12TestServer(TestServerCredential.Ed25519()) { Sessions = sessions, IssueTicket = issueTicket });
        }

        Tls12Session session = first.Handshake.Session!;
        await first.DisposeAsync();
        Diagnostics.Act("first session has a ticket", session.Ticket is not null);

        Tls12ClientStream second;
        Tls12RecordTestServer server;
        using (Diagnostics.Phase("resumed handshake"))
        {
            (second, server, _) = await ConnectAsync(
                new Tls12TestServer(TestServerCredential.Ed25519()) { Sessions = sessions, IssueTicket = issueTicket },
                DefaultSettings with { SessionToResume = session });
        }

        await using Tls12ClientStream stream = second;
        await stream.WriteAsync("up"u8.ToArray());
        byte[] up = await server.ReceiveApplicationDataAsync(2);
        await server.SendAsync(TlsContentType.ApplicationData, "down"u8.ToArray());
        byte[] down = new byte[4];
        await stream.ReadExactlyAsync(down);
        Diagnostics.Act("second handshake resumed", stream.Handshake.IsResumed);

        Diagnostics.Assert("first session has a ticket", issueTicket, session.Ticket is not null);
        Diagnostics.Diff("data the server received", "up"u8, up);
        Diagnostics.Diff("data the client read", "down"u8, down);
        Assert.AreEqual(issueTicket, session.Ticket is not null);
        Assert.IsTrue(stream.Handshake.IsResumed);
        CollectionAssert.AreEqual("up"u8.ToArray(), up);
        CollectionAssert.AreEqual("down"u8.ToArray(), down);
    }

    [TestMethod]
    public async Task RejectedChainSendsTheAlertAndReturnsTheRejection()
    {
        object reason = new();
        Diagnostics.Arrange("certificate verifier", "rejects the chain");
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));
        await server.SendFlightAsync(await server.AnswerClientHelloAsync());

        Tls12ConnectResult result = await pending;
        WriteFailure(result);
        TlsAlertDescription alertReceived = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert the server received", alertReceived);

        Diagnostics.Assert("rejection is the verifier's", true, ReferenceEquals(reason, result.Failure?.CertificateRejection));
        Diagnostics.Assert("alert the server received", result.Failure?.Alert, alertReceived);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Stream);
        Assert.AreSame(reason, result.Failure!.CertificateRejection);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(result.Failure.Alert, alertReceived);
    }

    [TestMethod]
    public async Task ServerAlertDuringTheHandshakeIsReceived()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        Diagnostics.Arrange("server sends", "fatal handshake_failure alert");
        await server.SendAsync(TlsContentType.Alert, [2, 40]);

        Tls12ConnectResult result = await pending;
        WriteFailure(result);

        Diagnostics.Assert("failure", $"{TlsAlertDescription.HandshakeFailure} {TlsHandshakeFailureOrigin.AlertReceived}", $"{result.Failure?.Alert} {result.Failure?.Origin}");
        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
    }

    [TestMethod]
    public async Task TransportClosingAfterTheServerFlightFailsTheHandshake()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, Stream serverEnd) = Start();
        await server.SendFlightAsync(await server.AnswerClientHelloAsync());
        Diagnostics.Arrange("transport", "closes after the server's flight");
        await serverEnd.DisposeAsync();

        Tls12ConnectResult result = await pending;
        WriteFailure(result);

        Diagnostics.Assert("failure", $"{TlsAlertDescription.CloseNotify} {TlsHandshakeFailureOrigin.TransportClosed}", $"{result.Failure?.Alert} {result.Failure?.Origin}");
        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
        Assert.AreEqual(TlsAlertDescription.CloseNotify, result.Failure.Alert);
    }

    [TestMethod]
    public async Task TransportClosingInsideARecordFailsTheHandshake()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, Stream serverEnd) = Start();
        await server.AnswerClientHelloAsync();
        byte[] partialRecord = [0x16, 0x03, 0x03, 0x00, 0x10, 0x02];
        Diagnostics.Arrange("partial record before the transport closes", Convert.ToHexStringLower(partialRecord));
        await server.SendRawAsync(partialRecord);
        await serverEnd.DisposeAsync();

        Tls12ConnectResult result = await pending;
        WriteFailure(result);

        Diagnostics.Assert("failure origin", TlsHandshakeFailureOrigin.TransportClosed, result.Failure?.Origin);
        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
    }

    [TestMethod]
    public async Task RecordOfAnotherMajorVersionBeforeTheServerHelloIsAProtocolVersionError()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        byte[] record = [0x16, 0x02, 0x00, 0x00, 0x00];
        Diagnostics.Arrange("record header of version 0x0200", Convert.ToHexStringLower(record));
        await server.SendRawAsync(record);

        Tls12ConnectResult result = await pending;
        WriteFailure(result);
        TlsAlertDescription alertReceived = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert the server received", alertReceived);

        Diagnostics.Assert("failure", $"{TlsAlertDescription.ProtocolVersion} {TlsHandshakeFailureOrigin.AlertSent}", $"{result.Failure?.Alert} {result.Failure?.Origin}");
        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, alertReceived);
    }

    [TestMethod]
    public async Task RecordOfAnotherVersionAfterTheServerHelloIsAProtocolVersionError()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        List<Tls12OutgoingMessage> flight = await server.AnswerClientHelloAsync();
        await server.SendFlightAsync(flight.Take(1));
        byte[] record = [0x16, 0x03, 0x01, 0x00, 0x00];
        Diagnostics.Arrange("record header of version 0x0301 after a TLS 1.2 ServerHello", Convert.ToHexStringLower(record));
        await server.SendRawAsync(record);

        Tls12ConnectResult result = await pending;
        WriteFailure(result);
        TlsAlertDescription alertReceived = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert the server received", alertReceived);

        Diagnostics.Assert("alert", TlsAlertDescription.ProtocolVersion, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, alertReceived);
    }

    [TestMethod]
    public async Task ApplicationDataDuringTheHandshakeIsUnexpected()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        Diagnostics.Arrange("server sends", "an application data record before its flight");
        await server.SendAsync(TlsContentType.ApplicationData, [1]);

        Tls12ConnectResult result = await pending;
        WriteFailure(result);
        TlsAlertDescription alertReceived = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert the server received", alertReceived);

        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alertReceived);
    }

    [TestMethod]
    public async Task ChangeCipherSpecBeforeTheServerHelloIsUnexpected()
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, _) = Start();
        await server.AnswerClientHelloAsync();
        Diagnostics.Arrange("server sends", "a ChangeCipherSpec record before its ServerHello");
        await server.SendAsync(TlsContentType.ChangeCipherSpec, [1]);

        Tls12ConnectResult result = await pending;
        WriteFailure(result);
        TlsAlertDescription alertReceived = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert the server received", alertReceived);

        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alertReceived);
    }

    [TestMethod]
    public async Task MissingArgumentsAreRefusedBeforeAnythingIsSent()
    {
        ScriptedTransport transport = new([]);
        Diagnostics.Arrange("arguments left null", "transport, then settings");

        ArgumentNullException noTransport = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls12ClientConnection.ConnectAsync(
            null!, DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        ArgumentNullException noSettings = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls12ClientConnection.ConnectAsync(
            transport, null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        Diagnostics.Act("parameters named", $"{noTransport.ParamName}, {noSettings.ParamName}");
        Diagnostics.Act("bytes written", transport.Written.Length);

        Diagnostics.Assert("transport disposed", false, transport.IsDisposed);
        Assert.IsEmpty(transport.Written);
        Assert.IsFalse(transport.IsDisposed);
    }

    private static TestServerCredential Credential(string name) => name == "rsa"
        ? TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)
        : TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);

    private void WriteFailure(Tls12ConnectResult result)
    {
        Diagnostics.Act("succeeded", result.Succeeded);
        Diagnostics.Act("failure alert", result.Failure?.Alert);
        Diagnostics.Act("failure origin", result.Failure?.Origin);
    }
}
