using System.Security.Cryptography;
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
    [TestMethod]
    [DataRow((ushort)0x1301)]
    [DataRow((ushort)0x1302)]
    [DataRow((ushort)0x1303)]
    public void HandshakeCompletesWithEachCipherSuite(int cipherSuite)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { CipherSuite = (ushort)cipherSuite };
        using Tls13ClientHandshake client = Client(DefaultSettings with { CipherSuites = [(ushort)cipherSuite] });

        Tls13HandshakeOutput output = Run(client, server);

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
    public void HandshakeCompletesWithAKeyShareOnEachGroup(int group)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = (ushort)group };
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [(ushort)group], KeyShareGroups = [(ushort)group] });

        Tls13HandshakeOutput output = Run(client, server);

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
    public void HandshakeCompletesAfterAHelloRetryRequestForEachGroup(int group)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = (ushort)group, CipherSuite = 0x1302 };
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519, (ushort)group] });

        Tls13HandshakeOutput output = Run(client, server);

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

        Tls13HandshakeOutput output = Run(client, server);

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
        TestServerFlight retry = server.Answer(client.Start().BytesToSend[0].Bytes);

        byte[] secondHello = client.Receive(TlsEncryptionLevel.Initial, retry.ServerHello).BytesToSend[0].Bytes;

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(secondHello).Message!.Body).Value;
        TlsExtension echoed = hello.Extensions.Single(extension => extension.Type == TlsExtensionType.Cookie);
        CollectionAssert.AreEqual(cookie, CookieExtension.Decode(echoed.Data).Value);
        Assert.IsTrue(Run2(client, server, secondHello).IsComplete);
    }

    [TestMethod]
    public void HelloRetryRequestWithOnlyACookieKeepsTheKeyShares()
    {
        using Tls13ClientHandshake client = Client();
        byte[] firstHello = client.Start().BytesToSend[0].Bytes;
        byte[] retry = new ServerHello(0x0303, ServerHello.HelloRetryRequestRandom.ToArray(), [], 0x1301, 0,
            [SupportedVersionsExtension.EncodeSelected(0x0304), CookieExtension.Encode([9])]).Encode();

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Initial, retry);

        ClientHello first = ClientHello.Decode(HandshakeMessageReader.Read(firstHello).Message!.Body).Value;
        ClientHello second = ClientHello.Decode(HandshakeMessageReader.Read(output.BytesToSend[0].Bytes).Message!.Body).Value;
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

        Tls13HandshakeOutput output = Run(client, server, replaceFlight: flight => Tamper(flight, HandshakeType.CertificateVerify));

        Assert.AreEqual(TlsAlertDescription.DecryptError, output.Failure!.Alert);
    }

    [TestMethod]
    public void HandshakeNegotiatesTheApplicationProtocolTheServerChose()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ApplicationProtocol = "http/1.1" };
        using Tls13ClientHandshake client = Client(DefaultSettings with { ApplicationProtocols = ["h2", "http/1.1"] });

        Assert.IsTrue(Run(client, server).IsComplete);
        Assert.AreEqual("http/1.1", client.ApplicationProtocol);
        Assert.IsNull(client.ServerQuicTransportParameters);
    }

    [TestMethod]
    public void HandshakeReportsTheServersQuicTransportParameters()
    {
        byte[] parameters = [0x04, 0x01, 0x20];
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { QuicTransportParameters = parameters };
        using Tls13ClientHandshake client = Client(DefaultSettings with { FixedExtensions = [QuicTransportParametersExtension.Encode([0x05, 0x01, 0x10])] });

        Assert.IsTrue(Run(client, server).IsComplete);
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

        Assert.IsTrue(Run(client, server).IsComplete);
        CollectionAssert.AreEqual(ocspResponse, verifier.Presented[0].OcspResponse);
        Assert.AreEqual("localhost", verifier.Presented[0].HostName);
    }

    [TestMethod]
    public void HandshakeEchoesARandomLegacySessionId()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(DefaultSettings with { SendLegacySessionId = true });

        Assert.IsTrue(Run(client, server).IsComplete);
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

        Assert.IsTrue(Run(client, server).IsComplete);
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

        // The server checks the CertificateVerify against the certificate's key with TlsCertificatePublicKey.
        Assert.IsTrue(Run(client, server).IsComplete);
        Assert.IsTrue(client.ClientCertificateSent);
        CollectionAssert.AreEqual(clientCredential.Certificate, server.ClientCertificates[0]);
        Assert.AreEqual(clientCredential.Scheme, server.ClientCertificateVerifyScheme);
    }

    [TestMethod]
    public void HandshakeSendsAnEmptyCertificateWhenItHasNone()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { RequestClientCertificate = true };
        using Tls13ClientHandshake client = Client();

        Assert.IsTrue(Run(client, server).IsComplete);
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

        Assert.IsTrue(Run(client, server).IsComplete);
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
        Assert.IsTrue(Run(client, server).IsComplete);
        byte[] request = server.CreatePostHandshakeCertificateRequest([5, 5]);

        Tls13HandshakeOutput answer = client.Receive(TlsEncryptionLevel.Application, request);

        Assert.IsTrue(answer.IsComplete);
        Assert.IsNull(answer.Failure);
        Assert.IsEmpty(answer.SecretsInstalled);
        Assert.HasCount(1, answer.BytesToSend);
        Assert.AreEqual(TlsEncryptionLevel.Application, answer.BytesToSend[0].Level);
        server.ReceivePostHandshakeAnswer(request, answer.BytesToSend[0].Bytes, [5, 5], server.ClientApplicationTrafficSecret);
        CollectionAssert.AreEqual(clientCredential.Certificate, server.ClientCertificates[0]);
    }

    /// <summary>Completes a handshake whose client offers only <paramref name="credential" />'s scheme, which the default settings do not offer.</summary>
    private static void AssertCompletesOfferingIt(TestServerCredential credential) =>
        AssertCompletes(credential, DefaultSettings with { SignatureAlgorithms = [credential.Scheme] });

    private static void AssertCompletes(TestServerCredential credential, Tls13ClientSettings? settings = null)
    {
        Tls13TestServer server = new(credential);
        using Tls13ClientHandshake client = Client(settings);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        CollectionAssert.AreEqual(credential.Certificate, client.ServerCertificates[0]);
    }

    private static Tls13HandshakeOutput Run2(Tls13ClientHandshake client, Tls13TestServer server, byte[] secondHello)
    {
        TestServerFlight flight = server.Answer(secondHello);
        client.Receive(TlsEncryptionLevel.Initial, flight.ServerHello);
        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Handshake, flight.Handshake);
        server.ReceiveClientFlight(output.BytesToSend[0].Bytes);
        return output;
    }
}
