using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Testing;
using static Curl.Tls.Tls12HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// Every way a server's messages fail the TLS 1.2, 1.1 and 1.0 client handshake, each with
/// the typed alert: out-of-order messages, a refused version or downgrade, a suite or
/// extension the client did not offer, a missing or non-empty <c>renegotiation_info</c>, a
/// resumption that changes the session, malformed messages, a rejected chain, a bad
/// ServerKeyExchange signature, bad key exchange parameters and a bad Finished.
/// </summary>
[TestClass]
public sealed class Tls12ClientHandshakeFailureTests
{
    private static readonly Tls12ClientSettings EverySuite = DefaultSettings with { CipherSuites = [.. Tls12CipherSuite.All.Select(suite => suite.Code)] };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ConstructorRejectsNullArguments()
    {
        Diagnostics.Arrange("null argument", "settings, then random source, then certificate verifier");

        ArgumentNullException settings = Assert.ThrowsExactly<ArgumentNullException>(() => new Tls12ClientHandshake(null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier()));
        ArgumentNullException randomSource = Assert.ThrowsExactly<ArgumentNullException>(() => new Tls12ClientHandshake(DefaultSettings, null!, new RecordingCertificateVerifier()));
        ArgumentNullException verifier = Assert.ThrowsExactly<ArgumentNullException>(() => new Tls12ClientHandshake(DefaultSettings, SystemTlsRandomSource.Instance, null!));
        Diagnostics.Act("refused parameters", $"{settings.ParamName}, {randomSource.ParamName}, {verifier.ParamName}");

        Diagnostics.Assert("refusals", 3, new[] { settings, randomSource, verifier }.Length);
    }

    [TestMethod]
    public void ConstructorRejectsSettingsThatCannotDriveAHandshake()
    {
        Tls12Session session = new(TlsProtocolVersion.Tls12, 0xc02b, [1], null, 0, new byte[48], true);
        Diagnostics.Arrange("refused settings", "SSL 3.0 minimum, 0x0304 maximum, minimum above maximum, no suites, only the SCSV, a TLS 1.3 suite, only CCM below TLS 1.2, an FFDHE group, an unknown scheme, and three sessions that cannot resume");
        Diagnostics.Arrange("session", session);

        ArgumentException[] refusals =
        [
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MinimumVersion = (TlsProtocolVersion)0x0300 })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MaximumVersion = (TlsProtocolVersion)0x0304 })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls12, MaximumVersion = TlsProtocolVersion.Tls11 })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [Tls12CipherSuite.EmptyRenegotiationInfoScsv] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [0xc02b, 0x1301] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10, MaximumVersion = TlsProtocolVersion.Tls11, CipherSuites = [0xc0ac, 0xc0a0] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.Ffdhe2048] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SignatureAlgorithms = [0xfefe] })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SessionToResume = session with { Version = TlsProtocolVersion.Tls11 } })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MaximumVersion = TlsProtocolVersion.Tls11, MinimumVersion = TlsProtocolVersion.Tls10, SessionToResume = session })),
            Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SessionToResume = session with { CipherSuite = 0x0001 } })),
        ];
        Diagnostics.Act("refused parameters", string.Join(", ", refusals.Select(refusal => refusal.ParamName)));

        Diagnostics.Assert("refusals", 12, refusals.Length);
    }

    [TestMethod]
    public void SupportedGroupsTakeX448AndTheBrainpoolCurvesAndTheRefusalNamesEveryGroupAllowed()
    {
        Diagnostics.Arrange("accepted groups", "brainpoolP256r1, brainpoolP384r1, brainpoolP512r1, x448");
        Diagnostics.Arrange("refused group", "X25519MLKEM768");
        _ = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.BrainpoolP256r1, TlsNamedGroup.BrainpoolP384r1, TlsNamedGroup.BrainpoolP512r1, TlsNamedGroup.X448] });

        ArgumentException refusal = Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519MlKem768] }));
        Diagnostics.Act("refusal", refusal.Message);

        Diagnostics.Assert("refused parameter", "SupportedGroups", refusal.ParamName);
        Assert.AreEqual("SupportedGroups", refusal.ParamName);
        StringAssert.StartsWith(refusal.Message, "Every supported group must be x25519, x448, secp256r1, secp384r1, secp521r1, brainpoolP256r1, brainpoolP384r1 or brainpoolP512r1.");
    }

    [TestMethod]
    public void StartingTwiceThrows()
    {
        Tls12ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("client", "started once");

        InvalidOperationException refusal = Assert.ThrowsExactly<InvalidOperationException>(() => client.Start());
        Diagnostics.Act("second start", refusal.Message);

        Diagnostics.Assert("second start throws", nameof(InvalidOperationException), refusal.GetType().Name);
    }

    [TestMethod]
    public void ReceivingBeforeStartingThrows()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("client", "not started; receives a handshake byte, then a ChangeCipherSpec");

        InvalidOperationException handshake = Assert.ThrowsExactly<InvalidOperationException>(() => client.ReceiveHandshake([2]));
        InvalidOperationException changeCipherSpec = Assert.ThrowsExactly<InvalidOperationException>(() => client.ReceiveChangeCipherSpec([1]));
        Diagnostics.Act("handshake refusal", handshake.Message);
        Diagnostics.Act("ChangeCipherSpec refusal", changeCipherSpec.Message);

        Diagnostics.Assert("both calls throw", 2, new[] { handshake, changeCipherSpec }.Length);
    }

    [TestMethod]
    public void AfterAFailureEveryCallReturnsTheSameFailureAndNothingElse()
    {
        Tls12ClientHandshake client = Client();
        client.Start();
        Tls12HandshakeOutput failed = client.ReceiveHandshake([3, 0, 0, 0]);
        Diagnostics.Bytes("first server message (HelloVerifyRequest type)", [3, 0, 0, 0]);
        Diagnostics.Arrange("first failure", failed.Failure);

        Tls12HandshakeOutput again = client.ReceiveHandshake(new HandshakeMessage(HandshakeType.ServerHello, []).Encode());
        Tls12HandshakeOutput changeCipherSpec = client.ReceiveChangeCipherSpec([1]);
        Diagnostics.Act("failure after a ServerHello", again.Failure);
        Diagnostics.Act("failure after a ChangeCipherSpec", changeCipherSpec.Failure);

        AssertFails(TlsAlertDescription.UnexpectedMessage, failed);
        Diagnostics.Assert("same failure every call", true, ReferenceEquals(failed.Failure, again.Failure) && ReferenceEquals(failed.Failure, changeCipherSpec.Failure));
        Diagnostics.Assert("messages sent after the failure", 0, again.MessagesToSend.Count);
        Assert.AreSame(failed.Failure, again.Failure);
        Assert.AreSame(failed.Failure, changeCipherSpec.Failure);
        Assert.IsEmpty(again.MessagesToSend);
        Assert.AreSame(failed.Failure, client.Failure);
    }

    [TestMethod]
    public void AHelloRequestWithABodyIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("server's first flight", "a HelloRequest with a one-byte body");

        AssertFails(TlsAlertDescription.DecodeError, client.ReceiveHandshake(new HandshakeMessage(HandshakeType.HelloRequest, [0]).Encode()));
    }

    [TestMethod]
    public void AnythingButAServerHelloFirstIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("server's first flight", "an empty Certificate in place of the ServerHello");

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.ReceiveHandshake(new Tls12CertificateMessage([]).Encode()));
    }

    [TestMethod]
    public void AMalformedServerHelloIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        client.Start();
        Diagnostics.Arrange("server's first flight", "a ServerHello whose body is only 03 03");

        AssertFails(TlsAlertDescription.DecodeError, client.ReceiveHandshake(new HandshakeMessage(HandshakeType.ServerHello, [3, 3]).Encode()));
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, TlsProtocolVersion.Tls12, TlsProtocolVersion.Tls11)]
    [DataRow(TlsProtocolVersion.Tls12, TlsProtocolVersion.Tls12, TlsProtocolVersion.Tls10)]
    [DataRow(TlsProtocolVersion.Tls10, TlsProtocolVersion.Tls11, TlsProtocolVersion.Tls12)]
    public void AVersionOutsideTheRangeIsAProtocolVersionAlert(TlsProtocolVersion minimum, TlsProtocolVersion maximum, TlsProtocolVersion server)
    {
        Tls12TestServer testServer = new(Rsa()) { Version = server, CipherSuite = 0xc013 };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = minimum, MaximumVersion = maximum });
        Diagnostics.Arrange("client range", $"{minimum} to {maximum}");
        Diagnostics.Arrange("server's version in its first flight", server);

        AssertFails(TlsAlertDescription.ProtocolVersion, Run(client, testServer));
        Diagnostics.Assert("negotiated version", null, client.Version);
        Assert.IsNull(client.Version);
    }

    [TestMethod]
    public void AVersionThatIsNoTlsVersionIsAProtocolVersionAlert()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a ServerHello with legacy_version 0x0300");

        AssertFails(TlsAlertDescription.ProtocolVersion, Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(flight, hello => hello with { LegacyVersion = 0x0300 })));
    }

    [TestMethod]
    public void TheDowngradeSentinelFromATlsOneTwoClientIsIllegal()
    {
        Tls12TestServer server = new(Rsa()) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc013, SendDowngradeSentinel = true };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10 });
        Diagnostics.Arrange("server's first flight", "a TLS 1.1 ServerHello whose random ends in the downgrade sentinel");

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, server));
    }

    [TestMethod]
    [DataRow((ushort)0x1301, TlsProtocolVersion.Tls12, (byte)0)]
    [DataRow((ushort)0x0001, TlsProtocolVersion.Tls12, (byte)0)]
    [DataRow((ushort)0xc02b, TlsProtocolVersion.Tls11, (byte)0)]
    [DataRow((ushort)0xc02b, TlsProtocolVersion.Tls12, (byte)1)]
    public void AServerHelloChoosingWhatWasNotOfferedIsIllegal(int cipherSuite, TlsProtocolVersion version, byte compression)
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10 });
        Diagnostics.Arrange("server's first flight", $"a ServerHello choosing suite 0x{cipherSuite:x4}, {version}, compression {compression}");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { CipherSuite = (ushort)cipherSuite, LegacyVersion = (ushort)version, LegacyCompressionMethod = compression }));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    [DataRow((ushort)0xc0ac, TlsProtocolVersion.Tls11)]
    [DataRow((ushort)0xc0a0, TlsProtocolVersion.Tls10)]
    public void AServerChoosingACcmSuiteBelowTlsOneTwoIsIllegal(int cipherSuite, TlsProtocolVersion version)
    {
        Tls12ClientHandshake client = Client(EverySuite with { MinimumVersion = TlsProtocolVersion.Tls10 });
        Diagnostics.Arrange("server's first flight", $"a ServerHello choosing CCM suite 0x{cipherSuite:x4} at {version}");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { CipherSuite = (ushort)cipherSuite, LegacyVersion = (ushort)version }));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void AServerHelloExtensionThatWasNotOfferedIsUnsupported()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a ServerHello carrying a key_share extension the client did not offer");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [.. hello.Extensions, new TlsExtension(TlsExtensionType.KeyShare, [])] }));

        AssertFails(TlsAlertDescription.UnsupportedExtension, output);
    }

    [TestMethod]
    public void AServerWithoutSecureRenegotiationIsRefused()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a ServerHello without renegotiation_info");

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(client, new Tls12TestServer(Ecdsa()) { SendRenegotiationInfo = false }));
    }

    [TestMethod]
    [DataRow(new byte[] { 1 }, TlsAlertDescription.DecodeError)]
    [DataRow(new byte[] { 1, 7 }, TlsAlertDescription.HandshakeFailure)]
    public void ARenegotiationInfoThatIsMalformedOrNotEmptyFails(byte[] data, TlsAlertDescription alert)
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a ServerHello whose only extension is this renegotiation_info");
        Diagnostics.Bytes("renegotiation_info in the server's first flight", data);

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [new TlsExtension(TlsExtensionType.RenegotiationInfo, data)] }));

        AssertFails(alert, output);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 1 }, TlsAlertDescription.DecodeError)]
    [DataRow(new byte[] { 0, 6, 2, (byte)'h', (byte)'2', 2, (byte)'h', (byte)'3' }, TlsAlertDescription.IllegalParameter)]
    [DataRow(new byte[] { 0, 3, 2, (byte)'h', (byte)'3' }, TlsAlertDescription.IllegalParameter)]
    public void AnApplicationProtocolThatIsMalformedOrNotOfferedIsRefused(byte[] data, TlsAlertDescription alert)
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2"] });
        Diagnostics.Arrange("offered protocols", "h2");
        Diagnostics.Bytes("ALPN in the server's first flight", data);

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [.. hello.Extensions, new TlsExtension(TlsExtensionType.ApplicationLayerProtocolNegotiation, data)] }));

        AssertFails(alert, output);
        Diagnostics.Assert("negotiated protocol", null, client.ApplicationProtocol);
        Assert.IsNull(client.ApplicationProtocol);
    }

    [TestMethod]
    public void AResumptionOnAnotherVersionOrSuiteIsIllegal()
    {
        Tls12TestSessionCache sessions = new();
        TestServerCredential credential = Rsa();
        Tls12ClientHandshake first = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11, OfferSessionTicket = false });
        using (Diagnostics.Phase("full handshake"))
        {
            Run(first, new Tls12TestServer(credential) { CipherSuite = 0xc013, Sessions = sessions });
        }

        Tls12ClientSettings resume = DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11, OfferSessionTicket = false, SessionToResume = first.Session };
        Diagnostics.Arrange("session to resume", first.Session);
        Diagnostics.Arrange("resuming servers' first flights", "TLS 1.1 with suite 0xc013, then TLS 1.2 with suite 0xc014");

        Tls12HandshakeOutput otherVersion;
        Tls12HandshakeOutput otherSuite;
        using (Diagnostics.Phase("resumptions"))
        {
            otherVersion = Run(Client(resume), new Tls12TestServer(credential) { CipherSuite = 0xc013, Version = TlsProtocolVersion.Tls11, Sessions = sessions });
            otherSuite = Run(Client(resume), new Tls12TestServer(credential) { CipherSuite = 0xc014, Sessions = sessions });
        }

        AssertFails(TlsAlertDescription.IllegalParameter, otherVersion);
        AssertFails(TlsAlertDescription.IllegalParameter, otherSuite);
    }

    [TestMethod]
    public void AResumptionThatChangesTheExtendedMasterSecretIsAHandshakeFailure()
    {
        Tls12TestServer server = new(Ecdsa());
        Tls12ClientHandshake first = Client(DefaultSettings with { OfferSessionTicket = false });
        using (Diagnostics.Phase("full handshake"))
        {
            Run(first, server);
        }

        Tls12ClientHandshake second = Client(DefaultSettings with { OfferSessionTicket = false, SessionToResume = first.Session! with { ExtendedMasterSecret = false } });
        Diagnostics.Arrange("session to resume, extended master secret cleared", first.Session! with { ExtendedMasterSecret = false });

        Tls12HandshakeOutput output;
        using (Diagnostics.Phase("resumption"))
        {
            output = Run(second, server);
        }

        AssertFails(TlsAlertDescription.HandshakeFailure, output);
    }

    [TestMethod]
    public void ARepeatedCertificateIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "the Certificate sent twice");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => [flight[0], flight[1], .. flight[1..]]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AServerKeyExchangeForRsaKeyExchangeIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "an ECDHE ServerKeyExchange after a ServerHello choosing RSA key exchange 0x009c");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Rsa()) { CipherSuite = 0xc02f }, flight => RewriteServerHello(flight, hello => hello with { CipherSuite = 0x009c }));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void ACertificateStatusThatWasNotPromisedIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        byte[] status = new HandshakeMessage(HandshakeType.CertificateStatus, StatusRequestExtension.EncodeOcspResponse([1]).Data).Encode();
        Diagnostics.Arrange("server's first flight", "a CertificateStatus the client never asked for, after the Certificate");
        Diagnostics.Bytes("CertificateStatus inserted after the Certificate in the server's first flight", status);

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => [flight[0], flight[1], new(TlsContentType.Handshake, status), .. flight[2..]]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void ACertificateInAnAnonymousSuiteIsUnexpected()
    {
        Tls12ClientHandshake client = Client(EverySuite);
        Diagnostics.Arrange("server's first flight", "a Certificate after a ServerHello choosing anonymous suite 0xc018");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(flight, hello => hello with { CipherSuite = 0xc018 }));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    [DataRow(HandshakeType.Certificate)]
    [DataRow(HandshakeType.ServerKeyExchange)]
    [DataRow(HandshakeType.ServerHelloDone)]
    public void AMissingRequiredMessageIsUnexpected(HandshakeType missing)
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", $"{missing} removed and a Finished appended");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => [.. Remove(flight, missing), new(TlsContentType.Handshake, new Finished(new byte[12]).Encode())]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AMalformedCertificateIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a Certificate whose body is 00 00 09");

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.Certificate, new HandshakeMessage(HandshakeType.Certificate, [0, 0, 9]).Encode())));
    }

    [TestMethod]
    public void AnEmptyServerCertificateIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a Certificate with no certificates");

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.Certificate, new Tls12CertificateMessage([]).Encode())));
    }

    [TestMethod]
    public void ACertificateThatDoesNotParseIsABadCertificate()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a Certificate holding the empty SEQUENCE 30 00");

        AssertFails(TlsAlertDescription.BadCertificate, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.Certificate, new Tls12CertificateMessage([[0x30, 0x00]]).Encode())));
    }

    [TestMethod]
    [DataRow((ushort)0xc02f, "ecdsa")]
    [DataRow((ushort)0xc02b, "rsa")]
    [DataRow((ushort)0x00a2, "rsa")]
    [DataRow((ushort)0x009e, "dsa")]
    public void AKeyOfTheWrongTypeForTheSuiteIsAHandshakeFailure(int cipherSuite, string credential)
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { CipherSuites = [(ushort)cipherSuite] });
        TestServerCredential key = credential switch
        {
            "rsa" => Rsa(),
            "dsa" => TestServerCredential.Dsa(TlsSignatureScheme.DsaSha256),
            _ => Ecdsa(),
        };
        Diagnostics.Arrange("suite and server key", $"0x{cipherSuite:x4} with an {credential} key");

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(client, new Tls12TestServer(key) { CipherSuite = (ushort)cipherSuite }));
    }

    [TestMethod]
    public void ARejectedChainFailsWithTheVerdictsAlertAndReason()
    {
        object reason = new();
        Tls12ClientHandshake client = Client(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));
        Diagnostics.Arrange("certificate verifier", "rejects every chain with a reason object");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()));

        AssertFails(TlsAlertDescription.BadCertificate, output);
        Diagnostics.Assert("rejection is the verifier's", true, ReferenceEquals(reason, output.Failure?.CertificateRejection));
        Assert.AreSame(reason, output.Failure!.CertificateRejection);
    }

    [TestMethod]
    public void AMalformedCertificateStatusIsRefused()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { RequestOcspStatus = true });
        Diagnostics.Arrange("server's first flight", "a CertificateStatus of unknown type 2 with body 02 00 00 01 01");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { OcspResponse = [1] }, flight => Replace(flight, HandshakeType.CertificateStatus, new HandshakeMessage(HandshakeType.CertificateStatus, [2, 0, 0, 1, 1]).Encode()));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
        Diagnostics.Assert("stapled response kept", null, client.OcspResponse);
        Assert.IsNull(client.OcspResponse);
    }

    [TestMethod]
    public void AMalformedServerKeyExchangeIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a ServerKeyExchange whose body is 03 00");

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.ServerKeyExchange, new HandshakeMessage(HandshakeType.ServerKeyExchange, [3, 0]).Encode())));
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls12, "rsa")]
    [DataRow(TlsProtocolVersion.Tls10, "rsa")]
    [DataRow(TlsProtocolVersion.Tls11, "ecdsa")]
    public void ABadServerKeyExchangeSignatureIsADecryptError(TlsProtocolVersion version, string credential)
    {
        Tls12TestServer server = new(credential == "rsa" ? Rsa() : Ecdsa()) { Version = version, CipherSuite = credential == "rsa" ? (ushort)0xc013 : (ushort)0xc009 };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10 });
        Diagnostics.Arrange("server's first flight", $"{version}, {credential} key, the ServerKeyExchange signature with one bit flipped");

        Tls12HandshakeOutput output = Run(client, server, flight => ReplaceServerKeyExchange(flight, version, message => message with { Signature = Corrupt(message.Signature!) }));

        AssertFails(TlsAlertDescription.DecryptError, output);
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, (ushort)0x00a2)]
    [DataRow(TlsProtocolVersion.Tls11, (ushort)0x0032)]
    [DataRow(TlsProtocolVersion.Tls10, (ushort)0x0032)]
    public void ADsaServerKeyExchangeSignatureThatDoesNotVerifyIsADecryptError(TlsProtocolVersion version, int cipherSuite)
    {
        Tls12TestServer server = new(TestServerCredential.Dsa(TlsSignatureScheme.DsaSha256)) { Version = version, CipherSuite = (ushort)cipherSuite };
        Tls12ClientHandshake client = Client(DefaultSettings with
        {
            MinimumVersion = TlsProtocolVersion.Tls10,
            CipherSuites = [0x00a2, 0x0032],
            SignatureAlgorithms = [TlsSignatureScheme.DsaSha256],
        });
        Diagnostics.Arrange("server's first flight", $"{version}, DHE-DSS suite 0x{cipherSuite:x4}, the ServerKeyExchange signature with one bit flipped");

        Tls12HandshakeOutput output = Run(client, server, flight => ReplaceServerKeyExchange(flight, version, message => message with { Signature = Corrupt(message.Signature!) }, Tls12KeyExchange.Dhe));

        AssertFails(TlsAlertDescription.DecryptError, output);
    }

    [TestMethod]
    public void ALegacyRsaSignatureOfTheWrongLengthOrOutOfRangeIsADecryptError()
    {
        Tls12TestServer server = new(Rsa()) { Version = TlsProtocolVersion.Tls10, CipherSuite = 0xc013 };
        Tls12ClientSettings settings = DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10 };
        Diagnostics.Arrange("server's first flights", "TLS 1.0 ServerKeyExchange signed one byte short, then 256 bytes of ff");

        Tls12HandshakeOutput shortSignature = Run(Client(settings), server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls10, message => message with { Signature = message.Signature![1..] }));
        Tls12HandshakeOutput tooLarge = Run(Client(settings), server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls10, message => message with { Signature = [.. Enumerable.Repeat((byte)0xff, 256)] }));

        AssertFails(TlsAlertDescription.DecryptError, shortSignature);
        AssertFails(TlsAlertDescription.DecryptError, tooLarge);
    }

    [TestMethod]
    public void AServerKeyExchangeSignedWithASchemeNotOfferedIsIllegal()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [TlsSignatureScheme.EcdsaSecp384r1Sha384] });
        Diagnostics.Arrange("offered scheme and server's", "ecdsa_secp384r1_sha384 offered; the server signs with ecdsa_secp256r1_sha256");

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(Ecdsa())));
    }

    [TestMethod]
    public void AnEd25519KeyHasNoTlsOneOneSignature()
    {
        Tls12TestServer server = new(TestServerCredential.Ed25519()) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc009 };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11 });
        Diagnostics.Arrange("server's first flight", "TLS 1.1, suite 0xc009, an Ed25519 key");

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, server));
    }

    [TestMethod]
    public void AnExplicitCurveIsIllegal()
    {
        Tls12ClientHandshake client = Client(EverySuite);
        byte[] explicitCurve = new HandshakeMessage(HandshakeType.ServerKeyExchange, [1, 0, 0x1d, 1, 9]).Encode();
        Diagnostics.Arrange("server's first flight", "anonymous ECDHE suite 0xc018 with an explicit_prime curve type in the ServerKeyExchange");
        Diagnostics.Bytes("ServerKeyExchange in the server's first flight (explicit_prime curve)", explicitCurve);

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(null) { CipherSuite = 0xc018 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, explicitCurve)));
    }

    [TestMethod]
    public void AGroupThatWasNotOfferedIsIllegal()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.Secp256r1] });
        Diagnostics.Arrange("offered group and server's", "secp256r1 offered; the server picks x25519");

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(Ecdsa()) { EcdheGroup = TlsNamedGroup.X25519 }));
    }

    [TestMethod]
    public void ADegenerateEcdhePointIsIllegal()
    {
        Tls12ClientHandshake client = Client(EverySuite);
        byte[] zeroPoint = new Tls12ServerKeyExchange(new Tls12EcdheParameters(TlsNamedGroup.Secp256r1, [4, 0]), null, null).Encode();
        Diagnostics.Arrange("server's first flight", "anonymous ECDHE suite 0xc018, secp256r1 with the degenerate point 04 00");
        Diagnostics.Bytes("ServerKeyExchange in the server's first flight (point 04 00)", zeroPoint);

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(null) { CipherSuite = 0xc018 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, zeroPoint)));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.BrainpoolP256r1)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1)]
    public void ABrainpoolKeyShareThatIsNotAPointOfItsCurveIsIllegal(int group)
    {
        Tls12ClientHandshake client = Client(EverySuite with { SupportedGroups = [(ushort)group] });
        Diagnostics.Arrange("server's first flight", $"group 0x{group:x4}, the point's last byte with its low bit flipped");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(null) { CipherSuite = 0xc018, EcdheGroup = (ushort)group }, flight => ReplaceEcdhePoint(flight, point => point[^1] ^= 0x01));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void AnX448KeyShareGivingAnAllZeroSharedSecretIsIllegal()
    {
        Tls12ClientHandshake client = Client(EverySuite with { SupportedGroups = [TlsNamedGroup.X448] });
        Diagnostics.Arrange("server's first flight", "x448, the point all zero");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(null) { CipherSuite = 0xc018, EcdheGroup = TlsNamedGroup.X448 }, flight => ReplaceEcdhePoint(flight, point => point.AsSpan().Clear()));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void ATlsOneThreeOnlyBrainpoolSchemeInATlsOneTwoServerKeyExchangeIsIllegal()
    {
        const ushort EcdsaBrainpoolP256r1Tls13Sha256 = 0x081a;
        Tls12TestServer server = new(TestServerCredential.Brainpool(BrainpoolCurve.BrainpoolP256r1, TlsSignatureScheme.BrainpoolP256r1Oid, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", $"a ServerKeyExchange naming TLS 1.3-only scheme 0x{EcdsaBrainpoolP256r1Tls13Sha256:x4}");

        Tls12HandshakeOutput output = Run(client, server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls12, message => message with { SignatureAlgorithm = EcdsaBrainpoolP256r1Tls13Sha256 }));

        Diagnostics.Assert("0x081a is a TLS 1.2 scheme", false, TlsSignatureScheme.IsTls12Scheme(EcdsaBrainpoolP256r1Tls13Sha256));
        Assert.IsFalse(TlsSignatureScheme.IsTls12Scheme(EcdsaBrainpoolP256r1Tls13Sha256));
        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void ABadBrainpoolServerKeyExchangeSignatureIsADecryptError()
    {
        Tls12TestServer server = new(TestServerCredential.Brainpool(BrainpoolCurve.BrainpoolP256r1, TlsSignatureScheme.BrainpoolP256r1Oid, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Diagnostics.Arrange("server's first flights", "brainpoolP256r1 key; the signature with one bit flipped, then the signature 30 00");

        Tls12HandshakeOutput corrupted = Run(Client(), server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls12, message => message with { Signature = Corrupt(message.Signature!) }));
        Tls12HandshakeOutput notDer = Run(Client(), server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls12, message => message with { Signature = [0x30, 0x00] }));

        AssertFails(TlsAlertDescription.DecryptError, corrupted);
        AssertFails(TlsAlertDescription.DecryptError, notDer);
    }

    [TestMethod]
    [DataRow("ecdsa", TlsSignatureScheme.EcdsaSha224, (ushort)0xc02b)]
    [DataRow("brainpool", TlsSignatureScheme.EcdsaSha224, (ushort)0xc02b)]
    [DataRow("rsa", TlsSignatureScheme.RsaPkcs1Sha224, (ushort)0xc02f)]
    [DataRow("ed448", TlsSignatureScheme.Ed448, (ushort)0xc02b)]
    public void ABadServerKeyExchangeSignatureWithASha224SchemeOrEd448IsADecryptError(string credential, int scheme, int cipherSuite)
    {
        TestServerCredential signer = credential switch
        {
            "ecdsa" => TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256),
            "brainpool" => TestServerCredential.Brainpool(BrainpoolCurve.BrainpoolP256r1, TlsSignatureScheme.BrainpoolP256r1Oid, TlsSignatureScheme.EcdsaSecp256r1Sha256),
            "rsa" => TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256),
            _ => TestServerCredential.Ed448(),
        };
        Tls12TestServer server = new(signer) { CipherSuite = (ushort)cipherSuite, SignatureScheme = (ushort)scheme };
        Tls12ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [(ushort)scheme] });
        Diagnostics.Arrange("server's first flight", $"{credential} key, scheme 0x{scheme:x4}, suite 0x{cipherSuite:x4}, the signature with one bit flipped");

        Tls12HandshakeOutput output = Run(client, server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls12, message => message with { Signature = Corrupt(message.Signature!) }));

        AssertFails(TlsAlertDescription.DecryptError, output);
    }

    [TestMethod]
    public void ABrainpoolCertificateKeyOfTheWrongLengthIsABadCertificate()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(TlsSignatureScheme.EcPublicKeyOid);
                writer.WriteObjectIdentifier(TlsSignatureScheme.BrainpoolP256r1Oid);
            }

            writer.WriteBitString(new byte[64]);
        }

        byte[] subjectPublicKeyInfo = writer.Encode();
        Diagnostics.Arrange("certificate key", "brainpoolP256r1 with 64 bytes of key instead of a 65-byte point");
        Diagnostics.Bytes("brainpoolP256r1 SubjectPublicKeyInfo with a 64-byte key", subjectPublicKeyInfo);

        TlsCertificatePublicKey key = TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(subjectPublicKeyInfo);
        TlsAlertDescription? verdict = key.VerifySignature(TlsSignatureScheme.FindTls12Rule(TlsSignatureScheme.EcdsaSecp256r1Sha256), [1], [0x30, 0x00]);
        Diagnostics.Act("alert from verifying a signature", verdict);

        Diagnostics.Assert("curve", TlsSignatureScheme.BrainpoolP256r1Oid, key.CurveOid);
        Diagnostics.Assert("alert", TlsAlertDescription.BadCertificate, verdict);
        Assert.AreEqual(TlsSignatureScheme.BrainpoolP256r1Oid, key.CurveOid);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, verdict);
    }

    [TestMethod]
    public void DheParametersThatAreNoGroupOrAnInvalidPublicValueAreIllegal()
    {
        byte[] prime = FiniteFieldDiffieHellmanGroup.Ffdhe2048.Prime.ToArray();
        byte[] evenPrime = [.. prime[..^1], 0xfe];
        byte[] notAGroup = new Tls12ServerKeyExchange(new Tls12DheParameters(evenPrime, [2], [5]), null, null).Encode();
        byte[] badPublicValue = new Tls12ServerKeyExchange(new Tls12DheParameters(prime, [2], [1]), null, null).Encode();
        Diagnostics.Arrange("server's first flights", "ffdhe2048 with an even prime and Ys 5, then ffdhe2048 with Ys 1");

        Tls12HandshakeOutput first = Run(Client(EverySuite), new Tls12TestServer(null) { CipherSuite = 0x00a6 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, notAGroup));
        Tls12HandshakeOutput second = Run(Client(EverySuite), new Tls12TestServer(null) { CipherSuite = 0x00a6 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, badPublicValue));

        AssertFails(TlsAlertDescription.IllegalParameter, first);
        AssertFails(TlsAlertDescription.IllegalParameter, second);
    }

    [TestMethod]
    public void ACompositeDhePrimeThatMakesTheSharedSecretZeroIsIllegal()
    {
        // p = q^2 and Ys = q, so Ys^x mod p is zero for any x of two or more.
        BigInteger q = (BigInteger.One << 512) + 1;
        byte[] square = (q * q).ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] zeroSecret = new Tls12ServerKeyExchange(new Tls12DheParameters(square, [2], q.ToByteArray(isUnsigned: true, isBigEndian: true)), null, null).Encode();
        Diagnostics.Arrange("server's first flight", "p = (2^512 + 1)^2 and Ys = 2^512 + 1");

        Tls12HandshakeOutput output = Run(Client(EverySuite), new Tls12TestServer(null) { CipherSuite = 0x00a6 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, zeroSecret));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    [DataRow(TlsExtensionType.ExtendedMasterSecret)]
    [DataRow(TlsExtensionType.SessionTicket)]
    public void AnEchoThatShouldBeEmptyButCarriesDataIsADecodeError(TlsExtensionType type)
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", $"a ServerHello echoing {type} with the body 00");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [.. hello.Extensions.Where(extension => extension.Type != type), new TlsExtension(type, [0])] }));

        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void ADheGroupBelowOneThousandTwentyFourBitsIsAHandshakeFailure()
    {
        Tls12ClientHandshake client = Client(EverySuite);
        Diagnostics.Arrange("server's first flight", "anonymous DHE suite 0x00a6 with the 768-bit group 1");

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(client, new Tls12TestServer(null) { CipherSuite = 0x00a6, DheGroup = FiniteFieldDiffieHellmanGroup.Group1 }));
    }

    [TestMethod]
    public void AMalformedCertificateRequestIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a CertificateRequest whose body is 01");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { RequestClientCertificate = true }, flight => Replace(flight, HandshakeType.CertificateRequest, new HandshakeMessage(HandshakeType.CertificateRequest, [1]).Encode()));

        AssertFails(TlsAlertDescription.DecodeError, output);
        Diagnostics.Assert("client certificate requested", false, client.ClientCertificateRequested);
        Assert.IsFalse(client.ClientCertificateRequested);
    }

    [TestMethod]
    public void AServerHelloDoneWithABodyIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "a ServerHelloDone whose body is 00");

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.ServerHelloDone, new HandshakeMessage(HandshakeType.ServerHelloDone, [0]).Encode())));
    }

    [TestMethod]
    public void AnRsaKeyThatDoesNotImportCannotTakeThePreMasterSecret()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "RSA key exchange suite 0x009c with an rsaEncryption key of bytes 01 02 03");

        AssertFails(TlsAlertDescription.BadCertificate, Run(client, new Tls12TestServer(TestServerCredential.Foreign(TlsSignatureScheme.RsaEncryptionOid, [1, 2, 3])) { CipherSuite = 0x009c }));
    }

    [TestMethod]
    public void AMalformedNewSessionTicketIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's final flight", "a NewSessionTicket whose body is 00");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { IssueTicket = true }, replaceFinalFlight: flight => Replace(flight, HandshakeType.NewSessionTicket, new HandshakeMessage(HandshakeType.NewSessionTicket, [0]).Encode()));

        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void AChangeCipherSpecInsteadOfAPromisedTicketIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's final flight", "the promised NewSessionTicket removed");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { IssueTicket = true }, replaceFinalFlight: flight => Remove(flight, HandshakeType.NewSessionTicket));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void ABadFinishedIsADecryptError()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's final flight", "the Finished tampered with");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => Tamper(flight, HandshakeType.Finished));

        AssertFails(TlsAlertDescription.DecryptError, output);
        Diagnostics.Assert("session", null, client.Session);
        Assert.IsNull(client.Session);
    }

    [TestMethod]
    public void AHandshakeMessageInPlaceOfTheFinishedIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's final flight", "a NewSessionTicket in place of the Finished");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => Replace(flight, HandshakeType.Finished, new Tls12NewSessionTicket(0, [1]).Encode()));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AChangeCipherSpecBeforeTheServerFlightEndsIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's first flight", "the ServerHello, then a ChangeCipherSpec");

        AssertFails(TlsAlertDescription.UnexpectedMessage, Run(client, new Tls12TestServer(Ecdsa()), flight => [flight[0], Tls12OutgoingMessage.ChangeCipherSpec]));
    }

    [TestMethod]
    public void AChangeCipherSpecInsideAHandshakeMessageIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's final flight", "a partial handshake message 14 00 before the ChangeCipherSpec");

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => [new(TlsContentType.Handshake, [20, 0]), .. flight]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { 1, 1 })]
    public void AChangeCipherSpecOtherThanTheByteOneIsADecodeError(byte[] content)
    {
        Tls12ClientHandshake client = Client();
        Diagnostics.Arrange("server's final flight", "a ChangeCipherSpec whose content is not the single byte 01");
        Diagnostics.Bytes("ChangeCipherSpec in the server's final flight", content);

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => [.. flight.Select(message => message.ContentType == TlsContentType.ChangeCipherSpec ? new Tls12OutgoingMessage(TlsContentType.ChangeCipherSpec, content) : message)]);

        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void AHandshakeMessageAfterCompletionIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        using (Diagnostics.Phase("handshake"))
        {
            Run(client, new Tls12TestServer(Ecdsa()));
        }

        Diagnostics.Arrange("after the completed handshake", "a NewSessionTicket");

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.ReceiveHandshake(new Tls12NewSessionTicket(0, [1]).Encode()));
    }

    private static TestServerCredential Ecdsa() => TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);

    private static TestServerCredential Rsa() => TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256);

    private static byte[] Corrupt(byte[] signature)
    {
        byte[] corrupted = [.. signature];
        corrupted[^8] ^= 0x01;
        return corrupted;
    }

    private static List<Tls12OutgoingMessage> RewriteServerHello(List<Tls12OutgoingMessage> flight, Func<ServerHello, ServerHello> rewrite) =>
        Replace(flight, HandshakeType.ServerHello, rewrite(ServerHello.Decode(Body(flight[0])).Value).Encode());

    /// <summary>Rewrites the server's point in an anonymous ECDHE ServerKeyExchange.</summary>
    private static List<Tls12OutgoingMessage> ReplaceEcdhePoint(List<Tls12OutgoingMessage> flight, Action<byte[]> rewrite)
    {
        Tls12OutgoingMessage original = flight.First(message => (HandshakeType)message.Bytes[0] == HandshakeType.ServerKeyExchange);
        Tls12EcdheParameters parameters = (Tls12EcdheParameters)Tls12ServerKeyExchange.Decode(Body(original), Tls12KeyExchange.Ecdhe, false, true).Value.Parameters;
        byte[] point = [.. parameters.PublicKey];
        rewrite(point);
        return Replace(flight, HandshakeType.ServerKeyExchange, new Tls12ServerKeyExchange(parameters with { PublicKey = point }, null, null).Encode());
    }

    private static List<Tls12OutgoingMessage> ReplaceServerKeyExchange(List<Tls12OutgoingMessage> flight, TlsProtocolVersion version, Func<Tls12ServerKeyExchange, Tls12ServerKeyExchange> rewrite, Tls12KeyExchange keyExchange = Tls12KeyExchange.Ecdhe)
    {
        Tls12OutgoingMessage original = flight.First(message => (HandshakeType)message.Bytes[0] == HandshakeType.ServerKeyExchange);
        Tls12ServerKeyExchange decoded = Tls12ServerKeyExchange.Decode(Body(original), keyExchange, true, version == TlsProtocolVersion.Tls12).Value;
        return Replace(flight, HandshakeType.ServerKeyExchange, rewrite(decoded).Encode());
    }

    /// <summary>
    /// Writes the alert the client sent, where it came from and whether the handshake
    /// completed, then asserts the handshake failed with <paramref name="alert" /> through
    /// <see cref="Tls12HandshakeDriver.AssertFails" />.
    /// </summary>
    private void AssertFails(TlsAlertDescription alert, Tls12HandshakeOutput output)
    {
        Diagnostics.Act("failure", output.Failure is null ? "none" : $"alert {output.Failure.Alert} ({(int)output.Failure.Alert}), {output.Failure.Origin}");
        Diagnostics.Assert("alert sent", alert, output.Failure?.Alert);
        Diagnostics.Assert("handshake complete", false, output.IsComplete);
        Tls12HandshakeDriver.AssertFails(alert, output);
    }
}
