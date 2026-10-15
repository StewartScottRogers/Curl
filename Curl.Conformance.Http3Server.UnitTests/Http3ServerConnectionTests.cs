using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

[TestClass]
public sealed class Http3ServerConnectionTests
{
    private static readonly X509Certificate2 Certificate = CreateRsaCertificate();

    private static readonly byte[] ClientDestination = [1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly byte[] ClientSource = [9, 9, 9, 9];

    private static Http3ServerTlsSettings ServerSettings => Http3ServerTlsSettings.FromRsaCertificate(Certificate, []);

    [TestMethod]
    public void Receive_CurlQuicClient_CompletesTheHandshakeWithH3()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = CreateClient();

        Pump(client, server, client.Start(), []);

        Assert.IsTrue(client.IsComplete);
        Assert.IsTrue(client.IsConfirmed);
        Assert.IsTrue(server.IsHandshakeComplete);
        Assert.AreEqual("h3", client.Tls.ApplicationProtocol);
        CollectionAssert.AreEqual(server.ConnectionId, client.ServerTransportParameters!.InitialSourceConnectionId);
    }

    [TestMethod]
    public void Write_BidirectionalStream_CarriesBytesAndFinBothWays()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server);
        var request = new byte[2500];
        RandomNumberGenerator.Fill(request);
        var stream = client.Streams.OpenBidirectional()!;
        stream.Write(request, endStream: true);

        Pump(client, server, client.TakeDatagramsToSend(), []);
        var received = server.AcceptStream()!;

        Assert.AreEqual(0UL, received.Id);
        Assert.IsFalse(received.IsUnidirectional);
        Assert.IsTrue(received.IsReadComplete);
        CollectionAssert.AreEqual(request, received.Read());
        Assert.AreEqual(0, received.Read().Length);
        Assert.IsNull(server.AcceptStream());

        server.Write(0, "hello, "u8, endStream: false);
        server.Write(0, "world"u8, endStream: true);
        Pump(client, server, [], server.TakeDatagramsToSend());

        Assert.AreEqual("hello, world", ReadAll(stream));
        Assert.IsTrue(stream.IsReadComplete);
    }

    [TestMethod]
    public void Write_UnidirectionalStreams_ArriveEachWay()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server);
        var control = server.OpenUnidirectionalStream();
        server.Write(control, [0x00], endStream: false);
        server.Write(server.OpenUnidirectionalStream(), [], endStream: true);
        var clientControl = client.Streams.OpenUnidirectional()!;
        clientControl.Write([0x00, 0x04], endStream: false);

        Pump(client, server, client.TakeDatagramsToSend(), server.TakeDatagramsToSend());

        Assert.AreEqual(3UL, control);
        var serverStream = client.Streams.AcceptUnidirectional()!;
        Assert.AreEqual(3UL, serverStream.Id);
        Assert.AreEqual("\0", ReadAll(serverStream));
        Assert.IsTrue(client.Streams.AcceptUnidirectional()!.IsReadComplete);
        var received = server.AcceptStream()!;
        Assert.IsTrue(received.IsUnidirectional);
        Assert.IsFalse(received.IsReadComplete);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x04 }, received.Read());
    }

    [TestMethod]
    public void Receive_ClientConnectionClose_ClosesTheServer()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server);

        var answers = client.CloseWithApplicationError(0x100).SelectMany(datagram => server.Receive(datagram)).ToList();

        Assert.AreEqual(0, answers.Count);
        Assert.IsTrue(server.IsClosed);
        Assert.AreEqual(0x100UL, server.PeerClose!.ErrorCode);
        Assert.AreEqual(0, server.Receive(new byte[1200]).Count);
        Assert.ThrowsExactly<InvalidOperationException>(() => server.Write(0, [], endStream: true));
    }

    [TestMethod]
    public void Close_AfterTheHandshake_ReachesTheClient()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server);

        foreach (var datagram in server.Close(0x101))
        {
            client.Receive(datagram);
        }

        Assert.IsTrue(server.IsClosed);
        Assert.AreEqual(0x101UL, client.Failure!.ServerClose!.ErrorCode);
    }

    [TestMethod]
    public void Close_BeforeTheHandshake_Throws()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);

        Assert.IsNull(server.TlsFailure);
        Assert.ThrowsExactly<InvalidOperationException>(() => server.Close(0));
        Assert.ThrowsExactly<InvalidOperationException>(() => server.OpenUnidirectionalStream());
    }

    [TestMethod]
    public void Receive_InitialOfAnotherVersion_GetsVersionNegotiation()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        var datagram = new byte[1200];
        byte[] header = [0xc0, 0x1a, 0x2a, 0x3a, 0x4a, 8, .. ClientDestination, 4, .. ClientSource];
        header.CopyTo(datagram, 0);

        var answer = (QuicVersionNegotiationPacket)QuicPacketCodec.Decode(server.Receive(datagram).Single(), 0).Packet;

        CollectionAssert.AreEqual(ClientSource, answer.DestinationConnectionId.ToArray());
        CollectionAssert.AreEqual(ClientDestination, answer.SourceConnectionId.ToArray());
        CollectionAssert.AreEqual(new uint[] { QuicPacketCodec.Version1 }, answer.SupportedVersions.ToArray());
        Assert.AreEqual(0, server.Receive(datagram.AsMemory(0, 100)).Count);
    }

    [TestMethod]
    public void Receive_DatagramFailingPacketProtection_IsDropped()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = CreateClient();
        var first = client.Start();
        var damaged = (byte[])first[0].Clone();
        damaged[^1] ^= 0x01;

        Assert.AreEqual(0, server.Receive(damaged).Count);
        Assert.IsNull(server.TlsFailure);

        Pump(client, server, first, []);
        Assert.IsTrue(server.IsHandshakeComplete);
    }

    [TestMethod]
    public void Receive_PacketsTheServerCannotUse_AreDropped()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);

        Assert.AreEqual(0, server.Receive(Unprotected(QuicPacketType.Handshake, ClientDestination)).Count);
        Assert.AreEqual(0, server.Receive(Unprotected(QuicPacketType.Initial, [1, 2, 3, 4])).Count);
        Assert.AreEqual(0, server.Receive(Unprotected(QuicPacketType.ZeroRtt, ClientDestination)).Count);
        Assert.AreEqual(0, server.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(ClientSource, ClientDestination, [1]))).Count);
        Assert.AreEqual(0, server.Receive(new byte[] { 0x40 }).Count);
        Assert.AreEqual(0, server.Receive(Initial(ClientDestination, 0, new QuicPaddingFrame(20))).Count);
        Assert.AreEqual(0, server.Receive(Initial([8, 8, 8, 8, 8, 8, 8, 8], 1, new QuicPingFrame())).Count);
        Assert.AreEqual(0, server.Receive(Unprotected(QuicPacketType.Handshake, server.ConnectionId)).Count);
        Assert.AreEqual(0, server.Receive(QuicPacketCodec.Encode(new QuicShortHeaderPacket(server.ConnectionId, 1, 0, new byte[30]))).Count);
        Assert.AreEqual(0, server.Receive(QuicPacketCodec.Encode(new QuicShortHeaderPacket(ClientDestination, 1, 0, new byte[30]))).Count);
    }

    [TestMethod]
    public void Receive_RepeatedPacket_IsAcknowledgedOnce()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        var ping = Initial(ClientDestination, 0, new QuicPingFrame());

        var acknowledgement = OpenServerInitial(server.Receive(ping).Single());

        Assert.AreEqual(0UL, ((QuicAckFrame)acknowledgement.Single()).LargestAcknowledged);
        Assert.AreEqual(0, server.Receive(ping).Count);
    }

    [TestMethod]
    public void Receive_UndecodableFrame_ClosesWithItsTransportError()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);

        var frames = OpenServerInitial(server.Receive(InitialPayload(ClientDestination, 0, [0x21, .. new byte[30]])).Single());

        Assert.IsTrue(server.IsClosed);
        Assert.AreEqual((ulong)QuicTransportErrorCode.FrameEncodingError, ((QuicConnectionCloseFrame)frames.Single()).ErrorCode);
    }

    [TestMethod]
    public void Receive_ClientWithoutH3_ClosesWithTheTlsAlert()
    {
        using var server = new Http3ServerConnection(ServerSettings, SystemTlsRandomSource.Instance);
        using var client = CreateClient(QuicClientSettings.CreateOpenSslTlsSettings("localhost") with { ApplicationProtocols = ["h2"] });

        Pump(client, server, client.Start(), []);

        Assert.AreEqual(TlsAlertDescription.NoApplicationProtocol, server.TlsFailure!.Alert);
        Assert.IsTrue(server.IsClosed);
        Assert.IsNotNull(client.Failure);
    }

    [TestMethod]
    public void Receive_ServerFlightOverThreeTimesTheClientInitial_HoldsTheRestUntilTheAddressIsValidated()
    {
        using var server = new Http3ServerConnection(ServerSettings with { CertificateChain = [.. Enumerable.Repeat(Certificate.RawData, 6)] }, SystemTlsRandomSource.Instance);
        using var client = CreateClient();
        var first = client.Start();

        var answer = first.SelectMany(datagram => server.Receive(datagram)).ToList();

        Assert.IsTrue(answer.Sum(datagram => datagram.Length) <= Http3ServerConnection.AmplificationFactor * first.Sum(datagram => datagram.Length));
        Assert.AreEqual(0, server.TakeDatagramsToSend().Count);
        Pump(client, server, [], answer);
        Assert.IsTrue(client.IsConfirmed);
    }

    [TestMethod]
    public void BuildAcknowledgement_GapsInThePacketNumbers_ListsEachRange()
    {
        using var space = new Http3ServerPacketSpace(QuicPacketType.Initial, TlsEncryptionLevel.Initial);
        foreach (var packetNumber in new ulong[] { 0, 1, 2, 5, 9, 10 })
        {
            space.RecordReceived(packetNumber);
        }

        var frame = space.BuildAcknowledgement();

        CollectionAssert.AreEqual(new[] { (9UL, 10UL), (5UL, 5UL), (0UL, 2UL) }, frame.GetAcknowledgedRanges().ToArray());
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ServerConnection(null!, SystemTlsRandomSource.Instance));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ServerConnection(ServerSettings, null!));
    }

    private static QuicClientConnectionState CreateClient(Tls13ClientSettings? tls = null) =>
        new(new QuicClientSettings { Tls = tls ?? QuicClientSettings.CreateOpenSslTlsSettings("localhost") }, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance, TimeProvider.System);

    private static QuicClientConnectionState Connect(Http3ServerConnection server)
    {
        var client = CreateClient();
        Pump(client, server, client.Start(), []);
        Assert.IsTrue(client.IsConfirmed);
        return client;
    }

    private static void Pump(QuicClientConnectionState client, Http3ServerConnection server, IReadOnlyList<byte[]> toServer, IReadOnlyList<byte[]> toClient)
    {
        for (var round = 0; round < 50 && (toServer.Count > 0 || toClient.Count > 0); round++)
        {
            var nextToClient = toServer.SelectMany(datagram => server.Receive(datagram)).ToList();
            var nextToServer = toClient.Concat(nextToClient).SelectMany(datagram => client.Receive(datagram)).ToList();
            toServer = nextToServer;
            toClient = [];
        }
    }

    private static string ReadAll(QuicStream stream)
    {
        var buffer = new byte[4096];
        var read = stream.Read(buffer);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }

    private static byte[] Unprotected(QuicPacketType type, byte[] destination) =>
        QuicPacketCodec.Encode(new QuicLongHeaderPacket(type, QuicPacketCodec.Version1, destination, ClientSource, ReadOnlyMemory<byte>.Empty, 1, 0, new byte[30]));

    private static byte[] Initial(byte[] destination, uint packetNumber, QuicFrame frame) =>
        InitialPayload(destination, packetNumber, QuicFrameCodec.Encode([frame, new QuicPaddingFrame(30)]));

    private static byte[] InitialPayload(byte[] destination, uint packetNumber, byte[] payload)
    {
        using var protection = QuicPacketProtection.CreateClientInitial(ClientDestination);
        return protection.Protect(new QuicLongHeaderPacket(QuicPacketType.Initial, QuicPacketCodec.Version1, destination, ClientSource, ReadOnlyMemory<byte>.Empty, 4, packetNumber, payload), packetNumber);
    }

    private static IReadOnlyList<QuicFrame> OpenServerInitial(byte[] datagram)
    {
        using var protection = QuicPacketProtection.CreateServerInitial(ClientDestination);
        var packet = (QuicLongHeaderPacket)protection.Unprotect(datagram, 0, null).Packet!;
        return QuicFrameCodec.Decode(packet.Payload, QuicPacketType.Initial);
    }

    private static X509Certificate2 CreateRsaCertificate()
    {
        using RSA key = RSA.Create(2048);
        return new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class AcceptingVerifier : IServerCertificateVerifier
    {
        public static AcceptingVerifier Instance { get; } = new();

        public ServerCertificateVerdict Verify(ServerCertificateChain presented) => ServerCertificateVerdict.Accepted;
    }
}
