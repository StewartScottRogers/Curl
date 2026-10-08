using System.Security.Cryptography;
using Curl.Testing;
using static Curl.Tls.HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// Completes handshakes against the in-memory server for every TLS 1.3 cipher suite, every
/// key share group (directly and through a HelloRetryRequest), every CertificateVerify
/// signature scheme, ALPN, a stapled OCSP response and client certificates.
/// </summary>
[TestClass]
public sealed class Tls13ClientHandshakeTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((ushort)0x1301)]
    [DataRow((ushort)0x1302)]
    [DataRow((ushort)0x1303)]
    public void HandshakeCompletesWithEachCipherSuite(int cipherSuite)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { CipherSuite = (ushort)cipherSuite };
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [(ushort)cipherSuite] });
        Diagnostics.Arrange("cipher suite", $"0x{cipherSuite:x4}");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Act("negotiated cipher suite", client.CipherSuite is null ? "none" : $"0x{client.CipherSuite.Code:x4}");
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Assert("cipher suite", cipherSuite, (int?)client.CipherSuite?.Code);
        Diagnostics.Diff("server application traffic secret", server.ServerApplicationTrafficSecret, output.SecretsInstalled[0].Secret);
        Diagnostics.Diff("client application traffic secret", server.ClientApplicationTrafficSecret, output.SecretsInstalled[1].Secret);
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual(cipherSuite, client.CipherSuite!.Code);
        CollectionAssert.AreEqual(server.ServerApplicationTrafficSecret, output.SecretsInstalled[0].Secret);
        CollectionAssert.AreEqual(server.ClientApplicationTrafficSecret, output.SecretsInstalled[1].Secret);
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519)]
    [DataRow(TlsNamedGroup.X448)]
    [DataRow(TlsNamedGroup.X25519MlKem768)]
    [DataRow(TlsNamedGroup.Secp256r1)]
    [DataRow(TlsNamedGroup.Secp384r1)]
    [DataRow(TlsNamedGroup.Secp521r1)]
    [DataRow(TlsNamedGroup.Ffdhe2048)]
    [DataRow(TlsNamedGroup.MlKem512)]
    [DataRow(TlsNamedGroup.MlKem768)]
    [DataRow(TlsNamedGroup.MlKem1024)]
    [DataRow(TlsNamedGroup.SecP256r1MlKem768)]
    [DataRow(TlsNamedGroup.SecP384r1MlKem1024)]
    [DataRow(TlsNamedGroup.BrainpoolP256r1Tls13)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1Tls13)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1Tls13)]
    public void HandshakeCompletesWithAKeyShareOnEachGroup(int group)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = (ushort)group };
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [(ushort)group], KeyShareGroups = [(ushort)group] });
        Diagnostics.Arrange("group (supported and key share)", $"0x{group:x4}");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        AssertGroup(group, client);
        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>((ushort)group, client.NegotiatedGroup);
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.Secp256r1)]
    [DataRow(TlsNamedGroup.Secp384r1)]
    [DataRow(TlsNamedGroup.Secp521r1)]
    [DataRow(TlsNamedGroup.Ffdhe3072)]
    [DataRow(TlsNamedGroup.X448)]
    [DataRow(TlsNamedGroup.X25519MlKem768)]
    [DataRow(TlsNamedGroup.MlKem1024)]
    [DataRow(TlsNamedGroup.SecP384r1MlKem1024)]
    [DataRow(TlsNamedGroup.BrainpoolP256r1Tls13)]
    public void HandshakeCompletesAfterAHelloRetryRequestForEachGroup(int group)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = (ushort)group, CipherSuite = 0x1302 };
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519, (ushort)group] });
        Diagnostics.Arrange("client groups", $"x25519, 0x{group:x4}");
        Diagnostics.Arrange("server group", $"0x{group:x4} (forces a HelloRetryRequest)");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Act("server sent a HelloRetryRequest", server.SentHelloRetryRequest);
        AssertGroup(group, client);
        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>((ushort)group, client.NegotiatedGroup);
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519MlKem768, false)]
    [DataRow(TlsNamedGroup.X25519, false)]
    [DataRow(TlsNamedGroup.X448, true)]
    public void HandshakeCompletesWithTheOpenSslProfilesGroupsWhicheverTheServerPicks(int group, bool retried)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = (ushort)group };
        using Tls13ClientHandshake client = Client(DefaultSettings with
        {
            SupportedGroups = ClientHelloProfile.OpenSsl.SupportedGroups,
            KeyShareGroups = ClientHelloProfile.OpenSsl.KeyShareGroups,
        });
        Diagnostics.Arrange("client groups", "OpenSSL profile");
        Diagnostics.Arrange("server group", $"0x{group:x4}");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Act("server sent a HelloRetryRequest", server.SentHelloRetryRequest);
        AssertGroup(group, client);
        Diagnostics.Assert("HelloRetryRequest sent", retried, server.SentHelloRetryRequest);
        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>((ushort)group, client.NegotiatedGroup);
        Assert.AreEqual(retried, server.SentHelloRetryRequest);
    }

    [TestMethod]
    public void HelloRetryRequestCookieIsEchoedInTheSecondClientHello()
    {
        byte[] cookie = [1, 2, 3, 4];
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = TlsNamedGroup.Secp256r1, RetryCookie = cookie };
        using Tls13ClientHandshake client = Client();
        Diagnostics.Bytes("retry cookie", cookie);
        Diagnostics.Arrange("server group", "secp256r1 (forces a HelloRetryRequest)");
        TestServerFlight retry;
        byte[] secondHello;
        using (Diagnostics.Phase("first flight and HelloRetryRequest"))
        {
            retry = server.Answer(client.Start().BytesToSend[0].Bytes);
            secondHello = client.Receive(TlsEncryptionLevel.Initial, retry.ServerHello).BytesToSend[0].Bytes;
        }

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(secondHello).Message!.Body).Value;
        TlsExtension echoed = hello.Extensions.Single(extension => extension.Type == TlsExtensionType.Cookie);
        Diagnostics.Bytes("second ClientHello", secondHello);
        Diagnostics.Act("echoed cookie extension length", echoed.Data.Length);
        Diagnostics.Diff("echoed cookie", cookie, CookieExtension.Decode(echoed.Data).Value);
        CollectionAssert.AreEqual(cookie, CookieExtension.Decode(echoed.Data).Value);
        bool complete;
        using (Diagnostics.Phase("second flight"))
        {
            complete = Run2(client, server, secondHello).IsComplete;
        }

        Diagnostics.Assert("complete", true, complete);
        Assert.IsTrue(complete);
    }

    [TestMethod]
    public void ClientHelloOffersTheRenegotiationScsvAfterTheTls13SuitesWhenAsked()
    {
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1302, 0x1303, 0x1301], OfferEmptyRenegotiationInfoScsv = true });
        Diagnostics.Arrange("cipher suites", "0x1302, 0x1303, 0x1301; OfferEmptyRenegotiationInfoScsv = true");

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(client.Start().BytesToSend[0].Bytes).Message!.Body).Value;

        Diagnostics.Act("offered cipher suites", Suites(hello));
        Diagnostics.Assert("offered cipher suites", "0x1302, 0x1303, 0x1301, 0x00ff", Suites(hello));
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301, 0x00ff }, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    public void ClientHelloOffersTheRenegotiationScsvOnceWhenTheLowerVersionsAlsoOfferIt()
    {
        Tls12ClientSettings lower = Tls12PipeDriver.DefaultSettings with { CipherSuites = [0xc02f, Tls12CipherSuite.EmptyRenegotiationInfoScsv] };
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [0x1301], OfferEmptyRenegotiationInfoScsv = true, LowerVersions = lower });
        Diagnostics.Arrange("TLS 1.3 cipher suites", "0x1301; OfferEmptyRenegotiationInfoScsv = true");
        Diagnostics.Arrange("lower-version cipher suites", "0xc02f, 0x00ff");

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(client.Start().BytesToSend[0].Bytes).Message!.Body).Value;

        Diagnostics.Act("offered cipher suites", Suites(hello));
        Diagnostics.Assert("offered cipher suites", "0x1301, 0xc02f, 0x00ff", Suites(hello));
        CollectionAssert.AreEqual(new ushort[] { 0x1301, 0xc02f, 0x00ff }, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    public void HelloRetryRequestWithOnlyACookieKeepsTheKeyShares()
    {
        using Tls13ClientHandshake client = Client();
        byte[] firstHello = client.Start().BytesToSend[0].Bytes;
        byte[] retry = new ServerHello(0x0303, ServerHello.HelloRetryRequestRandom.ToArray(), [], 0x1301, 0,
            [SupportedVersionsExtension.EncodeSelected(0x0304), CookieExtension.Encode([9])]).Encode();
        Diagnostics.Arrange("HelloRetryRequest", "cookie [9] only, no key_share");
        Diagnostics.Bytes("HelloRetryRequest", retry);

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Initial, retry);

        ClientHello first = ClientHello.Decode(HandshakeMessageReader.Read(firstHello).Message!.Body).Value;
        ClientHello second = ClientHello.Decode(HandshakeMessageReader.Read(output.BytesToSend[0].Bytes).Message!.Body).Value;
        Diagnostics.Act("first hello extensions", first.Extensions.Count);
        Diagnostics.Act("second hello extensions", second.Extensions.Count);
        Diagnostics.Diff(
            "key_share",
            first.Extensions.Single(extension => extension.Type == TlsExtensionType.KeyShare).Data,
            second.Extensions.Single(extension => extension.Type == TlsExtensionType.KeyShare).Data);
        Diagnostics.Assert("second hello extensions", first.Extensions.Count + 1, second.Extensions.Count);
        CollectionAssert.AreEqual(
            first.Extensions.Single(extension => extension.Type == TlsExtensionType.KeyShare).Data,
            second.Extensions.Single(extension => extension.Type == TlsExtensionType.KeyShare).Data);
        Assert.AreEqual(first.Extensions.Count + 1, second.Extensions.Count);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha256)]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha384)]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha512)]
    public void HandshakeCompletesWithEachRsaPssRsaeScheme(int scheme) => AssertCompletes(TestServerCredential.Rsa((ushort)scheme));

    [TestMethod]
    [DataRow(TlsSignatureScheme.RsaPssPssSha256)]
    [DataRow(TlsSignatureScheme.RsaPssPssSha384)]
    [DataRow(TlsSignatureScheme.RsaPssPssSha512)]
    public void HandshakeCompletesWithEachRsaPssPssScheme(int scheme) => AssertCompletes(TestServerCredential.RsaPss((ushort)scheme));

    [TestMethod]
    public void HandshakeCompletesWithEcdsaOnP256() => AssertCompletes(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256));

    [TestMethod]
    public void HandshakeCompletesWithEcdsaOnP384() => AssertCompletes(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP384, TlsSignatureScheme.EcdsaSecp384r1Sha384));

    [TestMethod]
    public void HandshakeCompletesWithEcdsaOnP521() => AssertCompletes(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP521, TlsSignatureScheme.EcdsaSecp521r1Sha512));

    [TestMethod]
    public void HandshakeCompletesWithEd25519() => AssertCompletes(TestServerCredential.Ed25519());

    [TestMethod]
    public void HandshakeCompletesWithEd448() => AssertCompletesOfferingIt(TestServerCredential.Ed448());

    [TestMethod]
    [DataRow(Cryptography.BrainpoolCurve.BrainpoolP256r1, TlsSignatureScheme.BrainpoolP256r1Oid, TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256)]
    [DataRow(Cryptography.BrainpoolCurve.BrainpoolP384r1, TlsSignatureScheme.BrainpoolP384r1Oid, TlsSignatureScheme.EcdsaBrainpoolP384r1Tls13Sha384)]
    [DataRow(Cryptography.BrainpoolCurve.BrainpoolP512r1, TlsSignatureScheme.BrainpoolP512r1Oid, TlsSignatureScheme.EcdsaBrainpoolP512r1Tls13Sha512)]
    public void HandshakeCompletesWithEachBrainpoolTls13Scheme(Cryptography.BrainpoolCurve curve, string curveOid, int scheme) =>
        AssertCompletesOfferingIt(TestServerCredential.Brainpool(curve, curveOid, (ushort)scheme));

    [TestMethod]
    [DataRow(Cryptography.MlDsaParameterSet.MlDsa44, TlsSignatureScheme.MlDsa44Oid, TlsSignatureScheme.MlDsa44)]
    [DataRow(Cryptography.MlDsaParameterSet.MlDsa65, TlsSignatureScheme.MlDsa65Oid, TlsSignatureScheme.MlDsa65)]
    [DataRow(Cryptography.MlDsaParameterSet.MlDsa87, TlsSignatureScheme.MlDsa87Oid, TlsSignatureScheme.MlDsa87)]
    public void HandshakeCompletesWithEachMlDsaScheme(Cryptography.MlDsaParameterSet parameterSet, string algorithmOid, int scheme) =>
        AssertCompletesOfferingIt(TestServerCredential.MlDsa(parameterSet, algorithmOid, (ushort)scheme));

    [TestMethod]
    [DataRow("ed448")]
    [DataRow("brainpool")]
    [DataRow("mldsa")]
    public void ABadCertificateVerifySignatureOnEachAddedSchemeIsADecryptError(string name)
    {
        TestServerCredential credential = name switch
        {
            "ed448" => TestServerCredential.Ed448(),
            "brainpool" => TestServerCredential.Brainpool(Cryptography.BrainpoolCurve.BrainpoolP256r1, TlsSignatureScheme.BrainpoolP256r1Oid, TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256),
            _ => TestServerCredential.MlDsa(Cryptography.MlDsaParameterSet.MlDsa44, TlsSignatureScheme.MlDsa44Oid, TlsSignatureScheme.MlDsa44),
        };
        Tls13TestServer server = new(credential);
        using Tls13ClientHandshake client = Client(DefaultSettings with { SignatureAlgorithms = [credential.Scheme] });
        Diagnostics.Arrange("server credential", $"{name}, scheme 0x{credential.Scheme:x4}");
        Diagnostics.Arrange("server flight", "CertificateVerify tampered");

        Tls13HandshakeOutput output = RunTimed(client, server, replaceFlight: flight => Tamper(flight, HandshakeType.CertificateVerify));

        WriteOutput(output);
        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, output.Failure!.Alert);
    }

    [TestMethod]
    public void HandshakeNegotiatesTheApplicationProtocolTheServerChose()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ApplicationProtocol = "http/1.1" };
        using Tls13ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2", "http/1.1"] });
        Diagnostics.Arrange("client ALPN", "h2, http/1.1");
        Diagnostics.Arrange("server ALPN", "http/1.1");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Act("application protocol", client.ApplicationProtocol ?? "none");
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Assert("application protocol", "http/1.1", client.ApplicationProtocol);
        Diagnostics.Assert("server QUIC transport parameters", "null", client.ServerQuicTransportParameters is null ? "null" : "present");
        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual("http/1.1", client.ApplicationProtocol);
        Assert.IsNull(client.ServerQuicTransportParameters);
    }

    [TestMethod]
    public void HandshakeReportsTheServersQuicTransportParameters()
    {
        byte[] parameters = [0x04, 0x01, 0x20];
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { QuicTransportParameters = parameters };
        using Tls13ClientHandshake client = Client(DefaultSettings with { FixedExtensions = [QuicTransportParametersExtension.Encode([0x05, 0x01, 0x10])] });
        Diagnostics.Bytes("server QUIC transport parameters", parameters);
        Diagnostics.Arrange("client", "offers QUIC transport parameters 05 01 10");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Diff("server QUIC transport parameters", parameters, client.ServerQuicTransportParameters ?? []);
        Assert.IsTrue(output.IsComplete);
        CollectionAssert.AreEqual(parameters, client.ServerQuicTransportParameters);
    }

    [TestMethod]
    public void HandshakeHandsTheStapledOcspResponseToTheVerifier()
    {
        byte[] ocspResponse = [0x30, 0x03, 0x0a, 0x01, 0x00];
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { LeafExtensions = [StatusRequestExtension.EncodeOcspResponse(ocspResponse)] };
        RecordingCertificateVerifier verifier = new();
        TlsExtension statusRequest = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));
        using Tls13ClientHandshake client = Client(DefaultSettings with { FixedExtensions = [statusRequest] }, verifier);
        Diagnostics.Bytes("stapled OCSP response", ocspResponse);
        Diagnostics.Arrange("client", "status_request sent as a fixed extension");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Act("chains presented to the verifier", verifier.Presented.Count);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Diff("OCSP response presented", ocspResponse, verifier.Presented.Count == 0 ? [] : verifier.Presented[0].OcspResponse ?? []);
        Diagnostics.Assert("host name presented", "localhost", verifier.Presented.Count == 0 ? null : verifier.Presented[0].HostName);
        Assert.IsTrue(output.IsComplete);
        CollectionAssert.AreEqual(ocspResponse, verifier.Presented[0].OcspResponse);
        Assert.AreEqual("localhost", verifier.Presented[0].HostName);
    }

    [TestMethod]
    public void HandshakeEchoesARandomLegacySessionId()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { SendLegacySessionId = true });
        Diagnostics.Arrange("settings", "SendLegacySessionId = true");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Assert.IsTrue(output.IsComplete);
    }

    [TestMethod]
    public void HandshakeSendsTheClientCertificateTheServerAskedFor()
    {
        TestServerCredential clientCredential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true };
        using Tls13ClientHandshake client = Client(DefaultSettings with
        {
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });
        Diagnostics.Arrange("client certificate", "ECDSA P-256");
        Diagnostics.Arrange("server", "requests a client certificate");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        WriteClientCertificate(client, server);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Assert("client certificate sent", true, client.ClientCertificateSent);
        Diagnostics.Assert("certificates the server received", 1, server.ClientCertificates.Count);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.ClientCertificateRequested);
        Assert.IsTrue(client.ClientCertificateSent);
        Assert.HasCount(1, server.ClientCertificates);
        CollectionAssert.AreEqual(clientCredential.Certificate, server.ClientCertificates[0]);
    }

    [TestMethod]
    [DataRow("ed448")]
    [DataRow("mldsa44")]
    [DataRow("mldsa65")]
    [DataRow("mldsa87")]
    public void HandshakeSignsTheClientCertificateVerifyWithAnEd448OrMlDsaKey(string credential)
    {
        TestServerCredential clientCredential = credential switch
        {
            "ed448" => TestServerCredential.Ed448(),
            "mldsa44" => TestServerCredential.MlDsa(Cryptography.MlDsaParameterSet.MlDsa44, TlsSignatureScheme.MlDsa44Oid, TlsSignatureScheme.MlDsa44),
            "mldsa65" => TestServerCredential.MlDsa(Cryptography.MlDsaParameterSet.MlDsa65, TlsSignatureScheme.MlDsa65Oid, TlsSignatureScheme.MlDsa65),
            _ => TestServerCredential.MlDsa(Cryptography.MlDsaParameterSet.MlDsa87, TlsSignatureScheme.MlDsa87Oid, TlsSignatureScheme.MlDsa87),
        };
        Tls13TestServer server = new(TestServerCredential.Ed25519())
        {
            RequestClientCertificate = true,
            ClientCertificateSchemes = [TlsSignatureScheme.Ed25519, TlsSignatureScheme.Ed448, TlsSignatureScheme.MlDsa44, TlsSignatureScheme.MlDsa65, TlsSignatureScheme.MlDsa87],
        };
        using Tls13ClientHandshake client = Client(DefaultSettings with
        {
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });
        Diagnostics.Arrange("client credential", $"{credential}, scheme 0x{clientCredential.Scheme:x4}");

        // The server checks the CertificateVerify against the certificate's key with TlsCertificatePublicKey.
        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        WriteClientCertificate(client, server);
        Diagnostics.Act("client CertificateVerify scheme", server.ClientCertificateVerifyScheme);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Assert("client CertificateVerify scheme", clientCredential.Scheme, server.ClientCertificateVerifyScheme);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.ClientCertificateSent);
        CollectionAssert.AreEqual(clientCredential.Certificate, server.ClientCertificates[0]);
        Assert.AreEqual(clientCredential.Scheme, server.ClientCertificateVerifyScheme);
    }

    [TestMethod]
    public void HandshakeSendsAnEmptyCertificateWhenItHasNone()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true };
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("client certificate", "none");
        Diagnostics.Arrange("server", "requests a client certificate");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        WriteClientCertificate(client, server);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Assert("client certificate sent", false, client.ClientCertificateSent);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.ClientCertificateRequested);
        Assert.IsFalse(client.ClientCertificateSent);
        Assert.IsEmpty(server.ClientCertificates);
    }

    [TestMethod]
    public void HandshakeSendsAnEmptyCertificateWhenNoRequestedSchemeFitsItsKey()
    {
        TestServerCredential clientCredential = TestServerCredential.Ed25519();
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true, ClientCertificateSchemes = [TlsSignatureScheme.RsaPssRsaeSha256] };
        using Tls13ClientHandshake client = Client(DefaultSettings with
        {
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });
        Diagnostics.Arrange("client certificate", "Ed25519");
        Diagnostics.Arrange("server requested schemes", "rsa_pss_rsae_sha256 only");

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        WriteClientCertificate(client, server);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Assert("client certificate sent", false, client.ClientCertificateSent);
        Assert.IsTrue(output.IsComplete);
        Assert.IsFalse(client.ClientCertificateSent);
    }

    [TestMethod]
    public void APostHandshakeRequestIsAnsweredAtTheApplicationLevelWithNoNewSecrets()
    {
        TestServerCredential clientCredential = TestServerCredential.Rsa(TlsSignatureScheme.RsaPssRsaeSha256);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { CipherSuite = Tls13CipherSuite.Aes256GcmSha384.Code };
        using Tls13ClientHandshake client = Client(DefaultSettings with
        {
            CipherSuites = [Tls13CipherSuite.Aes256GcmSha384.Code],
            ExtensionOrder = [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.PostHandshakeAuth],
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });
        Diagnostics.Arrange("client", "offers post_handshake_auth with an RSA certificate");
        bool complete = RunTimed(client, server).IsComplete;
        Diagnostics.Assert("handshake complete", true, complete);
        Assert.IsTrue(complete);
        byte[] request = server.CreatePostHandshakeCertificateRequest([5, 5]);
        Diagnostics.Bytes("post-handshake CertificateRequest", request);

        Tls13HandshakeOutput answer;
        using (Diagnostics.Phase("post-handshake answer"))
        {
            answer = client.Receive(TlsEncryptionLevel.Application, request);
        }

        WriteOutput(answer);
        Diagnostics.Act("secrets installed", answer.SecretsInstalled.Count);
        Diagnostics.Act("flights to send", answer.BytesToSend.Count);
        Diagnostics.Assert("answer level", TlsEncryptionLevel.Application, answer.BytesToSend.Count == 0 ? null : answer.BytesToSend[0].Level);
        Assert.IsTrue(answer.IsComplete);
        Assert.IsNull(answer.Failure);
        Assert.IsEmpty(answer.SecretsInstalled);
        Assert.HasCount(1, answer.BytesToSend);
        Assert.AreEqual(TlsEncryptionLevel.Application, answer.BytesToSend[0].Level);
        server.ReceivePostHandshakeAnswer(request, answer.BytesToSend[0].Bytes, [5, 5], server.ClientApplicationTrafficSecret);
        CollectionAssert.AreEqual(clientCredential.Certificate, server.ClientCertificates[0]);
    }

    /// <summary>Completes a handshake whose client offers only <paramref name="credential" />'s scheme, which the default settings do not offer.</summary>
    private void AssertCompletesOfferingIt(TestServerCredential credential) =>
        AssertCompletes(credential, DefaultSettings with { SignatureAlgorithms = [credential.Scheme] });

    private void AssertCompletes(TestServerCredential credential, Tls13ClientSettings? settings = null)
    {
        Tls13TestServer server = new(credential);
        using Tls13ClientHandshake client = Client(settings);
        Diagnostics.Arrange("server signature scheme", $"0x{credential.Scheme:x4}");
        Diagnostics.Arrange("client signature algorithms", settings is null ? "default" : string.Join(", ", settings.SignatureAlgorithms.Select(scheme => $"0x{scheme:x4}")));

        Tls13HandshakeOutput output = RunTimed(client, server);

        WriteOutput(output);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Diagnostics.Diff("server leaf certificate", credential.Certificate, client.ServerCertificates.Count == 0 ? [] : client.ServerCertificates[0]);
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        CollectionAssert.AreEqual(credential.Certificate, client.ServerCertificates[0]);
    }

    private static string Suites(ClientHello hello) => string.Join(", ", hello.CipherSuites.Select(suite => $"0x{suite:x4}"));

    private static Tls13HandshakeOutput Run2(Tls13ClientHandshake client, Tls13TestServer server, byte[] secondHello)
    {
        TestServerFlight flight = server.Answer(secondHello);
        client.Receive(TlsEncryptionLevel.Initial, flight.ServerHello);
        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Handshake, flight.Handshake);
        server.ReceiveClientFlight(output.BytesToSend[0].Bytes);
        return output;
    }

    private Tls13HandshakeOutput RunTimed(Tls13ClientHandshake client, Tls13TestServer server, Func<List<byte[]>, List<byte[]>>? replaceFlight = null)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return Run(client, server, replaceFlight: replaceFlight);
        }
    }

    private void AssertGroup(int group, Tls13ClientHandshake client)
    {
        Diagnostics.Act("negotiated group", client.NegotiatedGroup is { } negotiated ? $"0x{negotiated:x4}" : "none");
        Diagnostics.Assert("negotiated group", (ushort)group, client.NegotiatedGroup);
    }

    private void WriteOutput(Tls13HandshakeOutput output)
    {
        Diagnostics.Act("complete", output.IsComplete);
        Diagnostics.Act("failure alert", output.Failure?.Alert.ToString() ?? "none");
    }

    private void WriteClientCertificate(Tls13ClientHandshake client, Tls13TestServer server)
    {
        Diagnostics.Act("client certificate requested", client.ClientCertificateRequested);
        Diagnostics.Act("client certificate sent", client.ClientCertificateSent);
        Diagnostics.Act("certificates the server received", server.ClientCertificates.Count);
    }
}
