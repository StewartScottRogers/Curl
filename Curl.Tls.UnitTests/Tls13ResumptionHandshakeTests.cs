using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Drives <see cref="Tls13ClientHandshake" /> through resumption against <see cref="Tls13TestServer" />
/// at the message level: when a session is offered and when it is not, when early data is
/// offered, and each ServerHello, EncryptedExtensions and NewSessionTicket a resuming client
/// must refuse (RFC 8446 sections 4.2.10, 4.2.11 and 4.6.1).
/// </summary>
[TestClass]
public sealed class Tls13ResumptionHandshakeTests
{
    private static readonly Tls13ClientSettings Settings = HandshakeDriver.DefaultSettings with
    {
        ExtensionOrder = [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.EarlyData, TlsExtensionType.PskKeyExchangeModes],
    };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ResumedHandshakeSkipsTheCertificateAndEndsEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        RecordingCertificateVerifier verifier = new();
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session), verifier);
        WriteSession(session);

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);
        WriteOutcome(output, client);
        Diagnostics.Act("certificates presented to the verifier", verifier.Presented.Count);

        Diagnostics.Assert("first flight level", TlsEncryptionLevel.EarlyData, output.BytesToSend[0].Level);
        Assert.IsNull(output.Failure);
        Assert.IsTrue(client.IsResumed);
        Assert.IsTrue(client.EarlyDataAccepted);
        Assert.IsEmpty(verifier.Presented);
        Assert.AreEqual(TlsEncryptionLevel.EarlyData, output.BytesToSend[0].Level);
    }

    [TestMethod]
    public void FirstHandshakeOffersPskKeyExchangeModesWithoutATicket()
    {
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings);
        Diagnostics.Arrange("session", "none");

        ClientHello hello = SentHello(client);
        WriteExtensions(hello);

        Diagnostics.Assert("pre_shared_key offered", false, Find(hello, TlsExtensionType.PreSharedKey) is not null);
        Assert.AreEqual(PskKeyExchangeModesExtension.PskDheKe, PskKeyExchangeModesExtension.Decode(Find(hello, TlsExtensionType.PskKeyExchangeModes)!).Value.Single());
        Assert.IsNull(Find(hello, TlsExtensionType.PreSharedKey));
        Assert.IsNull(Find(hello, TlsExtensionType.EarlyData));
    }

    [TestMethod]
    public void ResumingHelloEndsWithTheTicketAndOffersEarlyData()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache());
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);

        ClientHello hello = SentHello(client);
        WriteExtensions(hello);

        Diagnostics.Assert("last extension", TlsExtensionType.PreSharedKey, hello.Extensions[^1].Type);
        Assert.AreEqual(TlsExtensionType.PreSharedKey, hello.Extensions[^1].Type);
        CollectionAssert.AreEqual(session.Ticket, PreSharedKeyExtension.DecodeOffered(hello.Extensions[^1].Data).Value.Identities.Single().Identity);
        Assert.IsNotNull(Find(hello, TlsExtensionType.EarlyData));
    }

    [TestMethod]
    [DataRow(0xc02f, "localhost", DisplayName = "a TLS 1.2 suite")]
    [DataRow(0x1301, "elsewhere", DisplayName = "another host")]
    public void SessionTheClientCannotUseIsNotOffered(int cipherSuite, string serverName)
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache()) with { CipherSuite = (ushort)cipherSuite, ServerName = serverName };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);

        ClientHello hello = SentHello(client);
        WriteExtensions(hello);
        Diagnostics.Act("early data offered, max early data size", $"{client.EarlyDataOffered}, {client.MaxEarlyDataSize}");

        Diagnostics.Assert("pre_shared_key offered", false, Find(hello, TlsExtensionType.PreSharedKey) is not null);
        Assert.IsNull(Find(hello, TlsExtensionType.PreSharedKey));
        Assert.IsFalse(client.EarlyDataOffered);
        Assert.AreEqual(0u, client.MaxEarlyDataSize);
        Assert.IsNull(client.EarlyDataCipherSuite);
    }

    [TestMethod]
    public void SessionWhoseHashNoOfferedSuiteSharesIsNotOffered()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache());
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session) with { CipherSuites = [Tls13CipherSuite.Aes256GcmSha384.Code] });
        WriteSession(session);
        Diagnostics.Arrange("offered suites", "TLS_AES_256_GCM_SHA384 only");

        ClientHello hello = SentHello(client);
        WriteExtensions(hello);

        Diagnostics.Assert("pre_shared_key offered", false, Find(hello, TlsExtensionType.PreSharedKey) is not null);
        Assert.IsNull(Find(hello, TlsExtensionType.PreSharedKey));
    }

    [TestMethod]
    public void SessionOfATls12VersionIsNotOffered()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache()) with { Version = 0x0303 };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);

        ClientHello hello = SentHello(client);
        WriteExtensions(hello);

        Diagnostics.Assert("pre_shared_key offered", false, Find(hello, TlsExtensionType.PreSharedKey) is not null);
        Assert.IsNull(Find(hello, TlsExtensionType.PreSharedKey));
    }

    [TestMethod]
    [DataRow(false, 1024u, 0x1301, DisplayName = "early data not asked for")]
    [DataRow(true, 0u, 0x1301, DisplayName = "a ticket that allows none")]
    [DataRow(true, 1024u, 0x1303, DisplayName = "the ticket's suite not offered")]
    public void TicketIsOfferedWithoutEarlyData(bool offerEarlyData, uint maxEarlyDataSize, int offeredSuite)
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache()) with { MaxEarlyDataSize = maxEarlyDataSize };
        Tls13ClientSettings settings = Resuming(session) with { OfferEarlyData = offerEarlyData, CipherSuites = [(ushort)offeredSuite] };
        using Tls13ClientHandshake client = HandshakeDriver.Client(settings);
        Diagnostics.Arrange("offer early data, ticket max early data size, offered suite", $"{offerEarlyData}, {maxEarlyDataSize}, 0x{offeredSuite:x4}");

        Tls13HandshakeOutput output = client.Start();

        ClientHello hello = ClientHello.Decode(output.BytesToSend[0].Bytes[4..]).Value;
        WriteExtensions(hello);
        Diagnostics.Act("secrets installed", output.SecretsInstalled.Count);
        Diagnostics.Assert("early_data offered", false, Find(hello, TlsExtensionType.EarlyData) is not null);
        Assert.IsNotNull(Find(hello, TlsExtensionType.PreSharedKey));
        Assert.IsNull(Find(hello, TlsExtensionType.EarlyData));
        Assert.IsEmpty(output.SecretsInstalled);
        Assert.IsFalse(client.EarlyDataOffered);
    }

    [TestMethod]
    public void EarlyDataIsNotOfferedWhenTheSessionProtocolIsNotOffered()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache()) with { ApplicationProtocol = "h2" };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session) with { ApplicationProtocols = ["http/1.1"] });
        WriteSession(session);
        Diagnostics.Arrange("offered protocols", "http/1.1");

        ClientHello hello = SentHello(client);
        WriteExtensions(hello);

        Diagnostics.Assert("early_data offered", false, Find(hello, TlsExtensionType.EarlyData) is not null);
        Assert.IsNull(Find(hello, TlsExtensionType.EarlyData));
    }

    [TestMethod]
    public void EarlyDataIsAcceptedWithTheSessionProtocol()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets, ApplicationProtocol = "h2" }, ["h2", "http/1.1"]);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets, ApplicationProtocol = "h2" };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session) with { ApplicationProtocols = ["h2", "http/1.1"] });
        WriteSession(session);
        Diagnostics.Arrange("server protocol", "h2");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);
        WriteOutcome(output, client);

        Diagnostics.Assert("session protocol", "h2", session.ApplicationProtocol);
        Assert.IsNull(output.Failure);
        Assert.AreEqual("h2", session.ApplicationProtocol);
        Assert.IsTrue(client.EarlyDataAccepted);
    }

    [TestMethod]
    public void EarlyDataAcceptedWithAnotherProtocolIsIllegal()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets, ApplicationProtocol = "h2" }, ["h2", "http/1.1"]);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets, ApplicationProtocol = "http/1.1" };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session) with { ApplicationProtocols = ["h2", "http/1.1"] });
        WriteSession(session);
        Diagnostics.Arrange("server protocol", "http/1.1");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
    }

    [TestMethod]
    public void EarlyDataAcceptedWithAnotherSuiteIsIllegal()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets, CipherSuite = Tls13CipherSuite.ChaCha20Poly1305Sha256.Code };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);
        Diagnostics.Arrange("server suite", "TLS_CHACHA20_POLY1305_SHA256");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
        Assert.IsTrue(client.IsResumed);
        Assert.IsFalse(client.EarlyDataAccepted);
    }

    [TestMethod]
    public void EarlyDataAcceptedWithoutTheTicketIsIllegal()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets, AcceptResumption = false };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);
        Diagnostics.Arrange("server", "refuses resumption, EncryptedExtensions replaced with an early_data indication");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceFlight: flight => HandshakeDriver.Replace(
            flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([EarlyDataExtension.EncodeIndication()]).Encode()));
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
    }

    [TestMethod]
    public void EarlyDataIndicationWithDataIsADecodeError()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);
        Diagnostics.Arrange("EncryptedExtensions", "early_data indication carrying one byte 00");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceFlight: flight => HandshakeDriver.Replace(
            flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([new TlsExtension(TlsExtensionType.EarlyData, [0])]).Encode()));
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure!.Alert);
    }

    [TestMethod]
    public void CertificateInAResumedHandshakeIsUnexpected()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        byte[] certificate = new CertificateMessage([], [new CertificateEntry(TestServerCredential.Ed25519().Certificate, [])]).Encode();
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);
        Diagnostics.Arrange("certificate message inserted after EncryptedExtensions, length", certificate.Length);

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceFlight: flight => [flight[0], certificate, .. flight[1..]]);
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure!.Alert);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 1 }, TlsAlertDescription.IllegalParameter, DisplayName = "identity 1")]
    [DataRow(new byte[] { 0 }, TlsAlertDescription.DecodeError, DisplayName = "a truncated index")]
    public void ServerHelloSelectingAnythingButTheTicketIsRefused(byte[] selected, TlsAlertDescription alert)
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);
        Diagnostics.Bytes("selected identity", selected);
        Diagnostics.Arrange("expected alert", alert);

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceServerHello: hello => ReplacePreSharedKey(hello, selected));
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", alert, output.Failure?.Alert);
        Assert.AreEqual(alert, output.Failure!.Alert);
        Assert.IsFalse(client.IsResumed);
    }

    [TestMethod]
    public void ServerHelloResumingWithASuiteOfAnotherHashIsIllegal()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));
        WriteSession(session);
        Diagnostics.Arrange("ServerHello suite", "replaced with TLS_AES_256_GCM_SHA384");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceServerHello: hello =>
        {
            ServerHello decoded = ServerHello.Decode(hello[4..]).Value;
            return (decoded with { CipherSuite = Tls13CipherSuite.Aes256GcmSha384.Code }).Encode();
        });
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
    }

    [TestMethod]
    public void ServerHelloSelectingATicketNeverOfferedIsAnUnsupportedExtension()
    {
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings);
        Diagnostics.Arrange("session", "none; ServerHello gains pre_shared_key selecting identity 0");

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, new Tls13TestServer(TestServerCredential.Ed25519()), replaceServerHello: hello =>
        {
            ServerHello decoded = ServerHello.Decode(hello[4..]).Value;
            return (decoded with { Extensions = [.. decoded.Extensions, PreSharedKeyExtension.EncodeSelected(0)] }).Encode();
        });
        WriteOutcome(output, client);

        Diagnostics.Assert("alert", TlsAlertDescription.UnsupportedExtension, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnsupportedExtension, output.Failure!.Alert);
    }

    [TestMethod]
    public void NewSessionTicketWithABadEarlyDataLimitIsADecodeError()
    {
        Tls13TestTicketCache tickets = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings);
        HandshakeDriver.Run(client, server);
        byte[] ticket = server.IssueTicket([new TlsExtension(TlsExtensionType.EarlyData, [0, 0])]);
        Diagnostics.Arrange("ticket early_data extension", "two bytes 00 00");

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Application, ticket);
        WriteOutcome(output, client);
        Diagnostics.Act("sessions received", client.ReceivedSessions.Count);

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure!.Alert);
        Assert.IsEmpty(client.ReceivedSessions);
    }

    [TestMethod]
    public void NewSessionTicketWithoutEarlyDataRecordsASessionThatAllowsNone()
    {
        Tls13TestTicketCache tickets = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings);
        HandshakeDriver.Run(client, server);
        byte[] ticket = server.IssueTicket([]);
        Diagnostics.Arrange("ticket extensions", "none");

        client.Receive(TlsEncryptionLevel.Application, ticket);
        uint maxEarlyDataSize = client.ReceivedSessions.Single().MaxEarlyDataSize;
        Diagnostics.Act("max early data size", maxEarlyDataSize);

        Diagnostics.Assert("max early data size", 0u, maxEarlyDataSize);
        Assert.AreEqual(0u, client.ReceivedSessions.Single().MaxEarlyDataSize);
    }

    [TestMethod]
    public void ResumptionWithoutPskKeyExchangeModesInTheOrderIsRefused()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache());
        WriteSession(session);
        Diagnostics.Arrange("extension order", "default, without psk_key_exchange_modes");

        ArgumentException refused = Assert.ThrowsExactly<ArgumentException>(() =>
            HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { ResumptionSession = session }));
        Diagnostics.Act("parameter", refused.ParamName);

        Diagnostics.Assert("parameter", "ExtensionOrder", refused.ParamName);
        Assert.AreEqual("ExtensionOrder", refused.ParamName);
    }

    [TestMethod]
    public void EarlyDataWithoutItsPlaceInTheOrderIsRefused()
    {
        Diagnostics.Arrange("extension order", "default, without early_data, early data offered");

        ArgumentException refused = Assert.ThrowsExactly<ArgumentException>(() =>
            HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { OfferEarlyData = true }));
        Diagnostics.Act("parameter", refused.ParamName);

        Diagnostics.Assert("parameter", "ExtensionOrder", refused.ParamName);
        Assert.AreEqual("ExtensionOrder", refused.ParamName);
    }

    /// <summary>Runs a full handshake against a server that keeps its tickets in <paramref name="tickets" />, and returns the session of the ticket it then issues.</summary>
    private static TlsSessionRecord FirstSession(Tls13TestTicketCache tickets, Tls13TestServer? server = null, IReadOnlyList<string>? applicationProtocols = null)
    {
        server ??= new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings with { ApplicationProtocols = applicationProtocols ?? [] });
        Assert.IsNull(HandshakeDriver.Run(client, server).Failure);
        Assert.IsNull(client.Receive(TlsEncryptionLevel.Application, server.IssueTicket()).Failure);
        return client.ReceivedSessions.Single();
    }

    private static Tls13ClientSettings Resuming(TlsSessionRecord session) => Settings with { ResumptionSession = session, OfferEarlyData = true };

    private static ClientHello SentHello(Tls13ClientHandshake client) => ClientHello.Decode(client.Start().BytesToSend[0].Bytes[4..]).Value;

    private static byte[]? Find(ClientHello hello, TlsExtensionType type) => hello.Extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private static byte[] ReplacePreSharedKey(byte[] serverHello, byte[] selected)
    {
        ServerHello decoded = ServerHello.Decode(serverHello[4..]).Value;
        TlsExtension[] extensions = [.. decoded.Extensions.Select(extension => extension.Type == TlsExtensionType.PreSharedKey ? new TlsExtension(TlsExtensionType.PreSharedKey, selected) : extension)];
        return (decoded with { Extensions = extensions }).Encode();
    }

    /// <summary>Writes the session the client resumes as an ARRANGE line.</summary>
    private void WriteSession(TlsSessionRecord session) =>
        Diagnostics.Arrange("session version, suite, server name, protocol, max early data size", $"0x{session.Version:x4}, 0x{session.CipherSuite:x4}, {session.ServerName}, {session.ApplicationProtocol ?? "none"}, {session.MaxEarlyDataSize}");

    /// <summary>Writes the extension types of the ClientHello the client sent as an ACT line.</summary>
    private void WriteExtensions(ClientHello hello) =>
        Diagnostics.Act("ClientHello extensions", string.Join(", ", hello.Extensions.Select(extension => extension.Type)));

    /// <summary>Writes the handshake's failure and resumption state as an ACT line.</summary>
    private void WriteOutcome(Tls13HandshakeOutput output, Tls13ClientHandshake client) =>
        Diagnostics.Act("failure alert, resumed, early data accepted", $"{output.Failure?.Alert.ToString() ?? "none"}, {client.IsResumed}, {client.EarlyDataAccepted}");
}
