using System.Globalization;
using Curl.Testing;
using static Curl.Tls.HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// Every way a server's messages fail the TLS 1.3 client handshake, each with the typed
/// alert RFC 8446 prescribes: out-of-order and mis-levelled messages, malformed messages,
/// a downgrade, a bad HelloRetryRequest, an unsupported group, extensions the client did
/// not offer, a rejected chain, a bad CertificateVerify and a bad Finished.
/// </summary>
[TestClass]
public sealed class Tls13ClientHandshakeFailureTests
{
    private static readonly byte[] ServerRandom = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ConstructorRejectsNullArguments()
    {
        Diagnostics.Arrange("arguments", "settings, random source and verifier, each null in turn");

        ArgumentNullException settings = Assert.ThrowsExactly<ArgumentNullException>(() => new Tls13ClientHandshake(null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier()));
        ArgumentNullException random = Assert.ThrowsExactly<ArgumentNullException>(() => new Tls13ClientHandshake(DefaultSettings, null!, new RecordingCertificateVerifier()));
        ArgumentNullException verifier = Assert.ThrowsExactly<ArgumentNullException>(() => new Tls13ClientHandshake(DefaultSettings, SystemTlsRandomSource.Instance, null!));

        Diagnostics.Act("parameters named", string.Join(", ", settings.ParamName, random.ParamName, verifier.ParamName));
        Diagnostics.Assert("ArgumentNullExceptions thrown", 3, 3);
    }

    [TestMethod]
    public void ConstructorRejectsSettingsThatCannotDriveAHandshake()
    {
        Diagnostics.Arrange("settings", "no suites; only TLS 1.2 suite 0xc02f; unknown group 0x0016; no share-capable group; Brainpool key share");

        ArgumentException[] thrown =
        [
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [0xc02f] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519, 0x0016] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.Secp256r1] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.BrainpoolP256r1, TlsNamedGroup.X25519], KeyShareGroups = [TlsNamedGroup.BrainpoolP256r1] })),
        ];

        Diagnostics.Act("exceptions", string.Join(", ", thrown.Select(exception => exception.GetType().Name)));
        Diagnostics.Assert("ArgumentExceptions thrown", 5, thrown.Length);
    }

    [TestMethod]
    public void StartingTwiceThrows()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("client", "started once");

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(() => client.Start());

        Diagnostics.Act("second Start threw", thrown.GetType().Name);
        Diagnostics.Assert("exception", nameof(InvalidOperationException), thrown.GetType().Name);
    }

    [TestMethod]
    public void ReceivingBeforeStartingThrows()
    {
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("client", "not started; receives [01] at the Initial level");

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(() => client.Receive(TlsEncryptionLevel.Initial, [1]));

        Diagnostics.Act("Receive threw", thrown.GetType().Name);
        Diagnostics.Assert("exception", nameof(InvalidOperationException), thrown.GetType().Name);
    }

    [TestMethod]
    public void DisposingBeforeStartingReleasesNothing()
    {
        Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("client", "not started");

        client.Dispose();

        Diagnostics.Act("IsComplete after Dispose", client.IsComplete);
        Diagnostics.Assert("IsComplete", false, client.IsComplete);
        Assert.IsFalse(client.IsComplete);
    }

    [TestMethod]
    public void BytesAtTheWrongLevelAreUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("failing flight", "server's first bytes, [01] at the Handshake level before any ServerHello");

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Handshake, [1]));
    }

    [TestMethod]
    public void AnUnknownHandshakeTypeIsUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("failing flight", "server's first message, handshake type 3, at the Initial level");

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, [3, 0, 0, 0]));
    }

    [TestMethod]
    public void AfterAFailureEveryCallReturnsTheSameFailureAndNothingElse()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        TlsHandshakeFailure failure = client.Receive(TlsEncryptionLevel.Handshake, [1]).Failure!;
        Diagnostics.Arrange("first failure", failure.Alert);
        Diagnostics.Arrange("then received", "a ServerHello-typed message at the Initial level");

        Tls13HandshakeOutput again = client.Receive(TlsEncryptionLevel.Initial, [2, 0, 0, 0]);

        Diagnostics.Act("second failure alert", again.Failure?.Alert);
        Diagnostics.Assert("same failure returned", true, ReferenceEquals(failure, again.Failure));
        Diagnostics.Assert("same failure on the client", true, ReferenceEquals(failure, client.Failure));
        Diagnostics.Assert("bytes to send", 0, again.BytesToSend.Count);
        Diagnostics.Assert("secrets installed", 0, again.SecretsInstalled.Count);
        Assert.AreSame(failure, again.Failure);
        Assert.AreSame(failure, client.Failure);
        Assert.IsEmpty(again.BytesToSend);
        Assert.IsEmpty(again.SecretsInstalled);
    }

    [TestMethod]
    public void AMessageAfterTheServerHelloAtTheInitialLevelIsUnexpected()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        TestServerFlight flight = server.Answer(client.Start().BytesToSend[0].Bytes);
        Diagnostics.Arrange("failing flight", "ServerHello followed by the first encrypted message, both at the Initial level");
        Diagnostics.Arrange("trailing message type", (HandshakeType)flight.HandshakeMessages[0][0]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, [.. flight.ServerHello, .. flight.HandshakeMessages[0]]));
    }

    [TestMethod]
    public void AnythingButAServerHelloFirstIsUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        byte[] message = new EncryptedExtensions([]).Encode();
        Diagnostics.Arrange("failing flight", "server's first message, EncryptedExtensions, at the Initial level");
        Diagnostics.Bytes("message", message);

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, message));
    }

    [TestMethod]
    [DataRow(HandshakeType.Certificate, 0)]
    [DataRow(HandshakeType.Finished, 1)]
    [DataRow(HandshakeType.Finished, 2)]
    [DataRow(HandshakeType.Certificate, 3)]
    public void AMessageOutOfOrderInTheEncryptedFlightIsUnexpected(HandshakeType wrongType, int position)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("failing flight", "encrypted server flight");
        Diagnostics.Arrange("out-of-order message", string.Create(CultureInfo.InvariantCulture, $"{wrongType} at position {position}"));

        Tls13HandshakeOutput output = RunTimed(client, server, replaceFlight: flight =>
        {
            List<byte[]> prefix = flight.Take(position).ToList();
            prefix.Add(flight.First(message => (HandshakeType)message[0] == wrongType));
            return prefix;
        });

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AnythingButACertificateAfterACertificateRequestIsUnexpected()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true };
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("failing flight", "encrypted server flight with a CertificateRequest");
        Diagnostics.Arrange("flight sent", "messages 0, 1 and 3 (the Certificate dropped)");

        Tls13HandshakeOutput output = RunTimed(client, server, replaceFlight: flight => [flight[0], flight[1], flight[3]]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AnythingButANewSessionTicketAfterTheHandshakeIsUnexpected()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        RunTimed(client, server);
        byte[] message = new Finished(new byte[32]).Encode();
        Diagnostics.Arrange("failing flight", "post-handshake, a Finished at the Application level");
        Diagnostics.Bytes("message", message);

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Application, message));
    }

    [TestMethod]
    public void AMalformedNewSessionTicketIsADecodeError()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        RunTimed(client, server);
        byte[] message = new HandshakeMessage(HandshakeType.NewSessionTicket, [0]).Encode();
        Diagnostics.Arrange("failing flight", "post-handshake, a one-byte NewSessionTicket at the Application level");
        Diagnostics.Bytes("message", message);

        AssertFails(TlsAlertDescription.DecodeError, client.Receive(TlsEncryptionLevel.Application, message));
    }

    [TestMethod]
    public void AMalformedServerHelloIsADecodeError() =>
        AssertServerHelloFails(TlsAlertDescription.DecodeError, new HandshakeMessage(HandshakeType.ServerHello, [3, 3]).Encode());

    [TestMethod]
    public void AServerHelloWithoutSupportedVersionsIsTheWrongProtocolVersion() =>
        AssertServerHelloFails(TlsAlertDescription.ProtocolVersion, ServerHelloBytes([]));

    [TestMethod]
    [DataRow((byte)0x01)]
    [DataRow((byte)0x00)]
    public void ATls12ServerHelloWithTheDowngradeSentinelIsAnIllegalParameter(byte sentinelVersion)
    {
        byte[] random = [.. ServerRandom[..24], .. "DOWNGRD"u8, sentinelVersion];
        Diagnostics.Bytes("server random", random);

        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, ServerHelloBytes([], random: random));
    }

    [TestMethod]
    public void ATls12ServerHelloWithoutAValidSentinelIsTheWrongProtocolVersion()
    {
        byte[] random = [.. ServerRandom[..24], .. "DOWNGRD"u8, 0x02];
        Diagnostics.Bytes("server random", random);

        AssertServerHelloFails(TlsAlertDescription.ProtocolVersion, ServerHelloBytes([], random: random));
    }

    [TestMethod]
    public void AMalformedSupportedVersionsIsADecodeError() =>
        AssertServerHelloFails(TlsAlertDescription.DecodeError, ServerHelloBytes([new TlsExtension(TlsExtensionType.SupportedVersions, [3])]));

    [TestMethod]
    public void SelectingTls12InSupportedVersionsIsAnIllegalParameter() =>
        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, ServerHelloBytes([SupportedVersionsExtension.EncodeSelected(0x0303)]));

    [TestMethod]
    public void AServerHelloEchoingAnotherSessionIdIsAnIllegalParameter() =>
        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, ServerHelloBytes([Tls13()], sessionIdEcho: [1]));

    [TestMethod]
    public void AServerHelloWithACompressionMethodIsAnIllegalParameter() =>
        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, ServerHelloBytes([Tls13()], compression: 1));

    [TestMethod]
    public void AServerHelloChoosingASuiteNotOfferedIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301] });
        client.Start();
        Diagnostics.Arrange("offered suites", "0x1301");
        Diagnostics.Arrange("failing flight", "ServerHello choosing 0x1302");

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13()], cipherSuite: 0x1302)));
    }

    [TestMethod]
    public void AServerHelloChoosingTheOfferedRenegotiationScsvIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301], OfferEmptyRenegotiationInfoScsv = true });
        client.Start();
        Diagnostics.Arrange("offered suites", "0x1301 and TLS_EMPTY_RENEGOTIATION_INFO_SCSV");
        Diagnostics.Arrange("failing flight", "ServerHello choosing TLS_EMPTY_RENEGOTIATION_INFO_SCSV");

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13()], cipherSuite: Tls12CipherSuite.EmptyRenegotiationInfoScsv)));
    }

    [TestMethod]
    public void AServerHelloChangingTheSuiteAfterAHelloRetryRequestIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301, 0x1302] });
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), KeyShareExtension.EncodeSelectedGroup(TlsNamedGroup.Secp256r1)]));
        Diagnostics.Arrange("HelloRetryRequest", "suite 0x1301, group secp256r1");
        Diagnostics.Arrange("failing flight", "second ServerHello choosing 0x1302");

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13()], cipherSuite: 0x1302)));
    }

    [TestMethod]
    public void ASecondHelloRetryRequestIsUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), CookieExtension.Encode([1])]));
        Diagnostics.Arrange("first HelloRetryRequest", "cookie [01]");
        Diagnostics.Arrange("failing flight", "second HelloRetryRequest, cookie [02]");

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), CookieExtension.Encode([2])])));
    }

    [TestMethod]
    public void AHelloRetryRequestWithAnotherExtensionIsAnUnsupportedExtension() =>
        AssertServerHelloFails(TlsAlertDescription.UnsupportedExtension, RetryBytes([Tls13(), CookieExtension.Encode([1]), ServerNameExtension.EncodeAcknowledgement()]));

    [TestMethod]
    public void AHelloRetryRequestThatChangesNothingIsAnIllegalParameter() =>
        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, RetryBytes([Tls13()]));

    [TestMethod]
    public void AHelloRetryRequestWithAMalformedKeyShareIsADecodeError() =>
        AssertServerHelloFails(TlsAlertDescription.DecodeError, RetryBytes([Tls13(), new TlsExtension(TlsExtensionType.KeyShare, [0, 23, 0])]));

    [TestMethod]
    public void AHelloRetryRequestWithAMalformedCookieIsADecodeError() =>
        AssertServerHelloFails(TlsAlertDescription.DecodeError, RetryBytes([Tls13(), new TlsExtension(TlsExtensionType.Cookie, [0, 5, 1])]));

    [TestMethod]
    public void AHelloRetryRequestForAGroupNotOfferedIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] });
        client.Start();
        Diagnostics.Arrange("offered groups", "x25519, secp256r1");
        Diagnostics.Arrange("failing flight", "HelloRetryRequest selecting secp384r1");

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), KeyShareExtension.EncodeSelectedGroup(TlsNamedGroup.Secp384r1)])));
    }

    [TestMethod]
    public void AHelloRetryRequestForATls12OnlyGroupOfferedIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.BrainpoolP256r1, TlsNamedGroup.X25519] });
        client.Start();
        Diagnostics.Arrange("offered groups", "brainpoolP256r1 (TLS 1.2 only), x25519");
        Diagnostics.Arrange("failing flight", "HelloRetryRequest selecting brainpoolP256r1");

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), KeyShareExtension.EncodeSelectedGroup(TlsNamedGroup.BrainpoolP256r1)])));
    }

    [TestMethod]
    public void AHelloRetryRequestForAGroupAlreadySharedIsAnIllegalParameter() =>
        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, RetryBytes([Tls13(), KeyShareExtension.EncodeSelectedGroup(TlsNamedGroup.X25519)]));

    [TestMethod]
    public void AServerHelloWithAnExtensionItMayNotCarryIsAnUnsupportedExtension() =>
        AssertServerHelloFails(TlsAlertDescription.UnsupportedExtension, ServerHelloBytes([Tls13(), CookieExtension.Encode([1])]));

    [TestMethod]
    public void AServerHelloWithoutAKeyShareIsAMissingExtension() =>
        AssertServerHelloFails(TlsAlertDescription.MissingExtension, ServerHelloBytes([Tls13()]));

    [TestMethod]
    public void AServerHelloWithAMalformedKeyShareIsADecodeError() =>
        AssertServerHelloFails(TlsAlertDescription.DecodeError, ServerHelloBytes([Tls13(), new TlsExtension(TlsExtensionType.KeyShare, [0, 29, 0])]));

    [TestMethod]
    public void AServerHelloOnAnUnsupportedGroupIsAnIllegalParameter()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = TlsNamedGroup.Secp256r1, AnswerUnsharedGroup = true };
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("failing flight", "ServerHello answering secp256r1, a group the client sent no share for");

        AssertFails(TlsAlertDescription.IllegalParameter, RunTimed(client, server));
    }

    [TestMethod]
    public void AServerHelloWithADegenerateKeyShareIsAnIllegalParameter() =>
        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, ServerHelloBytes([Tls13(), KeyShareExtension.EncodeServerShare(new KeyShareEntry(TlsNamedGroup.X25519, new byte[32]))]));

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519MlKem768, 1088 + 32 - 1)]
    [DataRow(TlsNamedGroup.X25519MlKem768, 1088 + 32 + 1)]
    [DataRow(TlsNamedGroup.X25519MlKem768, 32)]
    [DataRow(TlsNamedGroup.X448, 55)]
    [DataRow(TlsNamedGroup.X448, 57)]
    public void AServerShareOfTheWrongLengthIsAnIllegalParameter(int group, int length)
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with
        {
            CipherSuites = [0x1301],
            SupportedGroups = [TlsNamedGroup.X25519MlKem768, TlsNamedGroup.X448],
            KeyShareGroups = [TlsNamedGroup.X25519MlKem768, TlsNamedGroup.X448],
        });
        client.Start();
        Diagnostics.Arrange("failing flight", "ServerHello");
        Diagnostics.Arrange("server share", string.Create(CultureInfo.InvariantCulture, $"group 0x{group:x4}, {length} bytes"));

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13(), KeyShareExtension.EncodeServerShare(new KeyShareEntry((ushort)group, new byte[length]))]));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void MalformedEncryptedExtensionsAreADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, HandshakeType.EncryptedExtensions, new HandshakeMessage(HandshakeType.EncryptedExtensions, [0]).Encode());

    [TestMethod]
    public void AKeyShareInEncryptedExtensionsIsAnIllegalParameter() =>
        AssertFlightFails(TlsAlertDescription.IllegalParameter, HandshakeType.EncryptedExtensions, new EncryptedExtensions([KeyShareExtension.EncodeSelectedGroup(29)]).Encode());

    [TestMethod]
    public void AnEncryptedExtensionNotOfferedIsAnUnsupportedExtension() =>
        AssertFlightFails(TlsAlertDescription.UnsupportedExtension, HandshakeType.EncryptedExtensions, new EncryptedExtensions([new TlsExtension(TlsExtensionType.EcPointFormats, [1, 0])]).Encode());

    [TestMethod]
    public void AMalformedApplicationProtocolIsADecodeError() =>
        AssertApplicationProtocolFails(TlsAlertDescription.DecodeError, new TlsExtension(TlsExtensionType.ApplicationLayerProtocolNegotiation, [0, 9]));

    [TestMethod]
    public void TwoApplicationProtocolsAreAnIllegalParameter() =>
        AssertApplicationProtocolFails(TlsAlertDescription.IllegalParameter, ApplicationLayerProtocolNegotiationExtension.Encode(["h2", "http/1.1"]));

    [TestMethod]
    public void AnApplicationProtocolNotOfferedIsAnIllegalParameter() =>
        AssertApplicationProtocolFails(TlsAlertDescription.IllegalParameter, ApplicationLayerProtocolNegotiationExtension.Encode(["spdy/3"]));

    [TestMethod]
    public void AMalformedCertificateRequestIsADecodeError() =>
        AssertCertificateRequestFails(TlsAlertDescription.DecodeError, new HandshakeMessage(HandshakeType.CertificateRequest, [5]).Encode());

    [TestMethod]
    public void ACertificateRequestWithAContextIsAnIllegalParameter() =>
        AssertCertificateRequestFails(TlsAlertDescription.IllegalParameter, new CertificateRequest([1], [SignatureAlgorithmsExtension.Encode([0x0807])]).Encode());

    [TestMethod]
    public void ACertificateRequestWithoutSignatureAlgorithmsIsAMissingExtension() =>
        AssertCertificateRequestFails(TlsAlertDescription.MissingExtension, new CertificateRequest([], []).Encode());

    [TestMethod]
    public void ACertificateRequestWithMalformedSignatureAlgorithmsIsADecodeError() =>
        AssertCertificateRequestFails(TlsAlertDescription.DecodeError, new CertificateRequest([], [new TlsExtension(TlsExtensionType.SignatureAlgorithms, [0, 3, 8])]).Encode());

    [TestMethod]
    public void AMalformedCertificateIsADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, HandshakeType.Certificate, new HandshakeMessage(HandshakeType.Certificate, [0, 0]).Encode());

    [TestMethod]
    public void AServerCertificateWithAContextIsAnIllegalParameter() =>
        AssertFlightFails(TlsAlertDescription.IllegalParameter, HandshakeType.Certificate, new CertificateMessage([1], [new CertificateEntry([0x30, 0], [])]).Encode());

    [TestMethod]
    public void AnEmptyServerCertificateIsADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, HandshakeType.Certificate, new CertificateMessage([], []).Encode());

    [TestMethod]
    public void AServerCertificateThatDoesNotParseIsABadCertificate() =>
        AssertFlightFails(TlsAlertDescription.BadCertificate, HandshakeType.Certificate, new CertificateMessage([], [new CertificateEntry([0x30, 0], [])]).Encode());

    [TestMethod]
    public void ACertificateEntryExtensionNotOfferedIsAnUnsupportedExtension()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { LeafExtensions = [StatusRequestExtension.EncodeOcspResponse([1])] };
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("failing flight", "encrypted server flight, leaf Certificate entry carrying an unrequested status_request");

        AssertFails(TlsAlertDescription.UnsupportedExtension, RunTimed(client, server));
    }

    [TestMethod]
    public void AMalformedStapledResponseIsADecodeError()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { LeafExtensions = [new TlsExtension(TlsExtensionType.StatusRequest, [1, 0, 0, 9])] };
        TlsExtension statusRequest = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));
        using Tls13ClientHandshake client = Client(DefaultSettings with { FixedExtensions = [statusRequest] });
        Diagnostics.Arrange("client offers", "status_request (OCSP)");
        Diagnostics.Arrange("failing flight", "encrypted server flight, leaf status_request declaring 9 bytes and carrying none");

        AssertFails(TlsAlertDescription.DecodeError, RunTimed(client, server));
    }

    [TestMethod]
    public void ARejectedChainFailsWithBadCertificateAndTheVerifiersReason()
    {
        object reason = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));
        Diagnostics.Arrange("verifier verdict", "rejected, no alert named");
        Diagnostics.Arrange("failing flight", "encrypted server flight, at the Certificate");

        Tls13HandshakeOutput output = RunTimed(client, server);

        AssertFails(TlsAlertDescription.BadCertificate, output);
        Diagnostics.Assert("rejection is the verifier's reason", true, ReferenceEquals(reason, output.Failure!.CertificateRejection));
        Diagnostics.Assert("IsCertificateRejection", true, output.Failure.IsCertificateRejection);
        Assert.AreSame(reason, output.Failure!.CertificateRejection);
        Assert.IsTrue(output.Failure.IsCertificateRejection);
    }

    [TestMethod]
    public void ARejectedChainSendsTheAlertTheVerifierNames()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected("expired", TlsAlertDescription.CertificateExpired)));
        Diagnostics.Arrange("verifier verdict", "rejected \"expired\", alert certificate_expired");
        Diagnostics.Arrange("failing flight", "encrypted server flight, at the Certificate");

        AssertFails(TlsAlertDescription.CertificateExpired, RunTimed(client, server));
    }

    [TestMethod]
    public void AMalformedCertificateVerifyIsADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, HandshakeType.CertificateVerify, new HandshakeMessage(HandshakeType.CertificateVerify, [8]).Encode());

    [TestMethod]
    public void ACertificateVerifySchemeNotOfferedIsAnIllegalParameter()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [TlsSignatureScheme.EcdsaSecp256r1Sha256] });
        Diagnostics.Arrange("offered signature schemes", "ecdsa_secp256r1_sha256");
        Diagnostics.Arrange("failing flight", "encrypted server flight, CertificateVerify signed with ed25519");

        AssertFails(TlsAlertDescription.IllegalParameter, RunTimed(client, server));
    }

    [TestMethod]
    public void ACertificateVerifyWithAPkcs1SchemeIsAnIllegalParameter()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [TlsSignatureScheme.RsaPkcs1Sha256, TlsSignatureScheme.Ed25519] });
        Diagnostics.Arrange("offered signature schemes", "rsa_pkcs1_sha256, ed25519");
        Diagnostics.Arrange("failing flight", "encrypted server flight, CertificateVerify claiming rsa_pkcs1_sha256");

        AssertFails(TlsAlertDescription.IllegalParameter, RunTimed(client, server, replaceFlight: flight =>
            Replace(flight, HandshakeType.CertificateVerify, new CertificateVerify(TlsSignatureScheme.RsaPkcs1Sha256, new byte[64]).Encode())));
    }

    [TestMethod]
    public void ABadCertificateVerifySignatureIsADecryptError() =>
        AssertFlightFails(TlsAlertDescription.DecryptError, "CertificateVerify with its last byte flipped", flight => Tamper(flight, HandshakeType.CertificateVerify));

    [TestMethod]
    public void ABadServerFinishedIsADecryptError() =>
        AssertFlightFails(TlsAlertDescription.DecryptError, "Finished with its last byte flipped", flight => Tamper(flight, HandshakeType.Finished));

    private static TlsExtension Tls13() => SupportedVersionsExtension.EncodeSelected(0x0304);

    private static byte[] ServerHelloBytes(
        IReadOnlyList<TlsExtension> extensions,
        byte[]? random = null,
        byte[]? sessionIdEcho = null,
        ushort cipherSuite = 0x1301,
        byte compression = 0) =>
        new ServerHello(0x0303, random ?? ServerRandom, sessionIdEcho ?? [], cipherSuite, compression, extensions).Encode();

    private static byte[] RetryBytes(IReadOnlyList<TlsExtension> extensions) =>
        ServerHelloBytes(extensions, random: ServerHello.HelloRetryRequestRandom.ToArray());

    private Tls13HandshakeOutput RunTimed(Tls13ClientHandshake client, Tls13TestServer server, Func<List<byte[]>, List<byte[]>>? replaceFlight = null)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return Run(client, server, replaceFlight: replaceFlight);
        }
    }

    private void AssertServerHelloFails(TlsAlertDescription alert, byte[] serverHello)
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301] });
        client.Start();
        Diagnostics.Arrange("failing flight", "ServerHello at the Initial level");
        Diagnostics.Bytes("server hello", serverHello);

        AssertFails(alert, client.Receive(TlsEncryptionLevel.Initial, serverHello));
    }

    private void AssertFlightFails(TlsAlertDescription alert, HandshakeType replaced, byte[] replacement)
    {
        Diagnostics.Bytes(string.Create(CultureInfo.InvariantCulture, $"replacement {replaced}"), replacement);
        AssertFlightFails(alert, string.Create(CultureInfo.InvariantCulture, $"{replaced} replaced"), flight => Replace(flight, replaced, replacement));
    }

    private void AssertFlightFails(TlsAlertDescription alert, string change, Func<List<byte[]>, List<byte[]>> replaceFlight)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("failing flight", "encrypted server flight, " + change);

        AssertFails(alert, RunTimed(client, server, replaceFlight: replaceFlight));
    }

    private void AssertApplicationProtocolFails(TlsAlertDescription alert, TlsExtension applicationProtocol)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2", "http/1.1"] });
        Diagnostics.Arrange("offered protocols", "h2, http/1.1");
        Diagnostics.Arrange("failing flight", "encrypted server flight, EncryptedExtensions carrying the ALPN answer below");
        Diagnostics.Bytes("ALPN extension data", applicationProtocol.Data);

        AssertFails(alert, RunTimed(client, server, replaceFlight: flight =>
            Replace(flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([applicationProtocol]).Encode())));
    }

    private void AssertCertificateRequestFails(TlsAlertDescription alert, byte[] certificateRequest)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true };
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("failing flight", "encrypted server flight, CertificateRequest replaced");
        Diagnostics.Bytes("certificate request", certificateRequest);

        AssertFails(alert, RunTimed(client, server, replaceFlight: flight => Replace(flight, HandshakeType.CertificateRequest, certificateRequest)));
    }

    private void AssertFails(TlsAlertDescription alert, Tls13HandshakeOutput output)
    {
        Diagnostics.Act("alert", output.Failure?.Alert);
        Diagnostics.Act("origin", output.Failure?.Origin);
        Diagnostics.Assert("failure alert", alert, output.Failure?.Alert);
        Diagnostics.Assert("IsComplete", false, output.IsComplete);
        Diagnostics.Assert("bytes to send", 0, output.BytesToSend.Count);
        Assert.IsNotNull(output.Failure);
        Assert.AreEqual(alert, output.Failure.Alert);
        Assert.IsFalse(output.IsComplete);
        Assert.IsEmpty(output.BytesToSend);
    }
}
