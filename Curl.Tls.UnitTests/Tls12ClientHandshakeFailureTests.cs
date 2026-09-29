using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;
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

    [TestMethod]
    public void ConstructorRejectsNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Tls12ClientHandshake(null!, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Tls12ClientHandshake(DefaultSettings, null!, new RecordingCertificateVerifier()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Tls12ClientHandshake(DefaultSettings, SystemTlsRandomSource.Instance, null!));
    }

    [TestMethod]
    public void ConstructorRejectsSettingsThatCannotDriveAHandshake()
    {
        Tls12Session session = new(TlsProtocolVersion.Tls12, 0xc02b, [1], null, 0, new byte[48], true);
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MinimumVersion = (TlsProtocolVersion)0x0300 }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MaximumVersion = (TlsProtocolVersion)0x0304 }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls12, MaximumVersion = TlsProtocolVersion.Tls11 }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [Tls12CipherSuite.EmptyRenegotiationInfoScsv] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CipherSuites = [0xc02b, 0x1301] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.Ffdhe2048] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SignatureAlgorithms = [0xfefe] }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SessionToResume = session with { Version = TlsProtocolVersion.Tls11 } }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { MaximumVersion = TlsProtocolVersion.Tls11, MinimumVersion = TlsProtocolVersion.Tls10, SessionToResume = session }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SessionToResume = session with { CipherSuite = 0x0001 } }));
    }

    [TestMethod]
    public void StartingTwiceThrows()
    {
        Tls12ClientHandshake client = Client();
        client.Start();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.Start());
    }

    [TestMethod]
    public void ReceivingBeforeStartingThrows()
    {
        Tls12ClientHandshake client = Client();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.ReceiveHandshake([2]));
        Assert.ThrowsExactly<InvalidOperationException>(() => client.ReceiveChangeCipherSpec([1]));
    }

    [TestMethod]
    public void AfterAFailureEveryCallReturnsTheSameFailureAndNothingElse()
    {
        Tls12ClientHandshake client = Client();
        client.Start();
        Tls12HandshakeOutput failed = client.ReceiveHandshake([3, 0, 0, 0]);

        Tls12HandshakeOutput again = client.ReceiveHandshake(new HandshakeMessage(HandshakeType.ServerHello, []).Encode());
        Tls12HandshakeOutput changeCipherSpec = client.ReceiveChangeCipherSpec([1]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, failed);
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

        AssertFails(TlsAlertDescription.DecodeError, client.ReceiveHandshake(new HandshakeMessage(HandshakeType.HelloRequest, [0]).Encode()));
    }

    [TestMethod]
    public void AnythingButAServerHelloFirstIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        client.Start();

        AssertFails(TlsAlertDescription.UnexpectedMessage, client.ReceiveHandshake(new Tls12CertificateMessage([]).Encode()));
    }

    [TestMethod]
    public void AMalformedServerHelloIsADecodeError()
    {
        Tls12ClientHandshake client = Client();
        client.Start();

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

        AssertFails(TlsAlertDescription.ProtocolVersion, Run(client, testServer));
        Assert.IsNull(client.Version);
    }

    [TestMethod]
    public void AVersionThatIsNoTlsVersionIsAProtocolVersionAlert()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.ProtocolVersion, Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(flight, hello => hello with { LegacyVersion = 0x0300 })));
    }

    [TestMethod]
    public void TheDowngradeSentinelFromATlsOneTwoClientIsIllegal()
    {
        Tls12TestServer server = new(Rsa()) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc013, SendDowngradeSentinel = true };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10 });

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

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { CipherSuite = (ushort)cipherSuite, LegacyVersion = (ushort)version, LegacyCompressionMethod = compression }));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void AServerHelloExtensionThatWasNotOfferedIsUnsupported()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [.. hello.Extensions, new TlsExtension(TlsExtensionType.KeyShare, [])] }));

        AssertFails(TlsAlertDescription.UnsupportedExtension, output);
    }

    [TestMethod]
    public void AServerWithoutSecureRenegotiationIsRefused()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(client, new Tls12TestServer(Ecdsa()) { SendRenegotiationInfo = false }));
    }

    [TestMethod]
    [DataRow(new byte[] { 1 }, TlsAlertDescription.DecodeError)]
    [DataRow(new byte[] { 1, 7 }, TlsAlertDescription.HandshakeFailure)]
    public void ARenegotiationInfoThatIsMalformedOrNotEmptyFails(byte[] data, TlsAlertDescription alert)
    {
        Tls12ClientHandshake client = Client();

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

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [.. hello.Extensions, new TlsExtension(TlsExtensionType.ApplicationLayerProtocolNegotiation, data)] }));

        AssertFails(alert, output);
        Assert.IsNull(client.ApplicationProtocol);
    }

    [TestMethod]
    public void AResumptionOnAnotherVersionOrSuiteIsIllegal()
    {
        Tls12TestSessionCache sessions = new();
        TestServerCredential credential = Rsa();
        Tls12ClientHandshake first = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11, OfferSessionTicket = false });
        Run(first, new Tls12TestServer(credential) { CipherSuite = 0xc013, Sessions = sessions });
        Tls12ClientSettings resume = DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11, OfferSessionTicket = false, SessionToResume = first.Session };

        Tls12HandshakeOutput otherVersion = Run(Client(resume), new Tls12TestServer(credential) { CipherSuite = 0xc013, Version = TlsProtocolVersion.Tls11, Sessions = sessions });
        Tls12HandshakeOutput otherSuite = Run(Client(resume), new Tls12TestServer(credential) { CipherSuite = 0xc014, Sessions = sessions });

        AssertFails(TlsAlertDescription.IllegalParameter, otherVersion);
        AssertFails(TlsAlertDescription.IllegalParameter, otherSuite);
    }

    [TestMethod]
    public void AResumptionThatChangesTheExtendedMasterSecretIsAHandshakeFailure()
    {
        Tls12TestServer server = new(Ecdsa());
        Tls12ClientHandshake first = Client(DefaultSettings with { OfferSessionTicket = false });
        Run(first, server);
        Tls12ClientHandshake second = Client(DefaultSettings with { OfferSessionTicket = false, SessionToResume = first.Session! with { ExtendedMasterSecret = false } });

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(second, server));
    }

    [TestMethod]
    public void ARepeatedCertificateIsUnexpected()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => [flight[0], flight[1], .. flight[1..]]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AServerKeyExchangeForRsaKeyExchangeIsUnexpected()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Rsa()) { CipherSuite = 0xc02f }, flight => RewriteServerHello(flight, hello => hello with { CipherSuite = 0x009c }));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void ACertificateStatusThatWasNotPromisedIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        byte[] status = new HandshakeMessage(HandshakeType.CertificateStatus, StatusRequestExtension.EncodeOcspResponse([1]).Data).Encode();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => [flight[0], flight[1], new(TlsContentType.Handshake, status), .. flight[2..]]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void ACertificateInAnAnonymousSuiteIsUnexpected()
    {
        Tls12ClientHandshake client = Client(EverySuite);

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

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => [.. Remove(flight, missing), new(TlsContentType.Handshake, new Finished(new byte[12]).Encode())]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AMalformedCertificateIsADecodeError()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.Certificate, new HandshakeMessage(HandshakeType.Certificate, [0, 0, 9]).Encode())));
    }

    [TestMethod]
    public void AnEmptyServerCertificateIsADecodeError()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.Certificate, new Tls12CertificateMessage([]).Encode())));
    }

    [TestMethod]
    public void ACertificateThatDoesNotParseIsABadCertificate()
    {
        Tls12ClientHandshake client = Client();

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

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(client, new Tls12TestServer(key) { CipherSuite = (ushort)cipherSuite }));
    }

    [TestMethod]
    public void ARejectedChainFailsWithTheVerdictsAlertAndReason()
    {
        object reason = new();
        Tls12ClientHandshake client = Client(verifier: new RecordingCertificateVerifier(ServerCertificateVerdict.Rejected(reason)));

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()));

        AssertFails(TlsAlertDescription.BadCertificate, output);
        Assert.AreSame(reason, output.Failure!.CertificateRejection);
    }

    [TestMethod]
    public void AMalformedCertificateStatusIsRefused()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { RequestOcspStatus = true });

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { OcspResponse = [1] }, flight => Replace(flight, HandshakeType.CertificateStatus, new HandshakeMessage(HandshakeType.CertificateStatus, [2, 0, 0, 1, 1]).Encode()));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
        Assert.IsNull(client.OcspResponse);
    }

    [TestMethod]
    public void AMalformedServerKeyExchangeIsADecodeError()
    {
        Tls12ClientHandshake client = Client();

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

        Tls12HandshakeOutput output = Run(client, server, flight => ReplaceServerKeyExchange(flight, version, message => message with { Signature = Corrupt(message.Signature!) }, Tls12KeyExchange.Dhe));

        AssertFails(TlsAlertDescription.DecryptError, output);
    }

    [TestMethod]
    public void ALegacyRsaSignatureOfTheWrongLengthOrOutOfRangeIsADecryptError()
    {
        Tls12TestServer server = new(Rsa()) { Version = TlsProtocolVersion.Tls10, CipherSuite = 0xc013 };
        Tls12ClientSettings settings = DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10 };

        Tls12HandshakeOutput shortSignature = Run(Client(settings), server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls10, message => message with { Signature = message.Signature![1..] }));
        Tls12HandshakeOutput tooLarge = Run(Client(settings), server, flight => ReplaceServerKeyExchange(flight, TlsProtocolVersion.Tls10, message => message with { Signature = [.. Enumerable.Repeat((byte)0xff, 256)] }));

        AssertFails(TlsAlertDescription.DecryptError, shortSignature);
        AssertFails(TlsAlertDescription.DecryptError, tooLarge);
    }

    [TestMethod]
    public void AServerKeyExchangeSignedWithASchemeNotOfferedIsIllegal()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [TlsSignatureScheme.EcdsaSecp384r1Sha384] });

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(Ecdsa())));
    }

    [TestMethod]
    public void AnEd25519KeyHasNoTlsOneOneSignature()
    {
        Tls12TestServer server = new(TestServerCredential.Ed25519()) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc009 };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11 });

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, server));
    }

    [TestMethod]
    public void AnExplicitCurveIsIllegal()
    {
        Tls12ClientHandshake client = Client(EverySuite);
        byte[] explicitCurve = new HandshakeMessage(HandshakeType.ServerKeyExchange, [1, 0, 0x1d, 1, 9]).Encode();

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(null) { CipherSuite = 0xc018 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, explicitCurve)));
    }

    [TestMethod]
    public void AGroupThatWasNotOfferedIsIllegal()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.Secp256r1] });

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(Ecdsa()) { EcdheGroup = TlsNamedGroup.X25519 }));
    }

    [TestMethod]
    public void ADegenerateEcdhePointIsIllegal()
    {
        Tls12ClientHandshake client = Client(EverySuite);
        byte[] zeroPoint = new Tls12ServerKeyExchange(new Tls12EcdheParameters(TlsNamedGroup.Secp256r1, [4, 0]), null, null).Encode();

        AssertFails(TlsAlertDescription.IllegalParameter, Run(client, new Tls12TestServer(null) { CipherSuite = 0xc018 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, zeroPoint)));
    }

    [TestMethod]
    public void DheParametersThatAreNoGroupOrAnInvalidPublicValueAreIllegal()
    {
        byte[] prime = FiniteFieldDiffieHellmanGroup.Ffdhe2048.Prime.ToArray();
        byte[] evenPrime = [.. prime[..^1], 0xfe];
        byte[] notAGroup = new Tls12ServerKeyExchange(new Tls12DheParameters(evenPrime, [2], [5]), null, null).Encode();
        byte[] badPublicValue = new Tls12ServerKeyExchange(new Tls12DheParameters(prime, [2], [1]), null, null).Encode();

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

        Tls12HandshakeOutput output = Run(Client(EverySuite), new Tls12TestServer(null) { CipherSuite = 0x00a6 }, flight => Replace(flight, HandshakeType.ServerKeyExchange, zeroSecret));

        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    [DataRow(TlsExtensionType.ExtendedMasterSecret)]
    [DataRow(TlsExtensionType.SessionTicket)]
    public void AnEchoThatShouldBeEmptyButCarriesDataIsADecodeError(TlsExtensionType type)
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), flight => RewriteServerHello(
            flight,
            hello => hello with { Extensions = [.. hello.Extensions.Where(extension => extension.Type != type), new TlsExtension(type, [0])] }));

        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void ADheGroupBelowOneThousandTwentyFourBitsIsAHandshakeFailure()
    {
        Tls12ClientHandshake client = Client(EverySuite);

        AssertFails(TlsAlertDescription.HandshakeFailure, Run(client, new Tls12TestServer(null) { CipherSuite = 0x00a6, DheGroup = FiniteFieldDiffieHellmanGroup.Group1 }));
    }

    [TestMethod]
    public void AMalformedCertificateRequestIsADecodeError()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { RequestClientCertificate = true }, flight => Replace(flight, HandshakeType.CertificateRequest, new HandshakeMessage(HandshakeType.CertificateRequest, [1]).Encode()));

        AssertFails(TlsAlertDescription.DecodeError, output);
        Assert.IsFalse(client.ClientCertificateRequested);
    }

    [TestMethod]
    public void AServerHelloDoneWithABodyIsADecodeError()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.DecodeError, Run(client, new Tls12TestServer(Ecdsa()), flight => Replace(flight, HandshakeType.ServerHelloDone, new HandshakeMessage(HandshakeType.ServerHelloDone, [0]).Encode())));
    }

    [TestMethod]
    public void AnRsaKeyThatDoesNotImportCannotTakeThePreMasterSecret()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.BadCertificate, Run(client, new Tls12TestServer(TestServerCredential.Foreign(TlsSignatureScheme.RsaEncryptionOid, [1, 2, 3])) { CipherSuite = 0x009c }));
    }

    [TestMethod]
    public void AMalformedNewSessionTicketIsADecodeError()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { IssueTicket = true }, replaceFinalFlight: flight => Replace(flight, HandshakeType.NewSessionTicket, new HandshakeMessage(HandshakeType.NewSessionTicket, [0]).Encode()));

        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void AChangeCipherSpecInsteadOfAPromisedTicketIsUnexpected()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()) { IssueTicket = true }, replaceFinalFlight: flight => Remove(flight, HandshakeType.NewSessionTicket));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void ABadFinishedIsADecryptError()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => Tamper(flight, HandshakeType.Finished));

        AssertFails(TlsAlertDescription.DecryptError, output);
        Assert.IsNull(client.Session);
    }

    [TestMethod]
    public void AHandshakeMessageInPlaceOfTheFinishedIsUnexpected()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => Replace(flight, HandshakeType.Finished, new Tls12NewSessionTicket(0, [1]).Encode()));

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    public void AChangeCipherSpecBeforeTheServerFlightEndsIsUnexpected()
    {
        Tls12ClientHandshake client = Client();

        AssertFails(TlsAlertDescription.UnexpectedMessage, Run(client, new Tls12TestServer(Ecdsa()), flight => [flight[0], Tls12OutgoingMessage.ChangeCipherSpec]));
    }

    [TestMethod]
    public void AChangeCipherSpecInsideAHandshakeMessageIsUnexpected()
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => [new(TlsContentType.Handshake, [20, 0]), .. flight]);

        AssertFails(TlsAlertDescription.UnexpectedMessage, output);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { 1, 1 })]
    public void AChangeCipherSpecOtherThanTheByteOneIsADecodeError(byte[] content)
    {
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, new Tls12TestServer(Ecdsa()), replaceFinalFlight: flight => [.. flight.Select(message => message.ContentType == TlsContentType.ChangeCipherSpec ? new Tls12OutgoingMessage(TlsContentType.ChangeCipherSpec, content) : message)]);

        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void AHandshakeMessageAfterCompletionIsUnexpected()
    {
        Tls12ClientHandshake client = Client();
        Run(client, new Tls12TestServer(Ecdsa()));

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

    private static List<Tls12OutgoingMessage> ReplaceServerKeyExchange(List<Tls12OutgoingMessage> flight, TlsProtocolVersion version, Func<Tls12ServerKeyExchange, Tls12ServerKeyExchange> rewrite, Tls12KeyExchange keyExchange = Tls12KeyExchange.Ecdhe)
    {
        Tls12OutgoingMessage original = flight.First(message => (HandshakeType)message.Bytes[0] == HandshakeType.ServerKeyExchange);
        Tls12ServerKeyExchange decoded = Tls12ServerKeyExchange.Decode(Body(original), keyExchange, true, version == TlsProtocolVersion.Tls12).Value;
        return Replace(flight, HandshakeType.ServerKeyExchange, rewrite(decoded).Encode());
    }
}
