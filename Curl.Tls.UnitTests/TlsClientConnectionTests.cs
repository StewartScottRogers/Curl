using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Runs <see cref="TlsClientConnection" /> over a pipe: one ClientHello offering TLS 1.3 and
/// TLS 1.2, continued as TLS 1.3 against <see cref="Tls13RecordTestServer" /> and as TLS 1.2
/// against <see cref="Tls12RecordTestServer" />, the RFC 8446 section 4.1.3 downgrade
/// sentinels refused, and every first answer that is not a TLS 1.2 ServerHello judged by
/// the TLS 1.3 client.
/// </summary>
[TestClass]
public sealed class TlsClientConnectionTests
{
    private static readonly Tls12ClientSettings Tls12Offer = Tls12PipeDriver.DefaultSettings;

    private static readonly TlsClientSettings Settings = new(Tls13PipeDriver.DefaultSettings, Tls12Offer);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ClientHelloOffersBothVersionsTheirSuitesSchemesAndTls12Extensions()
    {
        (Task<TlsConnectResult> pending, Stream serverEnd) = Start(Settings);
        Diagnostics.Arrange("settings", "default TLS 1.3 and TLS 1.2 offers; the server reads the ClientHello and closes");

        ClientHello hello = await ReadClientHelloAsync(serverEnd);
        await serverEnd.DisposeAsync();
        TlsConnectResult result = await pending;
        Diagnostics.Act("client hello", $"legacy version 0x{hello.LegacyVersion:x4}, {hello.CipherSuites.Count} suites, {hello.Extensions.Count} extensions");

        Diagnostics.Assert("legacy version", (ushort)0x0303, hello.LegacyVersion);
        Diagnostics.Assert("extension order", "ServerName, SupportedGroups, SignatureAlgorithms, SupportedVersions, KeyShare, RenegotiationInfo, EcPointFormats, SessionTicket, EncryptThenMac, ExtendedMasterSecret", string.Join(", ", hello.Extensions.Select(extension => extension.Type)));
        Diagnostics.Assert("failure origin", TlsHandshakeFailureOrigin.TransportClosed, result.Failure?.Origin);
        Assert.AreEqual((ushort)0x0303, hello.LegacyVersion);
        CollectionAssert.AreEqual(
            new ushort[] { 0x0304, 0x0303, 0x0302, 0x0301 },
            SupportedVersionsExtension.DecodeOffered(Find(hello, TlsExtensionType.SupportedVersions)).Value.ToArray());
        CollectionAssert.AreEqual(
            Tls13PipeDriver.DefaultSettings.CipherSuites.Concat(Tls12Offer.OfferedCipherSuites).ToArray(),
            hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(
            Tls13PipeDriver.DefaultSettings.SignatureAlgorithms.Union(Tls12Offer.SignatureAlgorithms).ToArray(),
            SignatureAlgorithmsExtension.Decode(Find(hello, TlsExtensionType.SignatureAlgorithms)).Value.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                TlsExtensionType.ServerName, TlsExtensionType.SupportedGroups, TlsExtensionType.SignatureAlgorithms, TlsExtensionType.SupportedVersions,
                TlsExtensionType.KeyShare, TlsExtensionType.RenegotiationInfo, TlsExtensionType.EcPointFormats, TlsExtensionType.SessionTicket,
                TlsExtensionType.EncryptThenMac, TlsExtensionType.ExtendedMasterSecret,
            },
            hello.Extensions.Select(extension => extension.Type).ToArray());
        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
    }

    [TestMethod]
    public async Task ClientHelloSendsAFixedExtensionInPlaceOfTheTls12One()
    {
        TlsExtension fixedPointFormats = new(TlsExtensionType.EcPointFormats, [2, 0, 1]);
        TlsClientSettings settings = Settings with { Tls13 = Tls13PipeDriver.DefaultSettings with { FixedExtensions = [fixedPointFormats] } };
        (Task<TlsConnectResult> pending, Stream serverEnd) = Start(settings);
        Diagnostics.Arrange("fixed extension", "ec_point_formats 020001 in the TLS 1.3 settings");
        Diagnostics.Bytes("fixed ec_point_formats data", fixedPointFormats.Data);

        ClientHello hello = await ReadClientHelloAsync(serverEnd);
        await serverEnd.DisposeAsync();
        await pending;
        TlsExtension pointFormats = hello.Extensions.Single(extension => extension.Type == TlsExtensionType.EcPointFormats);
        Diagnostics.Act("last extension", hello.Extensions[^1].Type);

        Diagnostics.Diff("ec_point_formats data", fixedPointFormats.Data, pointFormats.Data);
        CollectionAssert.AreEqual(fixedPointFormats.Data, pointFormats.Data);
        Assert.AreSame(hello.Extensions[^1], pointFormats);
    }

    [TestMethod]
    public async Task Tls13ServerContinuesAsTls13AndExchangesData()
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, new Tls13TestServer(TestServerCredential.Ed25519()));
        Diagnostics.Arrange("server", "TLS 1.3 record test server, Ed25519 credential");

        TlsConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            Task serverHandshake = server.HandshakeAsync();
            result = await ConnectAsync(clientEnd, Settings);
            await serverHandshake;
        }

        await using Tls13ClientStream stream = result.Tls13Stream!;
        byte[] up;
        byte[] down;
        using (Diagnostics.Phase("exchange"))
        {
            await stream.WriteAsync("up"u8.ToArray());
            up = await server.ReceiveApplicationDataAsync(2);
            await server.SendAsync(TlsContentType.ApplicationData, "down"u8.ToArray());
            down = await Tls13PipeDriver.ReadAsync(stream, 4);
        }

        Diagnostics.Act("succeeded", result.Succeeded);

        Diagnostics.Assert("tls 1.2 stream", null, result.Tls12Stream);
        Diagnostics.Diff("data up", "up"u8.ToArray(), up);
        Diagnostics.Diff("data down", "down"u8.ToArray(), down);
        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Tls12Stream);
        Assert.IsTrue(stream.Handshake.IsComplete);
        CollectionAssert.AreEqual("up"u8.ToArray(), up);
        CollectionAssert.AreEqual("down"u8.ToArray(), down);
    }

    [TestMethod]
    [DataRow(Tls12RecordWriteState.MaximumFragmentLength)]
    [DataRow(7)]
    public async Task Tls12ServerContinuesAsTls12AndExchangesData(int handshakeRecordLength)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls12RecordTestServer server = new(serverEnd, new Tls12TestServer(TestServerCredential.Ed25519())) { HandshakeRecordLength = handshakeRecordLength };
        Diagnostics.Arrange("handshake record length", handshakeRecordLength);

        TlsConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            Task serverHandshake = server.HandshakeAsync();
            result = await ConnectAsync(clientEnd, Settings);
            await serverHandshake;
        }

        await using Tls12ClientStream stream = result.Tls12Stream!;
        byte[] up;
        byte[] down = new byte[4];
        using (Diagnostics.Phase("exchange"))
        {
            await stream.WriteAsync("up"u8.ToArray());
            up = await server.ReceiveApplicationDataAsync(2);
            await server.SendAsync(TlsContentType.ApplicationData, "down"u8.ToArray());
            await stream.ReadExactlyAsync(down);
        }

        Diagnostics.Act("succeeded", result.Succeeded);

        Diagnostics.Assert("version", TlsProtocolVersion.Tls12, stream.Handshake.Version);
        Diagnostics.Assert("extended master secret", true, stream.Handshake.ExtendedMasterSecret);
        Diagnostics.Diff("data up", "up"u8.ToArray(), up);
        Diagnostics.Diff("data down", "down"u8.ToArray(), down);
        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Tls13Stream);
        Assert.AreEqual(TlsProtocolVersion.Tls12, stream.Handshake.Version);
        Assert.IsTrue(stream.Handshake.ExtendedMasterSecret);
        CollectionAssert.AreEqual("up"u8.ToArray(), up);
        CollectionAssert.AreEqual("down"u8.ToArray(), down);
    }

    [TestMethod]
    public async Task ConnectWithEarlyDataToATls12ServerSendsTheDataAfterTheHandshake()
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls12RecordTestServer server = new(serverEnd, new Tls12TestServer(TestServerCredential.Ed25519()));
        Diagnostics.Arrange("early data", "early, to a TLS 1.2 server");

        TlsConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            Task serverHandshake = server.HandshakeAsync();
            result = await TlsClientConnection.ConnectWithEarlyDataAsync(
                clientEnd, Settings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), "early"u8.ToArray(), CancellationToken.None);
            await serverHandshake;
        }

        byte[] received = await server.ReceiveApplicationDataAsync(5);
        Diagnostics.Act("succeeded", result.Succeeded);

        Diagnostics.Diff("data after the handshake", "early"u8.ToArray(), received);
        Assert.IsNotNull(result.Tls12Stream);
        CollectionAssert.AreEqual("early"u8.ToArray(), received);
    }

    [TestMethod]
    public async Task ConnectWithEarlyDataResumingATls13SessionSendsItAsEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session;
        using (Diagnostics.Phase("first session"))
        {
            session = await Tls13ResumptionConnectionTests.FirstSessionAsync(tickets);
        }

        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets });
        TlsClientSettings settings = Settings with
        {
            Tls13 = Tls13PipeDriver.DefaultSettings with
            {
                ExtensionOrder = [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.EarlyData, TlsExtensionType.PskKeyExchangeModes],
                ResumptionSession = session,
                OfferEarlyData = true,
            },
        };
        Diagnostics.Arrange("resumption", "TLS 1.3 session from a first handshake, early data offered");

        TlsConnectResult result;
        using (Diagnostics.Phase("resumed handshake"))
        {
            Task serverHandshake = server.HandshakeAsync();
            result = await TlsClientConnection.ConnectWithEarlyDataAsync(
                clientEnd, settings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), "early"u8.ToArray(), CancellationToken.None);
            await serverHandshake;
        }

        Diagnostics.Act("early data accepted", result.Tls13Stream?.Handshake.EarlyDataAccepted);

        Diagnostics.Diff("early data", "early"u8.ToArray(), server.EarlyData.ToArray());
        Assert.IsTrue(result.Tls13Stream!.Handshake.EarlyDataAccepted);
        CollectionAssert.AreEqual("early"u8.ToArray(), server.EarlyData.ToArray());
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, false, true)]
    [DataRow(TlsProtocolVersion.Tls11, true, false)]
    public async Task Tls12ServerHelloCarryingADowngradeSentinelFailsWithIllegalParameter(TlsProtocolVersion version, bool tls11Sentinel, bool tls12Sentinel)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls12TestServer testServer = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256))
        {
            Version = version,
            CipherSuite = 0xc009,
            SendDowngradeSentinel = tls11Sentinel,
            SendTls12DowngradeSentinel = tls12Sentinel,
        };
        Tls12RecordTestServer server = new(serverEnd, testServer);
        Diagnostics.Arrange("server", $"version {version}, TLS 1.1 sentinel {tls11Sentinel}, TLS 1.2 sentinel {tls12Sentinel}");
        Task<TlsConnectResult> pending = ConnectAsync(clientEnd, Settings);
        await server.SendFlightAsync(await server.AnswerClientHelloAsync());

        TlsConnectResult result = await pending;
        Diagnostics.Act("failure", $"{result.Failure?.Alert}, {result.Failure?.Origin}");

        TlsAlertDescription received = await Tls12PipeDriver.ReceiveFatalAlertAsync(server);
        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, result.Failure?.Alert);
        Diagnostics.Assert("alert the server received", TlsAlertDescription.IllegalParameter, received);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Tls12Stream);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, received);
    }

    [TestMethod]
    public async Task AlertInPlaceOfTheServerHelloEndsTheHandshakeAsReceived()
    {
        (Task<TlsConnectResult> pending, Stream serverEnd) = Start(Settings);
        await ReadClientHelloAsync(serverEnd);
        byte[] alert = Record(TlsContentType.Alert, [2, (byte)TlsAlertDescription.HandshakeFailure]);
        Diagnostics.Arrange("server answer", "fatal handshake_failure alert record");
        Diagnostics.Bytes("alert record", alert);

        await serverEnd.WriteAsync(alert);
        TlsConnectResult result = await pending;
        Diagnostics.Act("failure", $"{result.Failure?.Alert}, {result.Failure?.Origin}");

        Diagnostics.Assert("alert", TlsAlertDescription.HandshakeFailure, result.Failure?.Alert);
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.AlertReceived, result.Failure?.Origin);
        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertReceived, result.Failure.Origin);
        Assert.IsNull(result.Tls13Stream);
    }

    [TestMethod]
    [DataRow(new byte[] { 2, 0, 0, 1, 0 }, TlsAlertDescription.DecodeError)]
    [DataRow(new byte[] { 11, 0, 0, 4, 0, 0, 0, 0 }, TlsAlertDescription.UnexpectedMessage)]
    [DataRow(new byte[] { 99, 0, 0, 0 }, TlsAlertDescription.UnexpectedMessage)]
    public async Task FirstMessageThatIsNoTls12ServerHelloFailsAsTheTls13ClientJudgesIt(byte[] message, TlsAlertDescription expected)
    {
        (Task<TlsConnectResult> pending, Stream serverEnd) = Start(Settings);
        await ReadClientHelloAsync(serverEnd);
        Diagnostics.Bytes("first handshake message", message);
        Diagnostics.Arrange("expected alert", expected);

        await serverEnd.WriteAsync(Record(TlsContentType.Handshake, message));
        TlsConnectResult result = await pending;
        Diagnostics.Act("failure", $"{result.Failure?.Alert}, {result.Failure?.Origin}");

        Diagnostics.Assert("alert", expected, result.Failure?.Alert);
        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.AlertSent, result.Failure?.Origin);
        Assert.AreEqual(expected, result.Failure!.Alert);
        Assert.AreEqual(TlsHandshakeFailureOrigin.AlertSent, result.Failure.Origin);
    }

    [TestMethod]
    public async Task RecordTooLongForPlaintextFailsWithRecordOverflow()
    {
        (Task<TlsConnectResult> pending, Stream serverEnd) = Start(Settings);
        await ReadClientHelloAsync(serverEnd);
        Diagnostics.Arrange("record content length", Tls13RecordProtection.MaximumPlaintextLength + 1);

        await serverEnd.WriteAsync(Record(TlsContentType.Handshake, new byte[Tls13RecordProtection.MaximumPlaintextLength + 1]));
        TlsConnectResult result = await pending;
        Diagnostics.Act("failure alert", result.Failure?.Alert);

        Diagnostics.Assert("alert", TlsAlertDescription.RecordOverflow, result.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.RecordOverflow, result.Failure!.Alert);
    }

    [TestMethod]
    public async Task TransportClosingInsideTheFirstRecordEndsTheHandshake()
    {
        (Task<TlsConnectResult> pending, Stream serverEnd) = Start(Settings);
        await ReadClientHelloAsync(serverEnd);
        byte[] partial = Record(TlsContentType.Handshake, [2, 0, 0, 40])[..7];
        Diagnostics.Arrange("server answer", "first 7 bytes of a ServerHello record, then close");
        Diagnostics.Bytes("partial record", partial);

        await serverEnd.WriteAsync(partial);
        await serverEnd.DisposeAsync();
        TlsConnectResult result = await pending;
        Diagnostics.Act("failure origin", result.Failure?.Origin);

        Diagnostics.Assert("origin", TlsHandshakeFailureOrigin.TransportClosed, result.Failure?.Origin);
        Assert.AreEqual(TlsHandshakeFailureOrigin.TransportClosed, result.Failure!.Origin);
    }

    [TestMethod]
    public async Task InvalidArgumentsAreRefused()
    {
        (Stream clientEnd, _) = InMemoryPipe.Create();
        Tls12Session session = new(TlsProtocolVersion.Tls12, 0xc02b, [1], null, 0, new byte[48], true);
        Diagnostics.Arrange("cases", "null stream, null settings, null TLS 1.3 or 1.2 settings, TLS 1.2 maximum below 1.2, a session to resume, no cipher suites");

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ConnectAsync(null!, Settings));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ConnectAsync(clientEnd, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ConnectAsync(clientEnd, Settings with { Tls13 = null! }));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ConnectAsync(clientEnd, Settings with { Tls12 = null! }));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => ConnectAsync(clientEnd, Settings with { Tls12 = Tls12Offer with { MaximumVersion = TlsProtocolVersion.Tls11 } }));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => ConnectAsync(clientEnd, Settings with { Tls12 = Tls12Offer with { SessionToResume = session } }));
        ArgumentException last = await Assert.ThrowsExactlyAsync<ArgumentException>(() => ConnectAsync(clientEnd, Settings with { Tls12 = Tls12Offer with { CipherSuites = [] } }));
        Diagnostics.Act("last refusal", last.GetType().Name);

        Diagnostics.Assert("last refusal type", nameof(ArgumentException), last.GetType().Name);
    }

    [TestMethod]
    public void StartingATls12HandshakeFromASentHelloTwiceThrows()
    {
        Tls12ClientHandshake handshake = new(Tls12Offer, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier());
        handshake.Start();
        Diagnostics.Arrange("handshake", "TLS 1.2 client handshake already started");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => handshake.StartFrom(null!));
        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(InvalidOperationException), exception.GetType().Name);
    }

    private static Task<TlsConnectResult> ConnectAsync(Stream clientEnd, TlsClientSettings settings) =>
        TlsClientConnection.ConnectAsync(clientEnd, settings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None);

    private static (Task<TlsConnectResult> Result, Stream ServerEnd) Start(TlsClientSettings settings)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        return (ConnectAsync(clientEnd, settings), serverEnd);
    }

    private static async Task<ClientHello> ReadClientHelloAsync(Stream serverEnd)
    {
        byte[] header = new byte[5];
        await serverEnd.ReadExactlyAsync(header);
        byte[] fragment = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        await serverEnd.ReadExactlyAsync(fragment);
        return ClientHello.Decode(HandshakeMessageReader.Read(fragment).Message!.Body).Value;
    }

    private static byte[] Find(ClientHello hello, TlsExtensionType type) => hello.Extensions.Single(extension => extension.Type == type).Data;

    private static byte[] Record(TlsContentType type, byte[] content)
    {
        byte[] record = new byte[5 + content.Length];
        record[0] = (byte)type;
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(1), 0x0303);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(3), (ushort)content.Length);
        content.CopyTo(record, 5);
        return record;
    }
}
