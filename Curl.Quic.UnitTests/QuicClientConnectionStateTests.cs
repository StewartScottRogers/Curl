using System.Security.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

[TestClass]
public sealed class QuicClientConnectionStateTests
{
    // QuicTestRandomSource hands out 00, 01, 02, ...: the destination connection ID first, then the source.
    private static readonly byte[] OriginalDestinationConnectionId = [.. Enumerable.Range(0, 20).Select(value => (byte)value)];

    private static readonly byte[] SourceConnectionId = [.. Enumerable.Range(20, 20).Select(value => (byte)value)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Start_FixedRandomness_SendsThePinned1200ByteInitial()
    {
        Diagnostics.Arrange("original destination connection ID", HexOf(OriginalDestinationConnectionId));
        Diagnostics.Arrange("source connection ID", HexOf(SourceConnectionId));
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        byte[] datagram = client.Start().Single();

        Diagnostics.Bytes("client Initial datagram (packet number 0)", datagram);
        Diagnostics.Act("datagram length", datagram.Length);
        Diagnostics.Assert("datagram length", 1200, datagram.Length);
        Assert.AreEqual(1200, datagram.Length);
        string hash = HexOf(SHA256.HashData(datagram));
        Diagnostics.Act("datagram SHA-256", hash);
        Diagnostics.Assert("datagram SHA-256", FirstInitialSha256, hash);
        Assert.AreEqual(FirstInitialSha256, hash);
    }

    [TestMethod]
    public void Start_CurlSettings_SendsCurlsClientHelloInOneCryptoFrameThenPadding()
    {
        Diagnostics.Arrange("original destination connection ID", HexOf(OriginalDestinationConnectionId));
        Diagnostics.Arrange("source connection ID", HexOf(SourceConnectionId));
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        byte[] datagram = client.Start().Single();

        (QuicLongHeaderPacket packet, IReadOnlyList<QuicFrame> frames) = OpenClientInitial(datagram);
        QuicCryptoFrame crypto = (QuicCryptoFrame)frames[0];
        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(crypto.Data.Span).Message!.Body).Value;

        Diagnostics.Bytes("client Initial datagram (packet number 0)", datagram);
        Diagnostics.Act("packet destination / source connection ID", FormattableString.Invariant($"{HexOf(packet.DestinationConnectionId)} / {HexOf(packet.SourceConnectionId)}"));
        Diagnostics.Act("packet number length / truncated packet number", FormattableString.Invariant($"{packet.PacketNumberLength} / {packet.TruncatedPacketNumber}"));
        Diagnostics.Act("frames", string.Join(", ", frames.Select(frame => frame.GetType().Name)));
        Diagnostics.Assert("destination connection ID", HexOf(OriginalDestinationConnectionId), HexOf(packet.DestinationConnectionId));
        Assert.AreEqual(HexOf(OriginalDestinationConnectionId), HexOf(packet.DestinationConnectionId));
        Diagnostics.Assert("source connection ID", HexOf(SourceConnectionId), HexOf(packet.SourceConnectionId));
        Assert.AreEqual(HexOf(SourceConnectionId), HexOf(packet.SourceConnectionId));
        Diagnostics.Assert("token is empty", true, packet.Token.IsEmpty);
        Assert.IsTrue(packet.Token.IsEmpty);
        Assert.AreEqual((1, 0u), (packet.PacketNumberLength, packet.TruncatedPacketNumber));
        Diagnostics.Assert("CRYPTO frame offset", 0UL, crypto.Offset);
        Assert.AreEqual(0UL, crypto.Offset);
        Diagnostics.Assert("second frame type", nameof(QuicPaddingFrame), frames[1].GetType().Name);
        Assert.IsInstanceOfType<QuicPaddingFrame>(frames[1]);
        Diagnostics.Assert("frame count", 2, frames.Count);
        Assert.HasCount(2, frames);
        Diagnostics.Act("cipher suites", string.Join(' ', hello.CipherSuites.ToArray().Select(suite => suite.ToString("x4", System.Globalization.CultureInfo.InvariantCulture))));
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301, 0x00ff }, hello.CipherSuites.ToArray());
        Diagnostics.Assert("legacy session id length", 0, hello.LegacySessionId.Length);
        Assert.IsEmpty(hello.LegacySessionId);
        TlsExtensionType[] expectedOrder = [TlsExtensionType.QuicTransportParameters, TlsExtensionType.ServerName, TlsExtensionType.EcPointFormats, TlsExtensionType.SupportedGroups, TlsExtensionType.KeyShare, TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms];
        TlsExtensionType[] actualOrder = hello.Extensions.Select(extension => extension.Type).ToArray();
        Diagnostics.Assert("extension order", string.Join(", ", expectedOrder), string.Join(", ", actualOrder));
        CollectionAssert.AreEqual(expectedOrder, actualOrder);
        string expectedParameters = HexOf((QuicTransportParameters.CurlClientDefaults with { InitialSourceConnectionId = SourceConnectionId }).Encode());
        Diagnostics.Assert("transport parameters extension", expectedParameters, HexOf(hello.Extensions[0].Data));
        Assert.AreEqual(expectedParameters, HexOf(hello.Extensions[0].Data));
        Diagnostics.Assert("client's own transport parameters", HexOf(hello.Extensions[0].Data), HexOf(client.TransportParameters.Encode()));
        Assert.AreEqual(HexOf(hello.Extensions[0].Data), HexOf(client.TransportParameters.Encode()));
        string[] protocols = ApplicationLayerProtocolNegotiationExtension.Decode(hello.Extensions[5].Data).Value.ToArray();
        Diagnostics.Assert("ALPN protocols", "h3, h3-29", string.Join(", ", protocols));
        CollectionAssert.AreEqual(new[] { "h3", "h3-29" }, protocols);
    }

    [TestMethod]
    public void Receive_InMemoryServer_CompletesConfirmsAndDiscardsEarlierKeys()
    {
        Diagnostics.Arrange("server", "in-memory QUIC server");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        Diagnostics.Act("complete / confirmed", FormattableString.Invariant($"{client.IsComplete} / {client.IsConfirmed}"));
        Diagnostics.Act("Initial / Handshake keys discarded", FormattableString.Invariant($"{client.InitialKeysDiscarded} / {client.HandshakeKeysDiscarded}"));
        Diagnostics.Assert("handshake complete", true, client.IsComplete);
        Assert.IsTrue(client.IsComplete);
        Diagnostics.Assert("handshake confirmed", true, client.IsConfirmed);
        Assert.IsTrue(client.IsConfirmed);
        Diagnostics.Assert("Initial keys discarded", true, client.InitialKeysDiscarded);
        Assert.IsTrue(client.InitialKeysDiscarded);
        Diagnostics.Assert("Handshake keys discarded", true, client.HandshakeKeysDiscarded);
        Assert.IsTrue(client.HandshakeKeysDiscarded);
        Diagnostics.Assert("failure", "null", client.Failure?.Message ?? "null");
        Assert.IsNull(client.Failure);
        Diagnostics.Assert("application protocol", "h3", client.Tls.ApplicationProtocol);
        Assert.AreEqual("h3", client.Tls.ApplicationProtocol);
        Diagnostics.Assert("server max_idle_timeout", 30000UL, client.ServerTransportParameters!.MaxIdleTimeout);
        Assert.AreEqual(30000UL, client.ServerTransportParameters!.MaxIdleTimeout);
        Diagnostics.Assert("peer connection ID", HexOf(server.ServerConnectionId), HexOf(client.PeerConnectionIds!.Current.ConnectionId));
        Assert.AreEqual(HexOf(server.ServerConnectionId), HexOf(client.PeerConnectionIds!.Current.ConnectionId));
        Diagnostics.Assert("stateless reset token", HexOf(server.StatelessResetToken), HexOf(client.PeerConnectionIds.Current.StatelessResetToken!));
        Assert.AreEqual(HexOf(server.StatelessResetToken), HexOf(client.PeerConnectionIds.Current.StatelessResetToken!));
        Diagnostics.Assert("server received the client Finished", true, server.ClientFinishedReceived);
        Assert.IsTrue(server.ClientFinishedReceived);
    }

    [TestMethod]
    public void Receive_HandshakeComplete_IssuesConnectionIdsUpToTheServersLimit()
    {
        Diagnostics.Arrange("server", "in-memory QUIC server");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        List<QuicNewConnectionIdFrame> issued = [.. server.ClientPackets.SelectMany(packet => packet.Frames).OfType<QuicNewConnectionIdFrame>()];

        ulong[] sequenceNumbers = issued.Select(frame => frame.SequenceNumber).ToArray();
        Diagnostics.Act("NEW_CONNECTION_ID sequence numbers", string.Join(", ", sequenceNumbers));
        Diagnostics.Assert("NEW_CONNECTION_ID sequence numbers", "1, 2, 3", string.Join(", ", sequenceNumbers));
        CollectionAssert.AreEqual(new ulong[] { 1, 2, 3 }, sequenceNumbers);
        Diagnostics.Assert("active local connection IDs", 4, client.LocalConnectionIds.Active.Count);
        Assert.HasCount(4, client.LocalConnectionIds.Active);
    }

    [TestMethod]
    public void Receive_FramesAfterTheHandshake_KeepsTokensRetiresIdsAndTakesTickets()
    {
        Diagnostics.Arrange("frames after the handshake", "HANDSHAKE_DONE, NEW_TOKEN 0707, NEW_CONNECTION_ID seq 1, RETIRE_CONNECTION_ID 1, PING; session ticket sent");
        using QuicTestServer server = new()
        {
            SendSessionTicket = true,
            FramesAfterHandshake = _ =>
            [
                new QuicHandshakeDoneFrame(),
                new QuicNewTokenFrame(new byte[] { 7, 7 }),
                new QuicNewConnectionIdFrame(1, 1, new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }, new byte[16]),
                new QuicRetireConnectionIdFrame(1),
                new QuicPingFrame(),
            ],
        };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        List<QuicFrame> sent = [.. server.ClientPackets.Where(packet => packet.Type == QuicPacketType.OneRtt).SelectMany(packet => packet.Frames)];

        Diagnostics.Act("1-RTT frames the client sent", string.Join(", ", sent.Select(frame => frame.GetType().Name)));
        Diagnostics.Assert("received token", "0707", HexOf(client.ReceivedTokens.Single()));
        Assert.AreEqual("0707", HexOf(client.ReceivedTokens.Single()));
        Diagnostics.Assert("current peer connection ID sequence number", 1UL, client.PeerConnectionIds!.Current.SequenceNumber);
        Assert.AreEqual(1UL, client.PeerConnectionIds!.Current.SequenceNumber);
        Diagnostics.Assert("retired sequence number", 0UL, sent.OfType<QuicRetireConnectionIdFrame>().Single().SequenceNumber);
        Assert.AreEqual(0UL, sent.OfType<QuicRetireConnectionIdFrame>().Single().SequenceNumber);
        Diagnostics.Assert("last issued sequence number", 4UL, sent.OfType<QuicNewConnectionIdFrame>().Last().SequenceNumber);
        Assert.AreEqual(4UL, sent.OfType<QuicNewConnectionIdFrame>().Last().SequenceNumber);
        Diagnostics.Assert("received tickets", 1, client.Tls.ReceivedTickets.Count);
        Assert.HasCount(1, client.Tls.ReceivedTickets);
        bool acked = sent.OfType<QuicAckFrame>().Any();
        Diagnostics.Assert("client sent an ACK", true, acked);
        Assert.IsTrue(acked);
    }

    [TestMethod]
    public void Receive_Retry_ResendsTheClientHelloWithItsTokenAndConnectionId()
    {
        Diagnostics.Arrange("server", "in-memory QUIC server that sends a Retry");
        using QuicTestServer server = new() { SendRetry = true };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        List<byte[]> sent;
        using (Diagnostics.Phase("handshake"))
        {
            sent = Run(client, server);
        }

        QuicLongHeaderPacket second = (QuicLongHeaderPacket)QuicPacketCodec.Decode(sent[1], QuicClientSettings.CurlConnectionIdLength).Packet;

        Diagnostics.Bytes("second client Initial (after the Retry)", sent[1]);
        Diagnostics.Act("second Initial token / destination connection ID", FormattableString.Invariant($"{HexOf(second.Token)} / {HexOf(second.DestinationConnectionId)}"));
        Diagnostics.Assert("handshake complete", true, client.IsComplete);
        Assert.IsTrue(client.IsComplete);
        Diagnostics.Assert("second Initial length", 1200, sent[1].Length);
        Assert.AreEqual(1200, sent[1].Length);
        Diagnostics.Assert("second Initial token", HexOf(server.RetryToken), HexOf(second.Token));
        Assert.AreEqual(HexOf(server.RetryToken), HexOf(second.Token));
        Diagnostics.Assert("second Initial destination connection ID", HexOf(server.RetrySourceConnectionId!), HexOf(second.DestinationConnectionId));
        Assert.AreEqual(HexOf(server.RetrySourceConnectionId!), HexOf(second.DestinationConnectionId));
        Diagnostics.Assert("retry_source_connection_id", HexOf(server.RetrySourceConnectionId!), HexOf(client.ServerTransportParameters!.RetrySourceConnectionId!));
        Assert.AreEqual(HexOf(server.RetrySourceConnectionId!), HexOf(client.ServerTransportParameters!.RetrySourceConnectionId!));
    }

    [TestMethod]
    public void Receive_SecondInvalidOrForeignRetry_IsIgnored()
    {
        Diagnostics.Arrange("original destination connection ID", HexOf(OriginalDestinationConnectionId));
        Diagnostics.Arrange("Retry packets", "unsigned, same source connection ID, other destination connection ID, valid, valid again");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();
        QuicRetryPacket unsigned = new(QuicPacketCodec.Version1, SourceConnectionId, new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }, new byte[] { 1 }, new byte[16]);
        QuicRetryPacket valid = unsigned with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, unsigned) };
        QuicRetryPacket otherConnection = valid with { DestinationConnectionId = new byte[20] };
        QuicRetryPacket sameConnectionId = valid with { SourceConnectionId = OriginalDestinationConnectionId };

        IReadOnlyList<byte[]> unsignedReplies = client.Receive(QuicPacketCodec.Encode(unsigned));
        IReadOnlyList<byte[]> sameIdReplies = client.Receive(QuicPacketCodec.Encode(sameConnectionId with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, sameConnectionId) }));
        IReadOnlyList<byte[]> otherConnectionReplies = client.Receive(QuicPacketCodec.Encode(otherConnection with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, otherConnection) }));
        IReadOnlyList<byte[]> validReplies = client.Receive(QuicPacketCodec.Encode(valid));
        IReadOnlyList<byte[]> secondValidReplies = client.Receive(QuicPacketCodec.Encode(valid));

        string counts = FormattableString.Invariant($"{unsignedReplies.Count}, {sameIdReplies.Count}, {otherConnectionReplies.Count}, {validReplies.Count}, {secondValidReplies.Count}");
        Diagnostics.Act("datagrams answering each Retry", counts);
        Diagnostics.Assert("datagrams answering each Retry", "0, 0, 0, 1, 0", counts);
        Assert.IsEmpty(unsignedReplies);
        Assert.IsEmpty(sameIdReplies);
        Assert.IsEmpty(otherConnectionReplies);
        Assert.HasCount(1, validReplies);
        Assert.IsEmpty(secondValidReplies);
        Diagnostics.Assert("failure", "null", client.Failure?.Message ?? "null");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void Receive_VersionNegotiationWithoutVersion1_FailsWithExit7()
    {
        Diagnostics.Arrange("server versions offered", "0xff00001d, 0x6b3343cf");
        using QuicTestServer server = new() { VersionNegotiation = [0xff00001d, 0x6b3343cf] };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        List<byte[]> sent;
        using (Diagnostics.Phase("handshake"))
        {
            sent = Run(client, server);
        }

        Diagnostics.Act("failure", FormattableString.Invariant($"{client.Failure?.ExitCode}: {client.Failure?.Message}"));
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Diagnostics.Assert("message lists the offered versions", true, client.Failure.Message.Contains("0xff00001d, 0x6b3343cf", StringComparison.Ordinal));
        StringAssert.Contains(client.Failure.Message, "0xff00001d, 0x6b3343cf");
        Diagnostics.Assert("datagrams sent", 1, sent.Count);
        Assert.HasCount(1, sent);
    }

    [TestMethod]
    public void Receive_VersionNegotiationListingVersion1ForeignOrLate_IsIgnored()
    {
        Diagnostics.Arrange("Version Negotiation packets", "lists version 1, foreign connection ID, arriving after a server Initial");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();

        IReadOnlyList<byte[]> listsVersion1 = client.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(SourceConnectionId, new byte[] { 1 }, [2, QuicPacketCodec.Version1])));
        IReadOnlyList<byte[]> foreign = client.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(new byte[] { 5 }, new byte[] { 1 }, [2])));
        client.Receive(ServerInitial(SourceConnectionId, [8, 8, 8, 8], 0, new QuicPingFrame()));
        IReadOnlyList<byte[]> late = client.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(SourceConnectionId, new byte[] { 1 }, [2])));

        string counts = FormattableString.Invariant($"{listsVersion1.Count} / {foreign.Count} / {late.Count}");
        Diagnostics.Act("datagrams answering listing version 1 / foreign / late", counts);
        Diagnostics.Assert("datagrams answering listing version 1 / foreign / late", "0 / 0 / 0", counts);
        Assert.IsEmpty(listsVersion1);
        Assert.IsEmpty(foreign);
        Assert.IsEmpty(late);
        Diagnostics.Assert("failure", "null", client.Failure?.Message ?? "null");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    [DataRow("original_destination_connection_id", DisplayName = "original_destination_connection_id missing")]
    [DataRow("initial_source_connection_id", DisplayName = "initial_source_connection_id different")]
    [DataRow("retry_source_connection_id", DisplayName = "retry_source_connection_id without a Retry")]
    public void Receive_MismatchedServerTransportParameter_ClosesWithTransportParameterError(string parameter)
    {
        Diagnostics.Arrange("mismatched server transport parameter", parameter);
        Func<QuicTransportParameters, QuicTransportParameters> change = parameter switch
        {
            "original_destination_connection_id" => parameters => parameters with { OriginalDestinationConnectionId = null },
            "initial_source_connection_id" => parameters => parameters with { InitialSourceConnectionId = [1] },
            _ => parameters => parameters with { RetrySourceConnectionId = [1] },
        };
        using QuicTestServer server = new() { EncodeTransportParameters = parameters => change(parameters).Encode() };

        QuicClientConnectionState client = AssertClosesWith(Diagnostics, server, QuicTransportErrorCode.TransportParameterError, CurlExitCode.CouldntConnect);

        Diagnostics.Act("failure message", client.Failure!.Message);
        Diagnostics.Assert("message names the parameter", true, client.Failure.Message.Contains(parameter, StringComparison.Ordinal));
        StringAssert.Contains(client.Failure!.Message, parameter);
        client.Dispose();
    }

    [TestMethod]
    public void Receive_MalformedServerTransportParameters_ClosesWithTransportParameterError()
    {
        Diagnostics.Arrange("server transport parameters bytes", "01 05 (malformed)");
        using QuicTestServer server = new() { EncodeTransportParameters = _ => [0x01, 0x05] };

        AssertClosesWith(Diagnostics, server, QuicTransportErrorCode.TransportParameterError, CurlExitCode.CouldntConnect).Dispose();
    }

    [TestMethod]
    public void Receive_ServerChoosesAnotherVersion_ClosesWithVersionNegotiationError()
    {
        Diagnostics.Arrange("server version_information", "chosen version 2, available [2]");
        using QuicTestServer server = new() { EncodeTransportParameters = parameters => (parameters with { VersionInformation = new QuicVersionInformation(2, [2]) }).Encode() };

        AssertClosesWith(Diagnostics, server, QuicTransportErrorCode.VersionNegotiationError, CurlExitCode.CouldntConnect).Dispose();
    }

    [TestMethod]
    public void Receive_ServerChoosesVersion1_Completes()
    {
        Diagnostics.Arrange("server version_information", "version 1 only");
        using QuicTestServer server = new() { EncodeTransportParameters = parameters => (parameters with { VersionInformation = QuicVersionInformation.Version1Only }).Encode() };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        Diagnostics.Act("handshake complete", client.IsComplete);
        Diagnostics.Assert("handshake complete", true, client.IsComplete);
        Assert.IsTrue(client.IsComplete);
    }

    [TestMethod]
    public void Receive_AcknowledgementOfAPacketNeverSent_ClosesWithProtocolViolation()
    {
        Diagnostics.Arrange("server Initial frame", "ACK of packet 5, which the client never sent");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();

        byte[] close = client.Receive(ServerInitial(SourceConnectionId, [8], 0, new QuicAckFrame(5, 0, 0, [], null))).Single();

        ulong errorCode = ((QuicConnectionCloseFrame)OpenClientInitial(close).Frames[0]).ErrorCode;
        Diagnostics.Bytes("client close datagram (Initial, packet number 1)", close);
        Diagnostics.Act("failure exit code / CONNECTION_CLOSE error code", FormattableString.Invariant($"{client.Failure?.ExitCode} / {errorCode}"));
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", QuicTransportErrorCode.ProtocolViolation, (QuicTransportErrorCode)errorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, errorCode);
    }

    [TestMethod]
    public void Receive_NoServerTransportParameters_ClosesWithMissingExtension()
    {
        Diagnostics.Arrange("server transport parameters", "none (extension missing)");
        using QuicTestServer server = new() { EncodeTransportParameters = _ => null };

        AssertClosesWith(Diagnostics, server, QuicTransportErrorCode.CryptoErrorBase + 109, CurlExitCode.SslConnectError).Dispose();
    }

    [TestMethod]
    public void Receive_NoApplicationProtocol_ClosesWithNoApplicationProtocol()
    {
        Diagnostics.Arrange("server application protocol", "none");
        using QuicTestServer server = new() { ApplicationProtocol = null };

        AssertClosesWith(Diagnostics, server, QuicTransportErrorCode.CryptoErrorBase + 120, CurlExitCode.SslConnectError).Dispose();
    }

    [TestMethod]
    public void Receive_TlsAlertFromTheClient_ClosesWithItsCryptoErrorAndExit35()
    {
        Diagnostics.Arrange("server application protocol", "h2 (client offered h3, h3-29)");
        using QuicTestServer server = new() { ApplicationProtocol = "h2" };

        QuicClientConnectionState client = AssertClosesWith(Diagnostics, server, QuicTransportErrorCode.CryptoErrorBase + (int)TlsAlertDescription.IllegalParameter, CurlExitCode.SslConnectError);

        Diagnostics.Act("TLS failure alert / message", FormattableString.Invariant($"{client.Failure!.TlsFailure!.Alert} / {client.Failure.Message}"));
        Diagnostics.Assert("TLS alert", TlsAlertDescription.IllegalParameter, client.Failure.TlsFailure.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, client.Failure!.TlsFailure!.Alert);
        Diagnostics.Assert("message names the alert", true, client.Failure.Message.Contains("IllegalParameter", StringComparison.Ordinal));
        StringAssert.Contains(client.Failure.Message, "IllegalParameter");
        client.Dispose();
    }

    [TestMethod]
    public void Receive_RejectedCertificate_ClosesWithBadCertificateAndExit60()
    {
        Diagnostics.Arrange("certificate verifier rejection", "SSL certificate problem: self-signed certificate");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(verifier: new QuicTestVerifier { Rejection = "SSL certificate problem: self-signed certificate" });

        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        ulong closeCode = ClientClose(server).ErrorCode;
        Diagnostics.Act("failure", FormattableString.Invariant($"{client.Failure?.ExitCode}: {client.Failure?.Message}"));
        Diagnostics.Act("CONNECTION_CLOSE error code", closeCode);
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, client.Failure!.ExitCode);
        Diagnostics.Assert("message", "SSL certificate problem: self-signed certificate", client.Failure.Message);
        Assert.AreEqual("SSL certificate problem: self-signed certificate", client.Failure.Message);
        Diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.CryptoErrorBase + 42, closeCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.CryptoErrorBase + 42, closeCode);
    }

    [TestMethod]
    [DataRow(0x128UL, CurlExitCode.CouldntConnect, "TLS alert HandshakeFailure (refused)", DisplayName = "A TLS alert from the server is exit 7")]
    [DataRow(0x02UL, CurlExitCode.WeirdServerReply, "transport error ConnectionRefused (refused)", DisplayName = "CONNECTION_REFUSED is exit 8")]
    [DataRow(0x0aUL, CurlExitCode.CouldntConnect, "transport error ProtocolViolation (refused)", DisplayName = "Another transport error is exit 7")]
    public void Receive_ServerCloseDuringTheHandshake_FailsWithTheMappedExit(ulong errorCode, CurlExitCode exitCode, string description)
    {
        Diagnostics.Arrange("server CONNECTION_CLOSE error code", FormattableString.Invariant($"0x{errorCode:x}"));
        Diagnostics.Arrange("expected exit code", exitCode);
        using QuicTestServer server = new() { CloseAfterClientHello = errorCode };
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        List<byte[]> sent;
        using (Diagnostics.Phase("handshake"))
        {
            sent = Run(client, server);
        }

        Diagnostics.Act("failure", FormattableString.Invariant($"{client.Failure?.ExitCode}: {client.Failure?.Message}"));
        Diagnostics.Assert("exit code", exitCode, client.Failure!.ExitCode);
        Assert.AreEqual(exitCode, client.Failure!.ExitCode);
        Diagnostics.Assert("message", $"The server closed the QUIC connection with {description}.", client.Failure.Message);
        Assert.AreEqual($"The server closed the QUIC connection with {description}.", client.Failure.Message);
        Diagnostics.Assert("datagrams sent", 1, sent.Count);
        Assert.HasCount(1, sent);
        IReadOnlyList<byte[]> replies = client.Receive(new byte[1200]);
        Diagnostics.Assert("datagrams answering a datagram after the close", 0, replies.Count);
        Assert.IsEmpty(replies);
    }

    [TestMethod]
    public void Receive_FramesAfterAClose_AreNotRead()
    {
        Diagnostics.Arrange("server Initial datagrams", "packet 0: CONNECTION_CLOSE 0x0a then PING; packet 1: PING");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();
        QuicConnectionCloseFrame close = new(0x0a, 0, ReadOnlyMemory<byte>.Empty);

        byte[] datagram = [.. ServerInitial(SourceConnectionId, [8], 0, close, new QuicPingFrame()), .. ServerInitial(SourceConnectionId, [8], 1, new QuicPingFrame())];

        IReadOnlyList<byte[]> replies = client.Receive(datagram);
        Diagnostics.Bytes("coalesced server datagram", datagram);
        Diagnostics.Act("replies / failure exit code", FormattableString.Invariant($"{replies.Count} / {client.Failure?.ExitCode}"));
        Diagnostics.Assert("replies", 0, replies.Count);
        Assert.IsEmpty(replies);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
    }

    [TestMethod]
    public void Receive_ForbiddenFrame_ClosesWithProtocolViolation()
    {
        Diagnostics.Arrange("server Initial frame", "HANDSHAKE_DONE (forbidden in an Initial)");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();

        byte[] close = client.Receive(ServerInitial(SourceConnectionId, [8], 0, new QuicHandshakeDoneFrame())).Single();

        ulong errorCode = ((QuicConnectionCloseFrame)OpenClientInitial(close, [8]).Frames[0]).ErrorCode;
        Diagnostics.Bytes("client close datagram (Initial, destination connection ID 08)", close);
        Diagnostics.Act("failure exit code / CONNECTION_CLOSE error code", FormattableString.Invariant($"{client.Failure?.ExitCode} / {errorCode}"));
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", QuicTransportErrorCode.ProtocolViolation, (QuicTransportErrorCode)errorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, errorCode);
    }

    [TestMethod]
    public void Receive_UnreadableForeignDuplicateOrUnkeyedPackets_AreDropped()
    {
        Diagnostics.Arrange("datagrams", "1 byte 0x40, tampered Initial, foreign destination ID, 0-RTT, short header to a foreign ID, valid PING, duplicate, wrong source ID, CRYPTO at offset 5 with ACK");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();
        byte[] ping = ServerInitial(SourceConnectionId, [8], 0, new QuicPingFrame());
        byte[] tampered = [.. ping];
        tampered[^1] ^= 1;
        Diagnostics.Bytes("valid server Initial (PING, packet number 0, source ID 08)", ping);
        Diagnostics.Bytes("tampered server Initial (last byte flipped)", tampered);

        IReadOnlyList<byte[]>[] replies =
        [
            client.Receive(new byte[] { 0x40 }),
            client.Receive(tampered),
            client.Receive(ServerInitial(new byte[20], [8], 0, new QuicPingFrame())),
            client.Receive(ServerLongHeader(QuicPacketType.ZeroRtt, SourceConnectionId, [8], 0, new QuicPingFrame())),
            client.Receive((byte[])[0x41, .. SourceConnectionId, .. new byte[30]]),
            client.Receive(ping),
            client.Receive(ping),
            client.Receive(ServerInitial(SourceConnectionId, [9], 1, new QuicPingFrame())),
            client.Receive(ServerInitial(SourceConnectionId, [8], 2, new QuicCryptoFrame(5, new byte[] { 1 }), new QuicAckFrame(0, 0, 0, [], null))),
        ];

        string counts = string.Join(", ", replies.Select(reply => reply.Count));
        Diagnostics.Act("datagrams answering each", counts);
        Diagnostics.Assert("datagrams answering each", "0, 0, 0, 0, 0, 1, 0, 0, 1", counts);
        Assert.IsEmpty(replies[0]);
        Assert.IsEmpty(replies[1]);
        Assert.IsEmpty(replies[2]);
        Assert.IsEmpty(replies[3]);
        Assert.IsEmpty(replies[4]);
        Assert.HasCount(1, replies[5]);
        Assert.IsEmpty(replies[6]);
        Assert.IsEmpty(replies[7]);
        Assert.HasCount(1, replies[8]);
        Diagnostics.Assert("peer connection ID", "08", HexOf(client.PeerConnectionIds!.Current.ConnectionId));
        Assert.AreEqual("08", HexOf(client.PeerConnectionIds!.Current.ConnectionId));
        Diagnostics.Assert("failure", "null", client.Failure?.Message ?? "null");
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void Receive_AfterTheHandshakeDiscardedKeysOrForeignIds_AreDropped()
    {
        Diagnostics.Arrange("datagrams after the handshake", "Initial PING, Handshake PING, short header to a foreign ID, 1-RTT PING");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        IReadOnlyList<byte[]>[] replies =
        [
            client.Receive(server.Protect(QuicPacketType.Initial, new QuicPingFrame())),
            client.Receive(server.Protect(QuicPacketType.Handshake, new QuicPingFrame())),
            client.Receive((byte[])[0x41, .. new byte[20], .. new byte[30]]),
            client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicPingFrame())),
        ];

        string counts = string.Join(", ", replies.Select(reply => reply.Count));
        Diagnostics.Act("datagrams answering each", counts);
        Diagnostics.Assert("datagrams answering each", "0, 0, 0, 1", counts);
        Assert.IsEmpty(replies[0]);
        Assert.IsEmpty(replies[1]);
        Assert.IsEmpty(replies[2]);
        Assert.HasCount(1, replies[3]);
    }

    [TestMethod]
    public void Start_ClientHelloLargerThanOnePacket_SendsTwoFullDatagrams()
    {
        Diagnostics.Arrange("extra ClientHello extension", "type 0x7a7a, 1100 bytes");
        QuicClientSettings settings = QuicClientConnectionStateTest.CurlSettings with
        {
            Tls = QuicClientConnectionStateTest.CurlSettings.Tls with { FixedExtensions = [.. QuicClientConnectionStateTest.CurlSettings.Tls.FixedExtensions, new TlsExtension((TlsExtensionType)0x7a7a, new byte[1100])] },
        };
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(settings);

        List<byte[]> sent;
        using (Diagnostics.Phase("handshake"))
        {
            sent = Run(client, server);
        }

        Diagnostics.Act("lengths of the first two datagrams", string.Join(", ", sent.Take(2).Select(datagram => datagram.Length)));
        bool bothFull = sent.Take(2).All(datagram => datagram.Length == 1200);
        Diagnostics.Assert("first two datagrams are 1200 bytes", true, bothFull);
        Assert.IsTrue(bothFull);
        ulong secondOffset = ((QuicCryptoFrame)OpenClientInitial(sent[1]).Frames[0]).Offset;
        Diagnostics.Act("CRYPTO offset in the second datagram", secondOffset);
        Diagnostics.Assert("CRYPTO offset in the second datagram exceeds 1100", true, secondOffset > 1100);
        Assert.IsTrue(secondOffset > 1100);
        Diagnostics.Assert("handshake complete", true, client.IsComplete);
        Assert.IsTrue(client.IsComplete);
    }

    [TestMethod]
    public void Abandon_AfterStart_ClosesAtEveryLevelWithKeys()
    {
        Diagnostics.Arrange("abandon", "InternalError with failure SendError \"timeout\"");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();

        byte[] close = client.Abandon(QuicTransportErrorCode.InternalError, new QuicHandshakeFailure(CurlExitCode.SendError, "timeout")).Single();

        ulong errorCode = ((QuicConnectionCloseFrame)OpenClientInitial(close).Frames[0]).ErrorCode;
        Diagnostics.Bytes("client close datagram (Initial)", close);
        Diagnostics.Act("close length / CONNECTION_CLOSE error code / failure exit code", FormattableString.Invariant($"{close.Length} / {errorCode} / {client.Failure?.ExitCode}"));
        Diagnostics.Assert("close length", 1200, close.Length);
        Assert.AreEqual(1200, close.Length);
        Diagnostics.Assert("CONNECTION_CLOSE error code", 1UL, errorCode);
        Assert.AreEqual(1UL, errorCode);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, client.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, client.Failure!.ExitCode);
        ArgumentNullException nullFailure = Assert.ThrowsExactly<ArgumentNullException>(() => client.Abandon(QuicTransportErrorCode.NoError, null!));
        Diagnostics.Act("null failure exception", FormattableString.Invariant($"{nullFailure.GetType().Name}: {nullFailure.ParamName}"));
        Diagnostics.Assert("null failure exception type", nameof(ArgumentNullException), nullFailure.GetType().Name);
    }

    [TestMethod]
    public void Constructor_StartAndReceive_RefuseMisuse()
    {
        Diagnostics.Arrange("misuse", "Receive and OnLossDetectionTimeout before Start, Start twice, null settings/random/time provider, connection ID length 7 and 21");
        QuicTestRandomSource random = new();
        QuicTestVerifier verifier = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();

        Exception receiveBeforeStart = Assert.ThrowsExactly<InvalidOperationException>(() => client.Receive(new byte[1]));
        Exception timeoutBeforeStart = Assert.ThrowsExactly<InvalidOperationException>(() => client.OnLossDetectionTimeout());
        client.Start();
        Exception startTwice = Assert.ThrowsExactly<InvalidOperationException>(() => client.Start());
        Exception nullSettings = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicClientConnectionState(null!, random, verifier, TimeProvider.System));
        Exception nullRandom = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicClientConnectionState(QuicClientConnectionStateTest.CurlSettings, null!, verifier, TimeProvider.System));
        Exception idTooShort = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicClientConnectionState(QuicClientConnectionStateTest.CurlSettings with { ConnectionIdLength = 7 }, random, verifier, TimeProvider.System));
        Exception idTooLong = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicClientConnectionState(QuicClientConnectionStateTest.CurlSettings with { ConnectionIdLength = 21 }, random, verifier, TimeProvider.System));
        Exception nullTime = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicClientConnectionState(QuicClientConnectionStateTest.CurlSettings, random, verifier, null!));

        Exception[] caught = [receiveBeforeStart, timeoutBeforeStart, startTwice, nullSettings, nullRandom, idTooShort, idTooLong, nullTime];
        string actual = string.Join(", ", caught.Select(exception => exception.GetType().Name));
        Diagnostics.Act("exceptions in order", actual);
        Diagnostics.Assert(
            "exceptions in order",
            "InvalidOperationException, InvalidOperationException, InvalidOperationException, ArgumentNullException, ArgumentNullException, ArgumentOutOfRangeException, ArgumentOutOfRangeException, ArgumentNullException",
            actual);
    }

    [TestMethod]
    public void Start_TokenFromAnEarlierConnection_GoesInTheFirstInitial()
    {
        Diagnostics.Arrange("token from an earlier connection", "0707");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { Token = new byte[] { 7, 7 } });

        byte[] datagram = client.Start().Single();

        Diagnostics.Bytes("client Initial datagram (packet number 0)", datagram);
        string token = HexOf(OpenClientInitial(datagram).Packet.Token);
        Diagnostics.Act("Initial token", token);
        Diagnostics.Assert("Initial token", "0707", token);
        Assert.AreEqual("0707", token);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_FirstInitialLost_ResendsTheClientHelloInANewPacketAndCompletes()
    {
        Diagnostics.Arrange("clock", "manual; first Initial lost, advance 999 ms to the probe timeout");
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(clock: clock);
        byte[] lost = client.Start().Single();
        Diagnostics.Bytes("lost client Initial (packet number 0)", lost);
        Diagnostics.Assert("time until loss detection timeout", TimeSpan.FromMilliseconds(999), client.TimeUntilLossDetectionTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(999), client.TimeUntilLossDetectionTimeout);

        clock.Advance(999);
        Diagnostics.Assert("time until loss detection timeout after 999 ms", TimeSpan.Zero, client.TimeUntilLossDetectionTimeout);
        Assert.AreEqual(TimeSpan.Zero, client.TimeUntilLossDetectionTimeout);
        byte[] probe = client.OnLossDetectionTimeout().Single();
        Diagnostics.Act("probe datagram length", probe.Length);

        // The probe is a new packet, number 1, carrying the lost packet's CRYPTO data (RFC 9002 section 6.2.4).
        (QuicLongHeaderPacket packet, IReadOnlyList<QuicFrame> frames) = OpenClientInitial(probe);
        QuicCryptoFrame resent = (QuicCryptoFrame)frames[0];
        QuicCryptoFrame original = (QuicCryptoFrame)OpenClientInitial(lost).Frames[0];
        Diagnostics.Bytes("probe client Initial (packet number 1)", probe);
        Diagnostics.Assert("probe packet number", 1U, packet.TruncatedPacketNumber);
        Assert.AreEqual(1U, packet.TruncatedPacketNumber);
        Diagnostics.Assert("probe length", 1200, probe.Length);
        Assert.AreEqual(1200, probe.Length);
        Diagnostics.Assert("resent CRYPTO offset and data", FormattableString.Invariant($"{original.Offset}:{HexOf(original.Data)}"), FormattableString.Invariant($"{resent.Offset}:{HexOf(resent.Data)}"));
        Assert.AreEqual((original.Offset, HexOf(original.Data)), (resent.Offset, HexOf(resent.Data)));
        Diagnostics.Assert("probe timeout count", 1, client.Recovery.ProbeTimeoutCount);
        Assert.AreEqual(1, client.Recovery.ProbeTimeoutCount);

        using (Diagnostics.Phase("handshake"))
        {
            Exchange(client, server, [probe]);
        }

        Diagnostics.Assert("handshake confirmed", true, client.IsConfirmed);
        Assert.IsTrue(client.IsConfirmed);
        Diagnostics.Assert("failure", "null", client.Failure?.Message ?? "null");
        Assert.IsNull(client.Failure);

        // The server acknowledged packet 1 at once, so packet 0, sent 999 ms before, is lost by the time threshold: its CRYPTO data went again and CUBIC cut the window to 0.7 x 12000.
        int offsetZeroInitials = server.ClientPackets.Count(sent => sent.Type == QuicPacketType.Initial && sent.Frames.OfType<QuicCryptoFrame>().Any(crypto => crypto.Offset == 0));
        Diagnostics.Assert("Initials carrying CRYPTO offset 0", 2, offsetZeroInitials);
        Assert.AreEqual(2, offsetZeroInitials);
        Diagnostics.Assert("congestion window", 8400, client.Recovery.Congestion.CongestionWindow);
        Assert.AreEqual(8400, client.Recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void Receive_InMemoryServer_SamplesTheRttKeepsTheApplicationLimitedWindowAndDisarmsOnceNothingIsInFlight()
    {
        Diagnostics.Arrange("congestion control", QuicCongestionControlAlgorithm.NewReno);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { CongestionControl = QuicCongestionControlAlgorithm.NewReno });

        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        Diagnostics.Act("controller type", client.Recovery.Congestion.GetType().Name);
        Diagnostics.Assert("controller type", nameof(QuicNewRenoCongestionController), client.Recovery.Congestion.GetType().Name);
        Assert.IsInstanceOfType<QuicNewRenoCongestionController>(client.Recovery.Congestion);
        Diagnostics.Assert("RTT has a sample", true, client.Recovery.Rtt.HasSample);
        Assert.IsTrue(client.Recovery.Rtt.HasSample);

        // No flight of the handshake filled the window, so every packet was application-limited and none grew it (RFC 9002 section 7.8).
        Diagnostics.Assert("congestion window", 12000, client.Recovery.Congestion.CongestionWindow);
        Assert.AreEqual(12000, client.Recovery.Congestion.CongestionWindow);
        Diagnostics.Assert("handshake confirmed in recovery", true, client.Recovery.IsHandshakeConfirmed);
        Assert.IsTrue(client.Recovery.IsHandshakeConfirmed);
        Diagnostics.Assert("max ACK delay", TimeSpan.FromMilliseconds(25), client.Recovery.MaxAckDelay);
        Assert.AreEqual(TimeSpan.FromMilliseconds(25), client.Recovery.MaxAckDelay);
        Diagnostics.Assert("time until loss detection timeout", Timeout.InfiniteTimeSpan, client.TimeUntilLossDetectionTimeout);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilLossDetectionTimeout);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_OneRttPacketLostByTheTimeThreshold_ResendsItsNewConnectionIdInANewPacket()
    {
        Diagnostics.Arrange("clock", "manual; two RETIRE_CONNECTION_ID frames at +100 ms, ACK of the second at +150 ms, timeout at +157 ms");
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(clock: clock);
        using (Diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        clock.Advance(100);

        // Two RETIRE_CONNECTION_ID frames at 100 ms: the client answers each with a NEW_CONNECTION_ID packet.
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicRetireConnectionIdFrame(1)));
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicRetireConnectionIdFrame(2)));
        QuicSentPacket[] answers = [.. client.Recovery.GetUnacknowledgedPackets(QuicPacketNumberSpaceId.ApplicationData).Where(packet => packet.IsAckEliciting)];
        QuicNewConnectionIdFrame lostFrame = answers[0].Frames.OfType<QuicNewConnectionIdFrame>().Single();
        Diagnostics.Act("ack-eliciting packets awaiting acknowledgement", answers.Length);

        // Only the second is acknowledged, 50 ms later: the first waits 9/8 x 50 ms from when it went (RFC 9002 section 6.1.2).
        clock.Advance(50);
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicAckFrame(answers[1].PacketNumber, 0, 0, [], null)));
        Diagnostics.Assert("time until loss detection timeout", TimeSpan.FromMilliseconds(6.25), client.TimeUntilLossDetectionTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(6.25), client.TimeUntilLossDetectionTimeout);
        clock.Advance(7);
        IReadOnlyList<byte[]> resent = client.OnLossDetectionTimeout();

        Diagnostics.Assert("datagrams resent", 1, resent.Count);
        Assert.HasCount(1, resent);
        Diagnostics.Assert("probe timeout count", 0, client.Recovery.ProbeTimeoutCount);
        Assert.AreEqual(0, client.Recovery.ProbeTimeoutCount);
        QuicSentPacket again = client.Recovery.GetUnacknowledgedPackets(QuicPacketNumberSpaceId.ApplicationData).Last();
        Diagnostics.Act("resent packet number (acknowledged packet number)", FormattableString.Invariant($"{again.PacketNumber} ({answers[1].PacketNumber})"));
        Assert.IsGreaterThan(answers[1].PacketNumber, again.PacketNumber);
        QuicNewConnectionIdFrame resentFrame = again.Frames.OfType<QuicNewConnectionIdFrame>().Single();
        Diagnostics.Assert("resent NEW_CONNECTION_ID frame", lostFrame, resentFrame);
        Assert.AreEqual(lostFrame, resentFrame);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_InitialAcknowledgedAndNothingElseInFlight_SendsAPaddedPingProbe()
    {
        Diagnostics.Arrange("clock", "manual; server acknowledges the Initial at +500 ms, probe timeout at +2000 ms");
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(clock: clock);
        server.Receive(client.Start().Single());
        clock.Advance(500);

        // The ACK alone asks for nothing, but the client cannot know the server can send, so a probe must go (RFC 9002 section 6.2.2.1).
        IReadOnlyList<byte[]> replies = client.Receive(server.Protect(QuicPacketType.Initial, new QuicAckFrame(0, 0, 0, [], null)));
        Diagnostics.Assert("datagrams answering the ACK", 0, replies.Count);
        Assert.IsEmpty(replies);
        Diagnostics.Assert("time until loss detection timeout", TimeSpan.FromMilliseconds(1500), client.TimeUntilLossDetectionTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), client.TimeUntilLossDetectionTimeout);
        clock.Advance(1500);
        byte[] probe = client.OnLossDetectionTimeout().Single();

        (QuicLongHeaderPacket packet, IReadOnlyList<QuicFrame> frames) = OpenClientInitial(probe, server.ServerConnectionId);
        Diagnostics.Bytes("probe client Initial (packet number 1)", probe);
        Diagnostics.Act("probe length / packet number / first frame", FormattableString.Invariant($"{probe.Length} / {packet.TruncatedPacketNumber} / {frames[0].GetType().Name}"));
        Diagnostics.Assert("probe length", 1200, probe.Length);
        Assert.AreEqual(1200, probe.Length);
        Diagnostics.Assert("probe packet number", 1U, packet.TruncatedPacketNumber);
        Assert.AreEqual(1U, packet.TruncatedPacketNumber);
        Diagnostics.Assert("first frame type", nameof(QuicPingFrame), frames[0].GetType().Name);
        Assert.IsInstanceOfType<QuicPingFrame>(frames[0]);
    }

    [TestMethod]
    public void TimeUntilSend_BurstSent_PacesAtTheWindowOverTheSmoothedRtt()
    {
        Diagnostics.Arrange("burst", FormattableString.Invariant($"{QuicPacer.BurstDatagrams} datagrams of 1200 bytes, initial window 12000"));
        ManualTimerTimeProvider clock = new();
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(clock: clock);
        for (var datagram = 0; datagram < QuicPacer.BurstDatagrams; datagram++)
        {
            TimeSpan wait = client.TimeUntilSend(1200);
            if (wait != TimeSpan.Zero)
            {
                Diagnostics.Assert("wait before burst datagram " + datagram.ToString(System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero, wait);
            }

            Assert.AreEqual(TimeSpan.Zero, wait);
            client.OnDatagramSent(1200);
        }

        // 1.25 x 12000 bytes per 333 ms: 1200 bytes take 26.64 ms.
        double paced = client.TimeUntilSend(1200).TotalMilliseconds;
        Diagnostics.Act("wait after the burst (ms)", paced.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        Diagnostics.Assert("wait after the burst (ms), within 0.01", 26.64, paced);
        Assert.AreEqual(26.64, paced, 0.01);
        clock.Advance(27);
        TimeSpan afterAdvance = client.TimeUntilSend(1200);
        Diagnostics.Assert("wait after advancing 27 ms", TimeSpan.Zero, afterAdvance);
        Assert.AreEqual(TimeSpan.Zero, afterAdvance);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_AfterFailure_SendsNothingAndTheTimerIsOff()
    {
        Diagnostics.Arrange("abandon", "NoError with failure OperationTimedOut \"timed out\"");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        client.Start();
        client.Abandon(QuicTransportErrorCode.NoError, new QuicHandshakeFailure(CurlExitCode.OperationTimedOut, "timed out"));

        Diagnostics.Assert("time until loss detection timeout", Timeout.InfiniteTimeSpan, client.TimeUntilLossDetectionTimeout);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilLossDetectionTimeout);
        IReadOnlyList<byte[]> datagrams = client.OnLossDetectionTimeout();
        Diagnostics.Act("datagrams sent on timeout", datagrams.Count);
        Diagnostics.Assert("datagrams sent on timeout", 0, datagrams.Count);
        Assert.IsEmpty(datagrams);
    }

    /// <summary>Exchanges datagrams between the client and the server until neither has anything to send; returns what the client sent.</summary>
    internal static List<byte[]> Run(QuicClientConnectionState client, QuicTestServer server) => Exchange(client, server, client.Start());

    /// <summary>Hands <paramref name="first" /> to the server, then exchanges datagrams until neither side has anything to send; returns what the client sent.</summary>
    internal static List<byte[]> Exchange(QuicClientConnectionState client, QuicTestServer server, IEnumerable<byte[]> first)
    {
        List<byte[]> sent = [];
        Queue<byte[]> toServer = new(first);
        while (toServer.TryDequeue(out byte[]? datagram))
        {
            sent.Add(datagram);
            foreach (byte[] reply in server.Receive(datagram).SelectMany(answer => client.Receive(answer)))
            {
                toServer.Enqueue(reply);
            }
        }

        return sent;
    }

    private static QuicClientConnectionState AssertClosesWith(TestDiagnostics diagnostics, QuicTestServer server, QuicTransportErrorCode errorCode, CurlExitCode exitCode)
    {
        diagnostics.Arrange("expected CONNECTION_CLOSE error code / exit code", FormattableString.Invariant($"0x{(ulong)errorCode:x} ({errorCode}) / {exitCode}"));
        QuicClientConnectionState client = QuicClientConnectionStateTest.Client();
        using (diagnostics.Phase("handshake"))
        {
            Run(client, server);
        }

        ulong closeCode = ClientClose(server).ErrorCode;
        diagnostics.Act("failure", FormattableString.Invariant($"{client.Failure?.ExitCode}: {client.Failure?.Message}"));
        diagnostics.Act("CONNECTION_CLOSE error code", closeCode);
        diagnostics.Assert("exit code", exitCode, client.Failure!.ExitCode);
        Assert.AreEqual(exitCode, client.Failure!.ExitCode);
        diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)errorCode, closeCode);
        Assert.AreEqual((ulong)errorCode, closeCode);
        diagnostics.Assert("server received the client Finished", false, server.ClientFinishedReceived);
        Assert.IsFalse(server.ClientFinishedReceived);
        return client;
    }

    private static QuicConnectionCloseFrame ClientClose(QuicTestServer server) =>
        server.ClientPackets.SelectMany(packet => packet.Frames).OfType<QuicConnectionCloseFrame>().First();

    private static (QuicLongHeaderPacket Packet, IReadOnlyList<QuicFrame> Frames) OpenClientInitial(byte[] datagram, byte[]? destinationConnectionId = null)
    {
        using QuicPacketProtection keys = QuicPacketProtection.CreateClientInitial(OriginalDestinationConnectionId);
        QuicUnprotectResult result = keys.Unprotect(datagram, 0, null);
        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        QuicLongHeaderPacket packet = (QuicLongHeaderPacket)result.Packet!;
        if (destinationConnectionId is not null)
        {
            Assert.AreEqual(HexOf(destinationConnectionId), HexOf(packet.DestinationConnectionId));
        }

        return (packet, QuicFrameCodec.Decode(packet.Payload, QuicPacketType.Initial));
    }

    private static byte[] ServerInitial(byte[] destination, byte[] source, uint packetNumber, params QuicFrame[] frames) =>
        ServerLongHeader(QuicPacketType.Initial, destination, source, packetNumber, frames);

    private static byte[] ServerLongHeader(QuicPacketType type, byte[] destination, byte[] source, uint packetNumber, params QuicFrame[] frames)
    {
        using QuicPacketProtection keys = QuicPacketProtection.CreateServerInitial(OriginalDestinationConnectionId);
        byte[] payload = [.. QuicFrameCodec.Encode(frames), .. new byte[20]];
        return keys.Protect(new QuicLongHeaderPacket(type, QuicPacketCodec.Version1, destination, source, ReadOnlyMemory<byte>.Empty, 1, packetNumber, payload), packetNumber);
    }

    private const string FirstInitialSha256 = "530afe73663df1785920663f9982fc1a0a655bdb7e5574ae2ac6eeb05ec0b8f9";
}
