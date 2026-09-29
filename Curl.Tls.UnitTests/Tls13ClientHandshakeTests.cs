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
    public void HandshakeCompletesAfterAHelloRetryRequestForEachGroup(int group)
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = (ushort)group, CipherSuite = 0x1302 };
        using Tls13ClientHandshake client = Client(DefaultSettings with { SupportedGroups = [TlsNamedGroup.X25519, (ushort)group] });

        Tls13HandshakeOutput output = Run(client, server);

        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>((ushort)group, client.NegotiatedGroup);
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

    private static void AssertCompletes(TestServerCredential credential)
    {
        Tls13TestServer server = new(credential);
        using Tls13ClientHandshake client = Client();

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
