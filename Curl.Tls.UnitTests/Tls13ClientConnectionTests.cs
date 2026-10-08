using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task CompatibilityModeSendsOneChangeCipherSpecAndIgnoresTheServers()
    {
        Diagnostics.Arrange("settings", "SendLegacySessionId = true");
        Diagnostics.Arrange("server", "sends a ChangeCipherSpec");
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectTimedAsync(
            settings: DefaultSettings with { SendLegacySessionId = true }, serverSendsChangeCipherSpec: true);
        await using Tls13ClientStream stream = client;

        WriteServer(server);
        Diagnostics.Assert("ChangeCipherSpecs received", 1, server.ChangeCipherSpecsReceived);
        Diagnostics.Assert("ClientHello record versions", "0x0301", Versions(server));
        Assert.AreEqual(1, server.ChangeCipherSpecsReceived);
        CollectionAssert.AreEqual(new ushort[] { 0x0301 }, server.ClientHelloRecordVersions);
    }

    [TestMethod]
    public async Task WithoutCompatibilityModeNoChangeCipherSpecIsSent()
    {
        Diagnostics.Arrange("settings", "SendLegacySessionId = false");
        Diagnostics.Arrange("server", "sends a ChangeCipherSpec");
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectTimedAsync(serverSendsChangeCipherSpec: true);
        await using Tls13ClientStream stream = client;

        WriteServer(server);
        Diagnostics.Assert("ChangeCipherSpecs received", 0, server.ChangeCipherSpecsReceived);
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
        Diagnostics.Arrange("client groups", "x25519, secp256r1; SendLegacySessionId = true");
        Diagnostics.Arrange("server group", "secp256r1 (forces a HelloRetryRequest)");

        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectTimedAsync(testServer, settings);
        await using Tls13ClientStream stream = client;

        WriteServer(server);
        Diagnostics.Act("negotiated group", stream.Handshake.NegotiatedGroup);
        Diagnostics.Assert("ChangeCipherSpecs received", 1, server.ChangeCipherSpecsReceived);
        Diagnostics.Assert("ClientHello record versions", "0x0301, 0x0303", Versions(server));
        Diagnostics.Assert("negotiated group", TlsNamedGroup.Secp256r1, stream.Handshake.NegotiatedGroup);
        Assert.AreEqual(1, server.ChangeCipherSpecsReceived);
        CollectionAssert.AreEqual(new ushort[] { 0x0301, 0x0303 }, server.ClientHelloRecordVersions);
        Assert.AreEqual<ushort?>(TlsNamedGroup.Secp256r1, stream.Handshake.NegotiatedGroup);
    }

    [TestMethod]
    public async Task FirstClientHelloRecordCarriesTheConfiguredVersion()
    {
        Diagnostics.Arrange("ClientHelloRecordVersion", "0x0303");
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectTimedAsync(settings: DefaultSettings with { ClientHelloRecordVersion = 0x0303 });
        await using Tls13ClientStream stream = client;

        WriteServer(server);
        Diagnostics.Assert("ClientHello record versions", "0x0303", Versions(server));
        CollectionAssert.AreEqual(new ushort[] { 0x0303 }, server.ClientHelloRecordVersions);
    }

    [TestMethod]
    public async Task RejectedCertificateSendsTheAlertUnderTheHandshakeKeys()
    {
        object reason = new();
        Diagnostics.Arrange("verifier verdict", "rejected");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));
        TestServerFlight flight = await server.SendServerHelloAsync();
        await server.SendAsync(TlsContentType.Handshake, flight.Handshake);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("succeeded", false, result.Succeeded);
        Diagnostics.Assert("rejection is the verifier's reason", true, ReferenceEquals(reason, result.Failure?.CertificateRejection));
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.AlertSent, result.Failure?.Origin);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Stream);
        Assert.AreSame(reason, result.Failure!.CertificateRejection);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, await ReceiveAlertAsync(server));
    }

    [TestMethod]
    public async Task CorruptedServerFlightSendsBadRecordMac()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        TestServerFlight flight = await server.SendServerHelloAsync();
        byte[] record = server.Protect(TlsContentType.Handshake, flight.Handshake);
        record[10] ^= 0x01;
        Diagnostics.Arrange("corruption", "byte 10 of the protected server flight flipped");
        Diagnostics.Bytes("corrupted record", record);
        await server.SendRawAsync(record);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, result.Failure?.Alert);
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.AlertSent, result.Failure?.Origin);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, await ReceiveAlertAsync(server));
    }

    [TestMethod]
    public async Task PlaintextAlertInsteadOfAServerHelloIsReceived()
    {
        Diagnostics.Arrange("server reply", "plaintext alert 2, 40 (fatal handshake_failure)");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.Alert, [2, 40]);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.HandshakeFailure, result.Failure?.Alert);
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.AlertReceived, result.Failure?.Origin);
        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
    }

    [TestMethod]
    public async Task ProtectedAlertDuringTheHandshakeIsReceived()
    {
        Diagnostics.Arrange("server reply", "ServerHello, then protected alert 2, 80 (fatal internal_error)");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.SendServerHelloAsync();
        await server.SendAsync(TlsContentType.Alert, [2, 80]);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.InternalError, result.Failure?.Alert);
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.AlertReceived, result.Failure?.Origin);
        Assert.AreEqual(TlsAlertDescription.InternalError, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
    }

    [TestMethod]
    public async Task TransportClosingBeforeTheServerHelloFailsTheHandshake()
    {
        Diagnostics.Arrange("server reply", "transport closed after the ClientHello");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, Stream serverEnd) = Start();
        await server.ReceiveClientHelloAsync();
        await serverEnd.DisposeAsync();

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.TransportClosed, result.Failure?.Origin);
        Diagnostics.Assert("alert", TlsAlertDescription.CloseNotify, result.Failure?.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
        Assert.AreEqual(TlsAlertDescription.CloseNotify, result.Failure.Alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { 1, 1 })]
    public async Task ChangeCipherSpecOtherThanTheSingleByteOneIsUnexpected(byte[] content)
    {
        Diagnostics.Bytes("ChangeCipherSpec content", content);
        Diagnostics.Arrange("server reply", "ChangeCipherSpec record instead of a ServerHello");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.ChangeCipherSpec, content);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveAlertAsync(server));
    }

    [TestMethod]
    public async Task ApplicationDataBeforeTheServerHelloIsUnexpected()
    {
        Diagnostics.Arrange("server reply", "plaintext application_data record [1]");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [1]);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task EmptyHandshakeRecordIsUnexpected()
    {
        Diagnostics.Arrange("server reply", "empty handshake record");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.Handshake, []);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task PlaintextRecordOverTheLimitIsARecordOverflow()
    {
        Diagnostics.Arrange("server reply", $"plaintext handshake record of {Tls13RecordProtection.MaximumPlaintextLength + 1} bytes");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.ReceiveClientHelloAsync();
        await server.SendAsync(TlsContentType.Handshake, new byte[Tls13RecordProtection.MaximumPlaintextLength + 1]);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.RecordOverflow, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.RecordOverflow, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task PlaintextHandshakeRecordAfterTheServerHelloIsUnexpected()
    {
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        TestServerFlight flight = await server.SendServerHelloAsync();
        byte[] record = [0x16, 0x03, 0x03, 0x00, 0x04, .. flight.Handshake[..4]];
        Diagnostics.Arrange("server reply", "ServerHello, then a plaintext handshake record");
        Diagnostics.Bytes("plaintext record", record);
        await server.SendRawAsync(record);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveAlertAsync(server));
    }

    [TestMethod]
    public async Task ProtectedApplicationDataDuringTheHandshakeIsUnexpected()
    {
        Diagnostics.Arrange("server reply", "ServerHello, then protected application_data [1]");
        (Task<Tls13ConnectResult> pending, Tls13RecordTestServer server, _) = Start();
        await server.SendServerHelloAsync();
        await server.SendAsync(TlsContentType.ApplicationData, [1]);

        Tls13ConnectResult result = await AwaitTimedAsync(pending);

        WriteResult(result);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task SuiteWhoseRecordsCannotBeProtectedIsRefusedBeforeAnythingIsSent()
    {
        ScriptedTransport transport = new([]);
        Diagnostics.Arrange("cipher suites", "0x1306 (records cannot be protected); then a null transport, then null settings");

        ArgumentException suite = await Assert.ThrowsExactlyAsync<ArgumentException>(() => Tls13ClientConnection.ConnectAsync(
            transport, DefaultSettings with { CipherSuites = [0x1306] }, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ClientConnection.ConnectAsync(
            null!, DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Tls13ClientConnection.ConnectAsync(
            transport, null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None));

        Diagnostics.Act("thrown for 0x1306", $"{suite.GetType().Name}: {suite.Message}");
        Diagnostics.Act("bytes written", transport.Written.Length);
        Diagnostics.Assert("transport disposed", false, transport.IsDisposed);
        Assert.IsEmpty(transport.Written);
        Assert.IsFalse(transport.IsDisposed);
    }

    private static string Versions(Tls13RecordTestServer server) =>
        string.Join(", ", server.ClientHelloRecordVersions.Select(version => $"0x{version:x4}"));

    private async Task<(Tls13ClientStream Client, Tls13RecordTestServer Server, Stream ServerEnd)> ConnectTimedAsync(
        Tls13TestServer? testServer = null, Tls13ClientSettings? settings = null, bool serverSendsChangeCipherSpec = false)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return await ConnectAsync(testServer, settings, serverSendsChangeCipherSpec);
        }
    }

    private async Task<Tls13ConnectResult> AwaitTimedAsync(Task<Tls13ConnectResult> pending)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return await pending;
        }
    }

    private async Task<TlsAlertDescription> ReceiveAlertAsync(Tls13RecordTestServer server)
    {
        TlsAlertDescription alert = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert the server received", alert);
        return alert;
    }

    private void WriteServer(Tls13RecordTestServer server)
    {
        Diagnostics.Act("ChangeCipherSpecs received", server.ChangeCipherSpecsReceived);
        Diagnostics.Act("ClientHello record versions", Versions(server));
    }

    private void WriteResult(Tls13ConnectResult result)
    {
        Diagnostics.Act("succeeded", result.Succeeded);
        Diagnostics.Act("failure alert", result.Failure?.Alert.ToString() ?? "none");
        Diagnostics.Act("failure origin", result.Failure?.Origin.ToString() ?? "none");
    }
}
