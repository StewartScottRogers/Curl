using static Curl.Tls.Tls13PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Runs <see cref="Tls13ClientConnection" />'s handshake over a pipe to the in-memory
/// server: middlebox compatibility mode, the first ClientHello's record version, and every
/// way a handshake over records fails - an alert sent, an alert received, a transport that
/// closes, and each record the client refuses.
/// </summary>
[TestClass]
public sealed class Tls13ClientConnectionTests
{
    [TestMethod]
    public async Task CompatibilityModeSendsOneChangeCipherSpecAndIgnoresTheServers()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(
            settings: DefaultSettings with { SendLegacySessionId = true }, serverSendsChangeCipherSpec: true);
        await using Tls13ClientStream stream = client;

        Assert.AreEqual(1, server.ChangeCipherSpecsReceived);
        CollectionAssert.AreEqual(new ushort[] { 0x0301 }, server.ClientHelloRecordVersions);
    }

    [TestMethod]
    public async Task WithoutCompatibilityModeNoChangeCipherSpecIsSent()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(serverSendsChangeCipherSpec: true);
        await using Tls13ClientStream stream = client;

        Assert.AreEqual(0, server.ChangeCipherSpecsReceived);
    }

    [TestMethod]
    public async Task HelloRetryRequestInCompatibilityModeSendsTheChangeCipherSpecBeforeTheSecondHello()
    {
        Tls13TestServer testServer = new(TestServerCredential.Ed25519()) { Group = TlsNamedGroup.Secp256r1 };
        Tls13ClientSettings settings = DefaultSettings with
        {
            SendLegacySessionId = true,
            SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1],
        };

        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(testServer, settings);
        await using Tls13ClientStream stream = client;

        Assert.AreEqual(1, server.ChangeCipherSpecsReceived);
        CollectionAssert.AreEqual(new ushort[] { 0x0301, 0x0303 }, server.ClientHelloRecordVersions);
        Assert.AreEqual<ushort?>(TlsNamedGroup.Secp256r1, stream.Handshake.NegotiatedGroup);
    }

    [TestMethod]
    public async Task FirstClientHelloRecordCarriesTheConfiguredVersion()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(settings: DefaultSettings with { ClientHelloRecordVersion = 0x0303 });
        await using Tls13ClientStream stream = client;

        CollectionAssert.AreEqual(new ushort[] { 0x0303 }, server.ClientHelloRecordVersions);
    }

    [TestMethod]
    public async Task RejectedCertificateSendsTheAlertUnderTheHandshakeKeys()
    {
        object reason = new();
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));
        TestServerFlight flight = await server.SendServerHelloAsync();
        await server.SendAsync(TlsContentType.Handshake, flight.Handshake);

        Tls13ConnectResult result = await pending;

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Stream);
        Assert.AreSame(reason, result.Failure!.CertificateRejection);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task CorruptedServerFlightSendsBadRecordMac()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        TestServerFlight flight = await server.SendServerHelloAsync();
        byte[] record = server.Protect(TlsContentType.Handshake, flight.Handshake);
        record[10] ^= 0x01;
        await server.SendRawAsync(record);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task PlaintextAlertInsteadOfAServerHelloIsReceived()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.Alert, [2, 40]);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
    }

    [TestMethod]
    public async Task ProtectedAlertDuringTheHandshakeIsReceived()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.SendServerHelloAsync();
        await server.SendAsync(TlsContentType.Alert, [2, 80]);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.InternalError, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
    }

    [TestMethod]
    public async Task TransportClosingBeforeTheServerHelloFailsTheHandshake()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, Stream serverEnd) = Start();
        await server.ReceiveClientHelloAsync();
        await serverEnd.DisposeAsync();

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
        Assert.AreEqual(TlsAlertDescription.CloseNotify, result.Failure.Alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { 1, 1 })]
    public async Task ChangeCipherSpecOtherThanTheSingleByteOneIsUnexpected(byte[] content)
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.ChangeCipherSpec, content);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task ApplicationDataBeforeTheServerHelloIsUnexpected()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [1]);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task EmptyHandshakeRecordIsUnexpected()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.Handshake, []);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task PlaintextRecordOverTheLimitIsARecordOverflow()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.Handshake, new byte[Tls13RecordProtection.MaximumPlaintextLength + 1]);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task PlaintextHandshakeRecordAfterTheServerHelloIsUnexpected()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        TestServerFlight flight = await server.SendServerHelloAsync();
        await server.SendRawAsync([0x16, 0x03, 0x03, 0x00, 0x04, .. flight.Handshake[..4]]);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task ProtectedApplicationDataDuringTheHandshakeIsUnexpected()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.SendServerHelloAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [1]);

        Tls13ConnectResult result = await pending;

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task SuiteWhoseRecordsCannotBeProtectedIsRefusedBeforeAnythingIsSent()
    {
        ScriptedTransport transport = new([]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Tls13ClientConnection.ConnectAsync(
            transport, DefaultSettings with { CipherSuites = [0x1306] }, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ClientConnection.ConnectAsync(
            null!, DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ClientConnection.ConnectAsync(
            transport, null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));

        Assert.IsEmpty(transport.Written);
        Assert.IsFalse(transport.IsDisposed);
    }
}
