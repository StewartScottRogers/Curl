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

    [TestMethod]
    public void ConstructorRejectsNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Tls13ClientHandshake(null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Tls13ClientHandshake(DefaultSettings, null!, new RecordingCertificateVerifier()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Tls13ClientHandshake(DefaultSettings, SystemTlsRandomSource.Instance, null!));
    }

    [TestMethod]
    public void ConstructorRejectsSettingsThatCannotDriveAHandshake()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [0xc02f] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519, 0x0016] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.Secp256r1] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.BrainpoolP256r1, TlsNamedGroup.X25519], KeyShareGroups = [TlsNamedGroup.BrainpoolP256r1] }));
    }

    [TestMethod]
    public void StartingTwiceThrows()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.Start());
    }

    [TestMethod]
    public void ReceivingBeforeStartingThrows()
    {
        using Tls13ClientHandshake client = Client();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.Receive(TlsEncryptionLevel.Initial, [1]));
    }

    [TestMethod]
    public void DisposingBeforeStartingReleasesNothing()
    {
        Tls13ClientHandshake client = Client();

        client.Dispose();

        Assert.IsFalse(client.IsComplete);
    }

    [TestMethod]
    public void BytesAtTheWrongLevelAreUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Handshake, [1]));
    }

    [TestMethod]
    public void AnUnknownHandshakeTypeIsUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, [3, 0, 0, 0]));
    }

    [TestMethod]
    public void AfterAFailureEveryCallReturnsTheSameFailureAndNothingElse()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        TlsHandshakeFailure failure = client.Receive(TlsEncryptionLevel.Handshake, [1]).Failure!;

        Tls13HandshakeOutput again = client.Receive(TlsEncryptionLevel.Initial, [2, 0, 0, 0]);

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

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, [.. flight.ServerHello, .. flight.HandshakeMessages[0]]));
    }

    [TestMethod]
    public void AnythingButAServerHelloFirstIsUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Initial, new EncryptedExtensions([]).Encode()));
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

        Tls13HandshakeOutput output = Run(client, server, replaceFlight: flight =>
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

        Tls13HandshakeOutput output = Run(client, server, replaceFlight: flight => [flight[0], flight[1], flight[3]]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AnythingButANewSessionTicketAfterTheHandshakeIsUnexpected()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        Run(client, server);

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.Receive(TlsEncryptionLevel.Application, new Finished(new byte[32]).Encode()));
    }

    [TestMethod]
    public void AMalformedNewSessionTicketIsADecodeError()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();
        Run(client, server);

        AssertFails(TlsAlertDescription.DecodeError, client.Receive(TlsEncryptionLevel.Application, new HandshakeMessage(HandshakeType.NewSessionTicket, [0]).Encode()));
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

        AssertServerHelloFails(TlsAlertDescription.IllegalParameter, ServerHelloBytes([], random: random));
    }

    [TestMethod]
    public void ATls12ServerHelloWithoutAValidSentinelIsTheWrongProtocolVersion()
    {
        byte[] random = [.. ServerRandom[..24], .. "DOWNGRD"u8, 0x02];

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

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13()], cipherSuite: 0x1302)));
    }

    [TestMethod]
    public void AServerHelloChoosingTheOfferedRenegotiationScsvIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301], OfferEmptyRenegotiationInfoScsv = true });
        client.Start();

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13()], cipherSuite: Tls12CipherSuite.EmptyRenegotiationInfoScsv)));
    }

    [TestMethod]
    public void AServerHelloChangingTheSuiteAfterAHelloRetryRequestIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301, 0x1302] });
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), KeyShareExtension.EncodeSelectedGroup(TlsNamedGroup.Secp256r1)]));

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13()], cipherSuite: 0x1302)));
    }

    [TestMethod]
    public void ASecondHelloRetryRequestIsUnexpected()
    {
        using Tls13ClientHandshake client = Client();
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), CookieExtension.Encode([1])]));

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

        AssertFails(TlsAlertDescription.IllegalParameter, client.Receive(TlsEncryptionLevel.Initial, RetryBytes([Tls13(), KeyShareExtension.EncodeSelectedGroup(TlsNamedGroup.Secp384r1)])));
    }

    [TestMethod]
    public void AHelloRetryRequestForATls12OnlyGroupOfferedIsAnIllegalParameter()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.BrainpoolP256r1, TlsNamedGroup.X25519] });
        client.Start();

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

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, server));
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

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Initial, ServerHelloBytes([Tls13(), KeyShareExtension.EncodeServerShare(new KeyShareEntry((ushort)group, new byte[length]))]));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void MalformedEncryptedExtensionsAreADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, flight => Replace(flight, HandshakeType.EncryptedExtensions, new HandshakeMessage(HandshakeType.EncryptedExtensions, [0]).Encode()));

    [TestMethod]
    public void AKeyShareInEncryptedExtensionsIsAnIllegalParameter() =>
        AssertFlightFails(TlsAlertDescription.IllegalParameter, flight => Replace(flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([KeyShareExtension.EncodeSelectedGroup(29)]).Encode()));

    [TestMethod]
    public void AnEncryptedExtensionNotOfferedIsAnUnsupportedExtension() =>
        AssertFlightFails(TlsAlertDescription.UnsupportedExtension, flight => Replace(flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([new TlsExtension(TlsExtensionType.EcPointFormats, [1, 0])]).Encode()));

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
        AssertFlightFails(TlsAlertDescription.DecodeError, flight => Replace(flight, HandshakeType.Certificate, new HandshakeMessage(HandshakeType.Certificate, [0, 0]).Encode()));

    [TestMethod]
    public void AServerCertificateWithAContextIsAnIllegalParameter() =>
        AssertFlightFails(TlsAlertDescription.IllegalParameter, flight => Replace(flight, HandshakeType.Certificate, new CertificateMessage([1], [new CertificateEntry([0x30, 0], [])]).Encode()));

    [TestMethod]
    public void AnEmptyServerCertificateIsADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, flight => Replace(flight, HandshakeType.Certificate, new CertificateMessage([], []).Encode()));

    [TestMethod]
    public void AServerCertificateThatDoesNotParseIsABadCertificate() =>
        AssertFlightFails(TlsAlertDescription.BadCertificate, flight => Replace(flight, HandshakeType.Certificate, new CertificateMessage([], [new CertificateEntry([0x30, 0], [])]).Encode()));

    [TestMethod]
    public void ACertificateEntryExtensionNotOfferedIsAnUnsupportedExtension()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { LeafExtensions = [StatusRequestExtension.EncodeOcspResponse([1])] };
        using Tls13ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.UnsupportedExtension, Run(client, server));
    }

    [TestMethod]
    public void AMalformedStapledResponseIsADecodeError()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { LeafExtensions = [new TlsExtension(TlsExtensionType.StatusRequest, [1, 0, 0, 9])] };
        TlsExtension statusRequest = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));
        using Tls13ClientHandshake client = Client(DefaultSettings with { FixedExtensions = [statusRequest] });

        AssertFails(TlsAlertDescription.DecodeError, Run(client, server));
    }

    [TestMethod]
    public void ARejectedChainFailsWithBadCertificateAndTheVerifiersReason()
    {
        object reason = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));

        Tls13HandshakeOutput output = Run(client, server);

        AssertFails(TlsAlertDescription.BadCertificate, output);
        Assert.AreSame(reason, output.Failure!.CertificateRejection);
        Assert.IsTrue(output.Failure.IsCertificateRejection);
    }

    [TestMethod]
    public void ARejectedChainSendsTheAlertTheVerifierNames()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected("expired", TlsAlertDescription.CertificateExpired)));

        AssertFails(TlsAlertDescription.CertificateExpired, Run(client, server));
    }

    [TestMethod]
    public void AMalformedCertificateVerifyIsADecodeError() =>
        AssertFlightFails(TlsAlertDescription.DecodeError, flight => Replace(flight, HandshakeType.CertificateVerify, new HandshakeMessage(HandshakeType.CertificateVerify, [8]).Encode()));

    [TestMethod]
    public void ACertificateVerifySchemeNotOfferedIsAnIllegalParameter()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [TlsSignatureScheme.EcdsaSecp256r1Sha256] });

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, server));
    }

    [TestMethod]
    public void ACertificateVerifyWithAPkcs1SchemeIsAnIllegalParameter()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [TlsSignatureScheme.RsaPkcs1Sha256, TlsSignatureScheme.Ed25519] });

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, server, replaceFlight: flight =>
            Replace(flight, HandshakeType.CertificateVerify, new CertificateVerify(TlsSignatureScheme.RsaPkcs1Sha256, new byte[64]).Encode())));
    }

    [TestMethod]
    public void ABadCertificateVerifySignatureIsADecryptError() =>
        AssertFlightFails(TlsAlertDescription.DecryptError, flight => Tamper(flight, HandshakeType.CertificateVerify));

    [TestMethod]
    public void ABadServerFinishedIsADecryptError() =>
        AssertFlightFails(TlsAlertDescription.DecryptError, flight => Tamper(flight, HandshakeType.Finished));

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

    private static void AssertServerHelloFails(TlsAlertDescription alert, byte[] serverHello)
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301] });
        client.Start();

        AssertFails(alert, client.Receive(TlsEncryptionLevel.Initial, serverHello));
    }

    private static void AssertFlightFails(TlsAlertDescription alert, Func<List<byte[]>, List<byte[]>> replaceFlight)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client();

        AssertFails(alert, Run(client, server, replaceFlight: replaceFlight));
    }

    private static void AssertApplicationProtocolFails(TlsAlertDescription alert, TlsExtension applicationProtocol)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2", "http/1.1"] });

        AssertFails(alert, Run(client, server, replaceFlight: flight =>
            Replace(flight, HandshakeType.EncryptedExtensions, new EncryptedExtensions([applicationProtocol]).Encode())));
    }

    private static void AssertCertificateRequestFails(TlsAlertDescription alert, byte[] certificateRequest)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true };
        using Tls13ClientHandshake client = Client();

        AssertFails(alert, Run(client, server, replaceFlight: flight => Replace(flight, HandshakeType.CertificateRequest, certificateRequest)));
    }

    private static void AssertFails(TlsAlertDescription alert, Tls13HandshakeOutput output)
    {
        Assert.IsNotNull(output.Failure);
        Assert.AreEqual(alert, output.Failure.Alert);
        Assert.IsFalse(output.IsComplete);
        Assert.IsEmpty(output.BytesToSend);
    }
}
