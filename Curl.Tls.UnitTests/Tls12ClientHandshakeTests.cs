using System.Security.Cryptography;
using Curl.Cryptography;
using static Curl.Tls.Tls12HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// Completes TLS 1.2, 1.1 and 1.0 handshakes against the in-memory server for every key
/// exchange and authentication, every bulk cipher family, every ECDHE group, the
/// ServerKeyExchange signature schemes, resumption by session ID and by ticket, ALPN, a
/// stapled OCSP response, the extended master secret, encrypt-then-MAC and client
/// certificates; each time the client's key block is the server's.
/// </summary>
[TestClass]
public sealed class Tls12ClientHandshakeTests
{
    private static readonly Tls12ClientSettings EverySuite = DefaultSettings with { CipherSuites = [.. Tls12CipherSuite.All.Select(suite => suite.Code)] };

    [TestMethod]
    [DataRow((ushort)0xc02b, "ecdsa")]
    [DataRow((ushort)0xc02f, "rsa")]
    [DataRow((ushort)0x009e, "rsa")]
    [DataRow((ushort)0x009c, "rsa")]
    [DataRow((ushort)0x00a6, "none")]
    [DataRow((ushort)0xc018, "none")]
    public void HandshakeCompletesForEachKeyExchangeAndAuthentication(int cipherSuite, string credential)
    {
        RecordingCertificateVerifier verifier = new();
        Tls12TestServer server = new(Credential(credential)) { CipherSuite = (ushort)cipherSuite };
        Tls12ClientHandshake client = Client(EverySuite, verifier);

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.AreEqual(cipherSuite, client.CipherSuite!.Code);
        Assert.AreEqual(TlsProtocolVersion.Tls12, client.Version);
        Assert.IsFalse(client.IsResumed);
        Assert.HasCount(credential == "none" ? 0 : 1, verifier.Presented);
        Assert.HasCount(credential == "none" ? 0 : 1, client.ServerCertificates);
    }

    [TestMethod]
    [DataRow((ushort)0xc02c)]
    [DataRow((ushort)0xcca9)]
    [DataRow((ushort)0xc023)]
    [DataRow((ushort)0xc024)]
    [DataRow((ushort)0xc00a)]
    [DataRow((ushort)0xc072)]
    [DataRow((ushort)0xc05c)]
    [DataRow((ushort)0xc008)]
    [DataRow((ushort)0xc006)]
    public void HandshakeCompletesWithEachBulkCipherAndItsKeysProtectRecords(int cipherSuite)
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256)) { CipherSuite = (ushort)cipherSuite };
        Tls12ClientHandshake client = Client(EverySuite);

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        using Tls12RecordWriteState write = Tls12RecordWriteState.Create(client.RecordProtection!, client.KeyBlock!.ClientWrite, SystemTlsRandomSource.Instance);
        using Tls12RecordReadState read = Tls12RecordReadState.Create(client.RecordProtection!, server.KeyBlock.ClientWrite);
        byte[] record = write.Protect(TlsContentType.ApplicationData, "GET / HTTP/1.1"u8);
        CollectionAssert.AreEqual("GET / HTTP/1.1"u8.ToArray(), read.Unprotect(TlsContentType.ApplicationData, record.AsSpan(5)).Value);
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519)]
    [DataRow(TlsNamedGroup.Secp256r1)]
    [DataRow(TlsNamedGroup.Secp384r1)]
    [DataRow(TlsNamedGroup.Secp521r1)]
    public void HandshakeCompletesOnEachEcdheGroup(int group)
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { CipherSuite = 0xc02f, EcdheGroup = (ushort)group };
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.AreEqual<ushort?>((ushort)group, client.NegotiatedGroup);
    }

    [TestMethod]
    public void DheTakesTheServersGroupAndReportsNoNamedGroup()
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { CipherSuite = 0x009f, DheGroup = FiniteFieldDiffieHellmanGroup.Group14 };
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.IsNull(client.NegotiatedGroup);
    }

    [TestMethod]
    [DataRow("ecdsa384", TlsSignatureScheme.EcdsaSecp384r1Sha384, (ushort)0xc02b)]
    [DataRow("ecdsa256", TlsSignatureScheme.EcdsaSecp384r1Sha384, (ushort)0xc02b)]
    [DataRow("ecdsa256", TlsSignatureScheme.EcdsaSha1, (ushort)0xc02b)]
    [DataRow("ed25519", TlsSignatureScheme.Ed25519, (ushort)0xc02b)]
    [DataRow("rsa", TlsSignatureScheme.RsaPkcs1Sha1, (ushort)0xc02f)]
    [DataRow("rsa", TlsSignatureScheme.RsaPkcs1Sha384, (ushort)0xc02f)]
    [DataRow("rsa", TlsSignatureScheme.RsaPkcs1Sha512, (ushort)0xc02f)]
    [DataRow("rsa", TlsSignatureScheme.RsaPssRsaeSha256, (ushort)0x009e)]
    public void ServerKeyExchangeVerifiesWithEachSignatureScheme(string credential, int scheme, int cipherSuite)
    {
        Tls12TestServer server = new(Credential(credential)) { CipherSuite = (ushort)cipherSuite, SignatureScheme = (ushort)scheme };
        Tls12ClientHandshake client = Client();

        AssertCompletesWithServerKeys(client, server, Run(client, server));
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls10, (ushort)0xc013, "rsa")]
    [DataRow(TlsProtocolVersion.Tls10, (ushort)0x002f, "rsa")]
    [DataRow(TlsProtocolVersion.Tls11, (ushort)0xc009, "ecdsa")]
    [DataRow(TlsProtocolVersion.Tls11, (ushort)0x0033, "rsa")]
    [DataRow(TlsProtocolVersion.Tls10, (ushort)0x0034, "none")]
    public void TlsOneZeroAndOneOneCompleteWhenTheRangeAllowsThem(TlsProtocolVersion version, int cipherSuite, string credential)
    {
        Tls12TestServer server = new(Credential(credential)) { Version = version, CipherSuite = (ushort)cipherSuite };
        Tls12ClientHandshake client = Client(EverySuite with { MinimumVersion = TlsProtocolVersion.Tls10 });

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.AreEqual(version, client.Version);
        Assert.AreEqual(version, client.Session!.Version);
    }

    [TestMethod]
    public void AClientCappedAtTlsOneOneOffersItAndNoSignatureAlgorithms()
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc014 };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls11, MaximumVersion = TlsProtocolVersion.Tls11 });

        Tls12HandshakeOutput hello = client.Start();
        Tls12HandshakeOutput clientFlight = Deliver(client, server.Answer(hello.MessagesToSend[0].Bytes));
        Tls12HandshakeOutput output = Deliver(client, server.ReceiveClientFlight(clientFlight.MessagesToSend));

        ClientHello decoded = ClientHello.Decode(Body(hello.MessagesToSend[0])).Value;
        Assert.AreEqual((ushort)0x0302, decoded.LegacyVersion);
        Assert.IsFalse(decoded.Extensions.Any(extension => extension.Type == TlsExtensionType.SignatureAlgorithms));
        AssertCompletesWithServerKeys(client, server, output);
    }

    [TestMethod]
    public void ADowngradeSentinelIsIgnoredWhenTheClientDoesNotOfferTlsOneTwo()
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc014, SendDowngradeSentinel = true };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10, MaximumVersion = TlsProtocolVersion.Tls11 });

        AssertCompletesWithServerKeys(client, server, Run(client, server));
    }

    [TestMethod]
    public void TheClientHelloCarriesTheExtensionsInOpenSslsOrder()
    {
        Tls12ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2"], RequestOcspStatus = true });

        ClientHello hello = ClientHello.Decode(Body(client.Start().MessagesToSend[0])).Value;

        CollectionAssert.AreEqual(
            new[]
            {
                TlsExtensionType.RenegotiationInfo, TlsExtensionType.ServerName, TlsExtensionType.EcPointFormats, TlsExtensionType.SupportedGroups,
                TlsExtensionType.SessionTicket, TlsExtensionType.StatusRequest, TlsExtensionType.ApplicationLayerProtocolNegotiation,
                TlsExtensionType.EncryptThenMac, TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.SignatureAlgorithms,
            },
            hello.Extensions.Select(extension => extension.Type).ToArray());
        Assert.IsEmpty(hello.LegacySessionId);
        Assert.AreEqual((ushort)0x0303, hello.LegacyVersion);
        CollectionAssert.AreEqual(new byte[] { 0 }, hello.LegacyCompressionMethods);
    }

    [TestMethod]
    public void AMinimalClientHelloLeavesOutEveryOptionalExtension()
    {
        Tls12ClientHandshake client = Client(new Tls12ClientSettings { OfferSessionTicket = false, OfferEncryptThenMac = false, OfferExtendedMasterSecret = false });

        ClientHello hello = ClientHello.Decode(Body(client.Start().MessagesToSend[0])).Value;

        CollectionAssert.AreEqual(
            new[] { TlsExtensionType.RenegotiationInfo, TlsExtensionType.EcPointFormats, TlsExtensionType.SupportedGroups, TlsExtensionType.SignatureAlgorithms },
            hello.Extensions.Select(extension => extension.Type).ToArray());
    }

    [TestMethod]
    public void TheRenegotiationSignalingSuiteMayBeOffered()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Tls12ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0xc02b, Tls12CipherSuite.EmptyRenegotiationInfoScsv] });

        AssertCompletesWithServerKeys(client, server, Run(client, server));
    }

    [TestMethod]
    public void AResumptionBySessionIdIsAnAbbreviatedHandshakeWithTheSameMasterSecret()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Tls12ClientHandshake first = Client(DefaultSettings with { OfferSessionTicket = false });
        Run(first, server);
        Tls12Session session = first.Session!;
        Tls12ClientHandshake second = Client(DefaultSettings with { OfferSessionTicket = false, SessionToResume = session });

        Tls12HandshakeOutput output = Run(second, server);

        AssertCompletesWithServerKeys(second, server, output);
        Assert.IsTrue(second.IsResumed);
        Assert.IsTrue(second.ExtendedMasterSecret);
        Assert.IsEmpty(second.ServerCertificates);
        CollectionAssert.AreEqual(session.SessionId, second.Session!.SessionId);
        CollectionAssert.AreEqual(session.MasterSecret, second.Session.MasterSecret);
        Assert.IsNull(second.Session.Ticket);
        Assert.AreEqual(0u, second.Session.TicketLifetimeHint);
        Assert.AreEqual(Tls12OutgoingMessage.ChangeCipherSpec, output.MessagesToSend[0]);
        Assert.AreEqual(HandshakeType.Finished, (HandshakeType)output.MessagesToSend[1].Bytes[0]);
    }

    [TestMethod]
    public void AResumptionByTicketOffersARandomSessionIdAndTakesTheNewTicket()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256)) { IssueTicket = true };
        Tls12ClientHandshake first = Client();
        Run(first, server);
        Tls12Session session = first.Session!;
        Tls12ClientHandshake second = Client(DefaultSettings with { SessionToResume = session });

        Tls12HandshakeOutput output = Run(second, server);

        AssertCompletesWithServerKeys(second, server, output);
        Assert.IsNotNull(session.Ticket);
        Assert.AreEqual(7200u, session.TicketLifetimeHint);
        Assert.IsTrue(second.IsResumed);
        Assert.HasCount(32, second.Session!.SessionId);
        CollectionAssert.AreNotEqual(session.Ticket, second.Session.Ticket);
    }

    [TestMethod]
    public void AResumptionByTicketWithoutANewTicketKeepsTheOldOne()
    {
        Tls12TestSessionCache sessions = new();
        TestServerCredential credential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);
        Tls12ClientHandshake first = Client();
        Run(first, new Tls12TestServer(credential) { IssueTicket = true, Sessions = sessions });
        Tls12ClientHandshake second = Client(DefaultSettings with { SessionToResume = first.Session });
        Tls12TestServer server = new(credential) { Sessions = sessions };

        Tls12HandshakeOutput output = Run(second, server);

        AssertCompletesWithServerKeys(second, server, output);
        Assert.IsTrue(second.IsResumed);
        CollectionAssert.AreEqual(first.Session!.Ticket, second.Session!.Ticket);
        Assert.AreEqual(7200u, second.Session.TicketLifetimeHint);
    }

    [TestMethod]
    public void AServerThatDoesNotKnowTheSessionRunsAFullHandshake()
    {
        TestServerCredential credential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);
        Tls12ClientHandshake first = Client(DefaultSettings with { OfferSessionTicket = false });
        Run(first, new Tls12TestServer(credential));
        Tls12ClientHandshake second = Client(DefaultSettings with { OfferSessionTicket = false, SessionToResume = first.Session });
        Tls12TestServer server = new(credential);

        Tls12HandshakeOutput output = Run(second, server);

        AssertCompletesWithServerKeys(second, server, output);
        Assert.IsFalse(second.IsResumed);
        CollectionAssert.AreNotEqual(first.Session!.SessionId, second.Session!.SessionId);
    }

    [TestMethod]
    public void ASessionWithNeitherIdNorTicketCannotBeResumed()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Tls12Session session = new(TlsProtocolVersion.Tls12, 0xc02b, [], null, 0, new byte[48], true);
        Tls12ClientHandshake client = Client(DefaultSettings with { SessionToResume = session });

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.IsFalse(client.IsResumed);
    }

    [TestMethod]
    public void TheServersApplicationProtocolIsReported()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256)) { ApplicationProtocol = "http/1.1" };
        Tls12ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2", "http/1.1"] });

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        Assert.AreEqual("http/1.1", client.ApplicationProtocol);
    }

    [TestMethod]
    public void AStapledOcspResponseGoesToTheVerifierWithTheChain()
    {
        RecordingCertificateVerifier verifier = new();
        using OcspTestPki pki = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] ocspResponse = pki.Response(now).Build();
        Tls12TestServer server = new(pki.LeafCredential) { OcspResponse = ocspResponse, IssuerCertificates = [pki.Ca.RawData] };
        Tls12ClientHandshake client = Client(DefaultSettings with { RequestOcspStatus = true, TimeProvider = new FixedTimeProvider(now) }, verifier);

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        CollectionAssert.AreEqual(ocspResponse, client.OcspResponse);
        CollectionAssert.AreEqual(ocspResponse, verifier.Presented[0].OcspResponse);
        Assert.AreEqual("localhost", verifier.Presented[0].HostName);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Good), client.CertificateStatus);
    }

    [TestMethod]
    public void AServerThatPromisesButOmitsTheCertificateStatusReachesTheStatusCheckWithNoResponse()
    {
        RecordingCertificateVerifier verifier = new();
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256)) { OcspResponse = [1], OmitCertificateStatus = true };
        Tls12ClientHandshake client = Client(DefaultSettings with { RequestOcspStatus = true }, verifier);

        Tls12HandshakeOutput output = Run(client, server);

        Assert.IsNull(client.OcspResponse);
        Assert.IsNull(verifier.Presented[0].OcspResponse);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.NoResponse), output.Failure!.CertificateStatusRejection);
    }

    [TestMethod]
    public void WithoutTheExtendedMasterSecretTheClassicMasterSecretIsUsed()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256)) { EchoExtendedMasterSecret = false };
        Tls12ClientHandshake client = Client();

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        Assert.IsFalse(client.ExtendedMasterSecret);
        Assert.IsFalse(client.Session!.ExtendedMasterSecret);
    }

    [TestMethod]
    [DataRow((ushort)0xc023, true, true)]
    [DataRow((ushort)0xc02b, true, false)]
    [DataRow((ushort)0xc023, false, false)]
    public void EncryptThenMacAppliesToCbcSuitesTheServerAgreedItFor(int cipherSuite, bool echoed, bool expected)
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256))
        {
            CipherSuite = (ushort)cipherSuite,
            EchoEncryptThenMac = echoed,
        };
        Tls12ClientHandshake client = Client(EverySuite);

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        Assert.AreEqual(expected, client.RecordProtection!.EncryptThenMac);
    }

    [TestMethod]
    [DataRow("ecdsa", TlsSignatureScheme.EcdsaSecp256r1Sha256)]
    [DataRow("rsa", TlsSignatureScheme.RsaPkcs1Sha256)]
    public void AClientCertificateIsSentAndSignedWhenRequested(string credential, int expectedScheme)
    {
        TestServerCredential clientCredential = Credential(credential);
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256))
        {
            RequestClientCertificate = true,
            ClientCertificateSchemes = [TlsSignatureScheme.RsaPssPssSha256, (ushort)expectedScheme],
        };
        Tls12ClientHandshake client = Client(DefaultSettings with { ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey) });

        Tls12HandshakeOutput output = Run(client, server);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.IsTrue(client.ClientCertificateRequested);
        Assert.IsTrue(client.ClientCertificateSent);
        CollectionAssert.AreEqual(clientCredential.Certificate, server.ClientCertificates[0]);
        Assert.AreEqual((ushort)expectedScheme, Tls12CertificateVerify.Decode(Body(output.MessagesToSend[2]), true).Value.SignatureAlgorithm);
    }

    [TestMethod]
    public void AClientCertificateOnTlsOneOneSignsWithEcdsaOverSha1()
    {
        TestServerCredential clientCredential = Credential("ecdsa");
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { Version = TlsProtocolVersion.Tls11, CipherSuite = 0xc014, RequestClientCertificate = true };
        Tls12ClientHandshake client = Client(DefaultSettings with
        {
            MinimumVersion = TlsProtocolVersion.Tls11,
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        Assert.IsTrue(client.ClientCertificateSent);
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls12, "ed25519")]
    [DataRow(TlsProtocolVersion.Tls10, "rsa")]
    [DataRow(TlsProtocolVersion.Tls12, "none")]
    public void WithoutAKeyThatCanSignTheClientSendsAnEmptyCertificate(TlsProtocolVersion version, string credential)
    {
        TestServerCredential? clientCredential = credential == "none" ? null : Credential(credential);
        TlsClientCertificate? certificate = clientCredential is null ? null : new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey);
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256))
        {
            Version = version,
            CipherSuite = 0xc013,
            RequestClientCertificate = true,
            ClientCertificateSchemes = [TlsSignatureScheme.RsaPkcs1Sha256],
        };
        Tls12ClientHandshake client = Client(DefaultSettings with { MinimumVersion = TlsProtocolVersion.Tls10, ClientCertificate = certificate });

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        Assert.IsTrue(client.ClientCertificateRequested);
        Assert.IsFalse(client.ClientCertificateSent);
        Assert.IsEmpty(server.ClientCertificates);
    }

    [TestMethod]
    public void AHelloRequestIsIgnoredDuringAndAfterTheHandshake()
    {
        byte[] helloRequest = new HandshakeMessage(HandshakeType.HelloRequest, []).Encode();
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Tls12ClientHandshake client = Client();

        Tls12HandshakeOutput output = Run(client, server, flight => [flight[0], new(TlsContentType.Handshake, helloRequest), .. flight[1..]]);
        Tls12HandshakeOutput after = client.ReceiveHandshake(helloRequest);

        AssertCompletesWithServerKeys(client, server, output);
        Assert.IsNull(after.Failure);
        Assert.IsTrue(after.IsComplete);
        Assert.IsEmpty(after.MessagesToSend);
    }

    [TestMethod]
    public void MessagesSplitAcrossAndJoinedWithinRecordsAreReassembled()
    {
        Tls12TestServer server = new(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Tls12ClientHandshake client = Client();
        byte[] flight = [.. server.Answer(client.Start().MessagesToSend[0].Bytes).SelectMany(message => message.Bytes)];
        List<Tls12OutgoingMessage> sent = [];

        foreach (byte[] chunk in flight.Chunk(7))
        {
            sent.AddRange(client.ReceiveHandshake(chunk).MessagesToSend);
        }

        Tls12HandshakeOutput output = Deliver(client, server.ReceiveClientFlight(sent));
        AssertCompletesWithServerKeys(client, server, output);
    }

    private static TestServerCredential Credential(string name) => name switch
    {
        "ecdsa" or "ecdsa256" => TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256),
        "ecdsa384" => TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP384, TlsSignatureScheme.EcdsaSecp384r1Sha384),
        "ed25519" => TestServerCredential.Ed25519(),
        "rsa" => TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256),
        _ => null!,
    };

    private static void AssertCompletesWithServerKeys(Tls12ClientHandshake client, Tls12TestServer server, Tls12HandshakeOutput output)
    {
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.IsComplete);
        Assert.AreEqual(server.KeyBlock, client.KeyBlock, KeyBlockComparer.Instance);
        CollectionAssert.AreEqual(server.MasterSecret, client.Session!.MasterSecret);
    }

    private sealed class KeyBlockComparer : IEqualityComparer<Tls12KeyBlock?>
    {
        public static readonly KeyBlockComparer Instance = new();

        public bool Equals(Tls12KeyBlock? x, Tls12KeyBlock? y) => Flatten(x).SequenceEqual(Flatten(y));

        public int GetHashCode(Tls12KeyBlock? obj) => 0;

        private static byte[] Flatten(Tls12KeyBlock? block) => block is null
            ? []
            : [.. block.ClientWrite.MacKey, .. block.ClientWrite.Key, .. block.ClientWrite.Iv, .. block.ServerWrite.MacKey, .. block.ServerWrite.Key, .. block.ServerWrite.Iv];
    }
}
