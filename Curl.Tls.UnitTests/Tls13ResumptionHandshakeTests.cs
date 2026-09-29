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

    [TestMethod]
    public void ResumedHandshakeSkipsTheCertificateAndEndsEarlyData()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        RecordingCertificateVerifier verifier = new();
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session), verifier);

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);

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

        ClientHello hello = SentHello(client);

        Assert.AreEqual(PskKeyExchangeModesExtension.PskDheKe, PskKeyExchangeModesExtension.Decode(Find(hello, TlsExtensionType.PskKeyExchangeModes)!).Value.Single());
        Assert.IsNull(Find(hello, TlsExtensionType.PreSharedKey));
        Assert.IsNull(Find(hello, TlsExtensionType.EarlyData));
    }

    [TestMethod]
    public void ResumingHelloEndsWithTheTicketAndOffersEarlyData()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache());
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));

        ClientHello hello = SentHello(client);

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

        ClientHello hello = SentHello(client);

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

        Assert.IsNull(Find(SentHello(client), TlsExtensionType.PreSharedKey));
    }

    [TestMethod]
    public void SessionOfATls12VersionIsNotOffered()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache()) with { Version = 0x0303 };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));

        Assert.IsNull(Find(SentHello(client), TlsExtensionType.PreSharedKey));
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

        Tls13HandshakeOutput output = client.Start();

        ClientHello hello = ClientHello.Decode(output.BytesToSend[0].Bytes[4..]).Value;
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

        Assert.IsNull(Find(SentHello(client), TlsExtensionType.EarlyData));
    }

    [TestMethod]
    public void EarlyDataIsAcceptedWithTheSessionProtocol()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets, new Tls13TestServer(TestServerCredential.Ed25519()) { Tickets = tickets, ApplicationProtocol = "h2" }, ["h2", "http/1.1"]);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets, ApplicationProtocol = "h2" };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session) with { ApplicationProtocols = ["h2", "http/1.1"] });

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);

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

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, HandshakeDriver.Run(client, server).Failure!.Alert);
    }

    [TestMethod]
    public void EarlyDataAcceptedWithAnotherSuiteIsIllegal()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets, CipherSuite = Tls13CipherSuite.ChaCha20Poly1305Sha256.Code };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);

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

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceFlight: flight => HandshakeDriver.Replace(
            flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([EarlyDataExtension.EncodeIndication()]).Encode()));

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
    }

    [TestMethod]
    public void EarlyDataIndicationWithDataIsADecodeError()
    {
        Tls13TestTicketCache tickets = new();
        TlsSessionRecord session = FirstSession(tickets);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Resuming(session));

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceFlight: flight => HandshakeDriver.Replace(
            flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([new TlsExtension(TlsExtensionType.EarlyData, [0])]).Encode()));

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

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceFlight: flight => [flight[0], certificate, .. flight[1..]]);

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

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceServerHello: hello => ReplacePreSharedKey(hello, selected));

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

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server, replaceServerHello: hello =>
        {
            ServerHello decoded = ServerHello.Decode(hello[4..]).Value;
            return (decoded with { CipherSuite = Tls13CipherSuite.Aes256GcmSha384.Code }).Encode();
        });

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
    }

    [TestMethod]
    public void ServerHelloSelectingATicketNeverOfferedIsAnUnsupportedExtension()
    {
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings);

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, new Tls13TestServer(TestServerCredential.Ed25519()), replaceServerHello: hello =>
        {
            ServerHello decoded = ServerHello.Decode(hello[4..]).Value;
            return (decoded with { Extensions = [.. decoded.Extensions, PreSharedKeyExtension.EncodeSelected(0)] }).Encode();
        });

        Assert.AreEqual(TlsAlertDescription.UnsupportedExtension, output.Failure!.Alert);
    }

    [TestMethod]
    public void NewSessionTicketWithABadEarlyDataLimitIsADecodeError()
    {
        Tls13TestTicketCache tickets = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Tickets = tickets };
        using Tls13ClientHandshake client = HandshakeDriver.Client(Settings);
        HandshakeDriver.Run(client, server);

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Application, server.IssueTicket([new TlsExtension(TlsExtensionType.EarlyData, [0, 0])]));

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

        client.Receive(TlsEncryptionLevel.Application, server.IssueTicket([]));

        Assert.AreEqual(0u, client.ReceivedSessions.Single().MaxEarlyDataSize);
    }

    [TestMethod]
    public void ResumptionWithoutPskKeyExchangeModesInTheOrderIsRefused()
    {
        TlsSessionRecord session = FirstSession(new Tls13TestTicketCache());

        ArgumentException refused = Assert.ThrowsExactly<ArgumentException>(() =>
            HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { ResumptionSession = session }));

        Assert.AreEqual("ExtensionOrder", refused.ParamName);
    }

    [TestMethod]
    public void EarlyDataWithoutItsPlaceInTheOrderIsRefused()
    {
        ArgumentException refused = Assert.ThrowsExactly<ArgumentException>(() =>
            HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { OfferEarlyData = true }));

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
}
