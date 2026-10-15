using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

[TestClass]
public sealed class Http3ServerTlsHandshakeTests
{
    private static readonly byte[] ServerTransportParameters = [0x01, 0x02, 0x03];

    private static readonly byte[] ClientTransportParameters = [0x0f, 0x00];

    private static readonly X509Certificate2 Certificate = CreateRsaCertificate();

    private static Http3ServerTlsSettings ServerSettings => Http3ServerTlsSettings.FromRsaCertificate(Certificate, ServerTransportParameters);

    private static Tls13ClientSettings CurlQuicTlsSettings
    {
        get
        {
            Tls13ClientSettings settings = QuicClientSettings.CreateOpenSslTlsSettings("localhost");
            return settings with { FixedExtensions = [.. settings.FixedExtensions, QuicTransportParametersExtension.Encode(ClientTransportParameters)] };
        }
    }

    [TestMethod]
    public void Receive_CurlQuicClientHello_CompletesWithEqualSecretsAtEveryLevel()
    {
        Exchange exchange = Drive(CurlQuicTlsSettings, ServerSettings);

        Assert.IsTrue(exchange.Client.IsComplete);
        Assert.IsTrue(exchange.Server.IsComplete);
        Assert.AreEqual("h3", exchange.Client.ApplicationProtocol);
        CollectionAssert.AreEqual(ServerTransportParameters, exchange.Client.ServerQuicTransportParameters);
        CollectionAssert.AreEqual(ClientTransportParameters, exchange.Server.ClientQuicTransportParameters);
        Assert.AreEqual(Tls13CipherSuite.Aes128GcmSha256, exchange.Server.CipherSuite);
        Assert.AreEqual(TlsNamedGroup.X25519, exchange.Server.NegotiatedGroup);
        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha256, exchange.Server.SignatureScheme);
        CollectionAssert.AreEqual(Certificate.RawData, exchange.Client.ServerCertificates[0]);
        AssertSecretsMirror(exchange);
    }

    [TestMethod]
    public void Receive_ClientWithoutAShareForTheServersGroup_SendsHelloRetryRequestAndCompletesOnSecp256r1()
    {
        Exchange exchange = Drive(
            CurlQuicTlsSettings with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1], KeyShareGroups = [TlsNamedGroup.X25519] },
            ServerSettings with { Groups = [TlsNamedGroup.Secp256r1] });

        Assert.IsTrue(exchange.Server.IsComplete);
        Assert.AreEqual(TlsNamedGroup.Secp256r1, exchange.Server.NegotiatedGroup);
        Assert.AreEqual(TlsNamedGroup.Secp256r1, exchange.Client.NegotiatedGroup);
        AssertSecretsMirror(exchange);
    }

    [TestMethod]
    public void Receive_ClientOfferingOnlyAnotherSuite_NegotiatesThatSuite()
    {
        Exchange exchange = Drive(CurlQuicTlsSettings with { CipherSuites = [Tls13CipherSuite.Aes256GcmSha384.Code] }, ServerSettings);

        Assert.IsTrue(exchange.Server.IsComplete);
        Assert.AreEqual(Tls13CipherSuite.Aes256GcmSha384, exchange.Server.CipherSuite);
        AssertSecretsMirror(exchange);
    }

    [TestMethod]
    public void Receive_NoCommonCipherSuite_FailsWithHandshakeFailure() =>
        AssertServerAlert(Drive(CurlQuicTlsSettings with { CipherSuites = [Tls13CipherSuite.Aes128GcmSha256.Code] }, ServerSettings with { CipherSuites = [Tls13CipherSuite.Aes256GcmSha384.Code] }), TlsAlertDescription.HandshakeFailure);

    [TestMethod]
    public void Receive_ServerSuiteTheLibraryDoesNotKnow_IsSkipped()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings with { CipherSuites = [0x13ff, 0x1301] }, SystemTlsRandomSource.Instance);

        server.Receive(TlsEncryptionLevel.Initial, Hello(suites: [0x13ff, 0x1301]));

        Assert.AreEqual(Tls13CipherSuite.Aes128GcmSha256, server.CipherSuite);
    }

    [TestMethod]
    public void Receive_NoCommonGroup_FailsWithHandshakeFailure() =>
        AssertServerAlert(Drive(CurlQuicTlsSettings with { SupportedGroups = [TlsNamedGroup.X25519], KeyShareGroups = [TlsNamedGroup.X25519] }, ServerSettings with { Groups = [TlsNamedGroup.Secp384r1] }), TlsAlertDescription.HandshakeFailure);

    [TestMethod]
    public void Receive_NoCommonSignatureScheme_FailsWithHandshakeFailure() =>
        AssertServerAlert(Drive(CurlQuicTlsSettings, ServerSettings with { SignatureSchemes = [TlsSignatureScheme.Ed25519] }), TlsAlertDescription.HandshakeFailure);

    [TestMethod]
    public void Receive_PreferredSchemeTheClientDoesNotOffer_SignsWithTheNextOne()
    {
        Exchange exchange = Drive(CurlQuicTlsSettings, ServerSettings with { SignatureSchemes = [0xfefe, TlsSignatureScheme.RsaPssRsaeSha256] });

        Assert.IsTrue(exchange.Client.IsComplete);
        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha256, exchange.Server.SignatureScheme);
    }

    [TestMethod]
    public void Receive_PreferredSchemeTheRsaKeyCannotSign_SignsWithTheNextOne()
    {
        Exchange exchange = Drive(CurlQuicTlsSettings, ServerSettings with { SignatureSchemes = [TlsSignatureScheme.EcdsaSecp256r1Sha256, TlsSignatureScheme.RsaPssRsaeSha384] });

        Assert.IsTrue(exchange.Client.IsComplete);
        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha384, exchange.Server.SignatureScheme);
    }

    [TestMethod]
    public void Receive_ApplicationProtocolsWithoutH3_FailsWithNoApplicationProtocol() =>
        AssertServerAlert(Drive(CurlQuicTlsSettings with { ApplicationProtocols = ["h2", "http/1.1"] }, ServerSettings), TlsAlertDescription.NoApplicationProtocol);

    [TestMethod]
    public void Receive_NoApplicationProtocolExtension_FailsWithNoApplicationProtocol() =>
        AssertServerAlert(Drive(CurlQuicTlsSettings with { ApplicationProtocols = [] }, ServerSettings), TlsAlertDescription.NoApplicationProtocol);

    [TestMethod]
    public void Receive_BadClientFinished_FailsWithDecryptError()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);
        using Tls13ClientHandshake client = new(CurlQuicTlsSettings, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance);
        Tls13HandshakeOutput serverFlight = Deliver(server, client.Start());
        byte[] finished = Deliver(client, serverFlight).BytesToSend.Single().Bytes;
        finished[^1] ^= 0x01;

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Handshake, finished);

        Assert.AreEqual(TlsAlertDescription.DecryptError, output.Failure?.Alert);
        Assert.IsFalse(output.IsComplete);
        Assert.AreEqual(TlsAlertDescription.DecryptError, server.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_ClientFinishedSplitAcrossCalls_CompletesOnTheLastPart()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);
        using Tls13ClientHandshake client = new(CurlQuicTlsSettings, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance);
        byte[] finished = Deliver(client, Deliver(server, client.Start())).BytesToSend.Single().Bytes;

        Tls13HandshakeOutput first = server.Receive(TlsEncryptionLevel.Handshake, finished.AsSpan(0, 3));
        Tls13HandshakeOutput last = server.Receive(TlsEncryptionLevel.Handshake, finished.AsSpan(3));

        Assert.IsFalse(first.IsComplete);
        Assert.IsEmpty(first.SecretsInstalled);
        Assert.IsTrue(last.IsComplete);
        Assert.AreEqual(TlsEncryptionLevel.Application, last.SecretsInstalled.Single().Level);
    }

    [TestMethod]
    public void Receive_MessageAfterClientFinishedInTheSameBytes_FailsWithUnexpectedMessage()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);
        using Tls13ClientHandshake client = new(CurlQuicTlsSettings, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance);
        byte[] finished = Deliver(client, Deliver(server, client.Start())).BytesToSend.Single().Bytes;

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Handshake, [.. finished, .. finished]);

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_AnotherMessageInPlaceOfClientFinished_FailsWithUnexpectedMessage()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);
        server.Receive(TlsEncryptionLevel.Initial, Hello());

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Handshake, Hello());

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_EmptyClientFinished_FailsWithDecryptError()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);
        server.Receive(TlsEncryptionLevel.Initial, Hello());

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Handshake, new HandshakeMessage(HandshakeType.Finished, []).Encode());

        Assert.AreEqual(TlsAlertDescription.DecryptError, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_SecondClientHelloStillWithoutTheRequestedShare_FailsWithIllegalParameter()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings with { Groups = [TlsNamedGroup.Secp256r1] }, SystemTlsRandomSource.Instance);
        byte[] hello = Hello(groups: [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1]);
        Tls13HandshakeOutput retry = server.Receive(TlsEncryptionLevel.Initial, hello);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, hello);

        Assert.IsTrue(ServerHello.Decode(retry.BytesToSend.Single().Bytes[HandshakeMessage.HeaderLength..]).Value.IsHelloRetryRequest);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_X25519ShareOfTheWrongLength_FailsWithIllegalParameter()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, Hello(share: new KeyShareEntry(TlsNamedGroup.X25519, [1, 2, 3])));

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
    }

    [TestMethod]
    [DataRow(TlsExtensionType.SupportedVersions, TlsAlertDescription.ProtocolVersion)]
    [DataRow(TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsAlertDescription.NoApplicationProtocol)]
    [DataRow(TlsExtensionType.SupportedGroups, TlsAlertDescription.MissingExtension)]
    [DataRow(TlsExtensionType.KeyShare, TlsAlertDescription.MissingExtension)]
    [DataRow(TlsExtensionType.SignatureAlgorithms, TlsAlertDescription.MissingExtension)]
    [DataRow(TlsExtensionType.QuicTransportParameters, TlsAlertDescription.MissingExtension)]
    public void Receive_ClientHelloWithoutARequiredExtension_FailsWithItsAlert(TlsExtensionType missing, TlsAlertDescription alert)
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, Hello(without: missing));

        Assert.AreEqual(alert, output.Failure?.Alert);
        Assert.IsEmpty(output.BytesToSend);
    }

    [TestMethod]
    public void Receive_SupportedVersionsWithoutTls13_FailsWithProtocolVersion()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, Hello(versions: [0x0303]));

        Assert.AreEqual(TlsAlertDescription.ProtocolVersion, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_MalformedExtension_FailsWithDecodeError()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, Hello(without: TlsExtensionType.KeyShare, extra: new TlsExtension(TlsExtensionType.KeyShare, [0x00, 0x09])));

        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_MalformedClientHello_FailsWithDecodeError()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, new HandshakeMessage(HandshakeType.ClientHello, [0x03, 0x03]).Encode());

        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_FirstMessageNotAClientHello_FailsWithUnexpectedMessage()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, new Finished(new byte[32]).Encode());

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_ClientHelloAtTheHandshakeLevel_FailsWithUnexpectedMessage()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Handshake, Hello());

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_UnknownHandshakeType_FailsWithUnexpectedMessage()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);

        Tls13HandshakeOutput output = server.Receive(TlsEncryptionLevel.Initial, [0xee, 0x00, 0x00, 0x00]);

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
    }

    [TestMethod]
    public void Receive_AfterTheHandshakeEnded_Throws()
    {
        using Http3ServerTlsHandshake server = new(ServerSettings, SystemTlsRandomSource.Instance);
        server.Receive(TlsEncryptionLevel.Initial, [0xee, 0x00, 0x00, 0x00]);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.Receive(TlsEncryptionLevel.Initial, Hello()));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ServerTlsHandshake(null!, SystemTlsRandomSource.Instance));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ServerTlsHandshake(ServerSettings, null!));
        new Http3ServerTlsHandshake(ServerSettings, SystemTlsRandomSource.Instance).Dispose();
    }

    [TestMethod]
    public void FromRsaCertificate_CertificateWithoutAnRsaKeyOrNullArguments_Throws()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 ecdsa = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Assert.ThrowsExactly<ArgumentException>(() => Http3ServerTlsSettings.FromRsaCertificate(ecdsa, ServerTransportParameters));
        Assert.ThrowsExactly<ArgumentNullException>(() => Http3ServerTlsSettings.FromRsaCertificate(null!, ServerTransportParameters));
        Assert.ThrowsExactly<ArgumentNullException>(() => Http3ServerTlsSettings.FromRsaCertificate(Certificate, null!));
    }

    private static X509Certificate2 CreateRsaCertificate()
    {
        using RSA key = RSA.Create(2048);
        return new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static byte[] Hello(
        IReadOnlyList<ushort>? suites = null,
        IReadOnlyList<ushort>? versions = null,
        IReadOnlyList<ushort>? groups = null,
        KeyShareEntry? share = null,
        TlsExtensionType? without = null,
        TlsExtension? extra = null)
    {
        using X25519KeyShare clientShare = new([9, .. new byte[31]]);
        TlsExtension[] extensions =
        [
            SupportedVersionsExtension.EncodeOffered(versions ?? [0x0304]),
            ApplicationLayerProtocolNegotiationExtension.Encode(["h3"]),
            SupportedGroupsExtension.Encode(groups ?? [TlsNamedGroup.X25519]),
            KeyShareExtension.EncodeClientShares([share ?? clientShare.Entry]),
            SignatureAlgorithmsExtension.Encode([TlsSignatureScheme.RsaPssRsaeSha256]),
            QuicTransportParametersExtension.Encode(ClientTransportParameters),
        ];
        return new ClientHello(
            0x0303,
            new byte[ClientHello.RandomLength],
            [],
            suites ?? [0x1301],
            [0],
            [.. extensions.Where(extension => extension.Type != without), .. extra is null ? [] : new[] { extra }]).Encode();
    }

    private static Exchange Drive(Tls13ClientSettings clientSettings, Http3ServerTlsSettings serverSettings)
    {
        Tls13ClientHandshake client = new(clientSettings, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance);
        Http3ServerTlsHandshake server = new(serverSettings, SystemTlsRandomSource.Instance);
        Exchange exchange = new(client, server, [], []);
        Tls13HandshakeOutput toServer = client.Start();
        while (toServer.BytesToSend.Count > 0)
        {
            exchange.ClientSecrets.AddRange(toServer.SecretsInstalled);
            Tls13HandshakeOutput fromServer = Deliver(server, toServer);
            exchange.ServerSecrets.AddRange(fromServer.SecretsInstalled);
            toServer = Deliver(client, fromServer);
        }

        exchange.ClientSecrets.AddRange(toServer.SecretsInstalled);
        return exchange;
    }

    private static Tls13HandshakeOutput Deliver(Http3ServerTlsHandshake server, Tls13HandshakeOutput from) =>
        Merge(from.BytesToSend.Select(bytes => server.Receive(bytes.Level, bytes.Bytes)));

    private static Tls13HandshakeOutput Deliver(Tls13ClientHandshake client, Tls13HandshakeOutput from) =>
        Merge(from.BytesToSend.Select(bytes => client.Receive(bytes.Level, bytes.Bytes)));

    private static Tls13HandshakeOutput Merge(IEnumerable<Tls13HandshakeOutput> outputs)
    {
        Tls13HandshakeOutput[] all = [.. outputs];
        return new(
            [.. all.SelectMany(output => output.BytesToSend)],
            [.. all.SelectMany(output => output.SecretsInstalled)],
            all.Any(output => output.IsComplete),
            all.Select(output => output.Failure).FirstOrDefault(failure => failure is not null));
    }

    private static void AssertSecretsMirror(Exchange exchange)
    {
        Assert.HasCount(4, exchange.ServerSecrets);
        Assert.HasCount(4, exchange.ClientSecrets);
        foreach (Tls13TrafficSecret server in exchange.ServerSecrets)
        {
            TlsTrafficDirection mirrored = server.Direction == TlsTrafficDirection.Read ? TlsTrafficDirection.Write : TlsTrafficDirection.Read;
            Tls13TrafficSecret client = exchange.ClientSecrets.Single(secret => secret.Level == server.Level && secret.Direction == mirrored);
            CollectionAssert.AreEqual(client.Secret, server.Secret, $"{server.Level} {server.Direction}");
        }
    }

    private static void AssertServerAlert(Exchange exchange, TlsAlertDescription alert)
    {
        Assert.IsFalse(exchange.Server.IsComplete);
        Assert.AreEqual(alert, exchange.Server.Failure?.Alert);
    }

    private sealed record Exchange(Tls13ClientHandshake Client, Http3ServerTlsHandshake Server, List<Tls13TrafficSecret> ClientSecrets, List<Tls13TrafficSecret> ServerSecrets);

    private sealed class AcceptingVerifier : IServerCertificateVerifier
    {
        public static AcceptingVerifier Instance { get; } = new();

        public ServerCertificateVerdict Verify(ServerCertificateChain presented) => ServerCertificateVerdict.Accepted;
    }
}
