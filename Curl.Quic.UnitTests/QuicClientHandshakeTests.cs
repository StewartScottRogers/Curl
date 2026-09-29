using System.Security.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

[TestClass]
public sealed class QuicClientHandshakeTests
{
    // QuicTestRandomSource hands out 00, 01, 02, ...: the destination connection ID first, then the source.
    private static readonly byte[] OriginalDestinationConnectionId = [.. Enumerable.Range(0, 20).Select(value => (byte)value)];

    private static readonly byte[] SourceConnectionId = [.. Enumerable.Range(20, 20).Select(value => (byte)value)];

    [TestMethod]
    public void Start_FixedRandomness_SendsThePinned1200ByteInitial()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        byte[] datagram = client.Start().Single();

        Assert.AreEqual(1200, datagram.Length);
        Assert.AreEqual(FirstInitialSha256, HexOf(SHA256.HashData(datagram)));
    }

    [TestMethod]
    public void Start_CurlSettings_SendsCurlsClientHelloInOneCryptoFrameThenPadding()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        byte[] datagram = client.Start().Single();

        (QuicLongHeaderPacket packet, IReadOnlyList<QuicFrame> frames) = OpenClientInitial(datagram);
        QuicCryptoFrame crypto = (QuicCryptoFrame)frames[0];
        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(crypto.Data.Span).Message!.Body).Value;

        Assert.AreEqual(HexOf(OriginalDestinationConnectionId), HexOf(packet.DestinationConnectionId));
        Assert.AreEqual(HexOf(SourceConnectionId), HexOf(packet.SourceConnectionId));
        Assert.IsTrue(packet.Token.IsEmpty);
        Assert.AreEqual((1, 0u), (packet.PacketNumberLength, packet.TruncatedPacketNumber));
        Assert.AreEqual(0UL, crypto.Offset);
        Assert.IsInstanceOfType<QuicPaddingFrame>(frames[1]);
        Assert.HasCount(2, frames);
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1303, 0x1301 }, hello.CipherSuites.ToArray());
        Assert.IsEmpty(hello.LegacySessionId);
        CollectionAssert.AreEqual(
            new[] { TlsExtensionType.QuicTransportParameters, TlsExtensionType.ServerName, TlsExtensionType.EcPointFormats, TlsExtensionType.SupportedGroups, TlsExtensionType.KeyShare, TlsExtensionType.ApplicationLayerProtocolNegotiation, TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms },
            hello.Extensions.Select(extension => extension.Type).ToArray());
        Assert.AreEqual(HexOf((QuicTransportParameters.CurlClientDefaults with { InitialSourceConnectionId = SourceConnectionId }).Encode()), HexOf(hello.Extensions[0].Data));
        Assert.AreEqual(HexOf(hello.Extensions[0].Data), HexOf(client.TransportParameters.Encode()));
        CollectionAssert.AreEqual(new[] { "h3", "h3-29" }, ApplicationLayerProtocolNegotiationExtension.Decode(hello.Extensions[5].Data).Value.ToArray());
    }

    [TestMethod]
    public void Receive_InMemoryServer_CompletesConfirmsAndDiscardsEarlierKeys()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        Run(client, server);

        Assert.IsTrue(client.IsComplete);
        Assert.IsTrue(client.IsConfirmed);
        Assert.IsTrue(client.InitialKeysDiscarded);
        Assert.IsTrue(client.HandshakeKeysDiscarded);
        Assert.IsNull(client.Failure);
        Assert.AreEqual("h3", client.Tls.ApplicationProtocol);
        Assert.AreEqual(30000UL, client.ServerTransportParameters!.MaxIdleTimeout);
        Assert.AreEqual(HexOf(server.ServerConnectionId), HexOf(client.PeerConnectionIds!.Current.ConnectionId));
        Assert.AreEqual(HexOf(server.StatelessResetToken), HexOf(client.PeerConnectionIds.Current.StatelessResetToken!));
        Assert.IsTrue(server.ClientFinishedReceived);
    }

    [TestMethod]
    public void Receive_HandshakeComplete_IssuesConnectionIdsUpToTheServersLimit()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        Run(client, server);
        List<QuicNewConnectionIdFrame> issued = [.. server.ClientPackets.SelectMany(packet => packet.Frames).OfType<QuicNewConnectionIdFrame>()];

        CollectionAssert.AreEqual(new ulong[] { 1, 2, 3 }, issued.Select(frame => frame.SequenceNumber).ToArray());
        Assert.HasCount(4, client.LocalConnectionIds.Active);
    }

    [TestMethod]
    public void Receive_FramesAfterTheHandshake_KeepsTokensRetiresIdsAndTakesTickets()
    {
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
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        Run(client, server);
        List<QuicFrame> sent = [.. server.ClientPackets.Where(packet => packet.Type == QuicPacketType.OneRtt).SelectMany(packet => packet.Frames)];

        Assert.AreEqual("0707", HexOf(client.ReceivedTokens.Single()));
        Assert.AreEqual(1UL, client.PeerConnectionIds!.Current.SequenceNumber);
        Assert.AreEqual(0UL, sent.OfType<QuicRetireConnectionIdFrame>().Single().SequenceNumber);
        Assert.AreEqual(4UL, sent.OfType<QuicNewConnectionIdFrame>().Last().SequenceNumber);
        Assert.HasCount(1, client.Tls.ReceivedTickets);
        Assert.IsTrue(sent.OfType<QuicAckFrame>().Any());
    }

    [TestMethod]
    public void Receive_Retry_ResendsTheClientHelloWithItsTokenAndConnectionId()
    {
        using QuicTestServer server = new() { SendRetry = true };
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        List<byte[]> sent = Run(client, server);
        QuicLongHeaderPacket second = (QuicLongHeaderPacket)QuicPacketCodec.Decode(sent[1], QuicClientSettings.CurlConnectionIdLength).Packet;

        Assert.IsTrue(client.IsComplete);
        Assert.AreEqual(1200, sent[1].Length);
        Assert.AreEqual(HexOf(server.RetryToken), HexOf(second.Token));
        Assert.AreEqual(HexOf(server.RetrySourceConnectionId!), HexOf(second.DestinationConnectionId));
        Assert.AreEqual(HexOf(server.RetrySourceConnectionId!), HexOf(client.ServerTransportParameters!.RetrySourceConnectionId!));
    }

    [TestMethod]
    public void Receive_SecondInvalidOrForeignRetry_IsIgnored()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();
        QuicRetryPacket unsigned = new(QuicPacketCodec.Version1, SourceConnectionId, new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }, new byte[] { 1 }, new byte[16]);
        QuicRetryPacket valid = unsigned with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, unsigned) };
        QuicRetryPacket otherConnection = valid with { DestinationConnectionId = new byte[20] };
        QuicRetryPacket sameConnectionId = valid with { SourceConnectionId = OriginalDestinationConnectionId };

        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(unsigned)));
        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(sameConnectionId with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, sameConnectionId) })));
        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(otherConnection with { RetryIntegrityTag = QuicRetryIntegrity.ComputeTag(OriginalDestinationConnectionId, otherConnection) })));
        Assert.HasCount(1, client.Receive(QuicPacketCodec.Encode(valid)));
        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(valid)));
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void Receive_VersionNegotiationWithoutVersion1_FailsWithExit7()
    {
        using QuicTestServer server = new() { VersionNegotiation = [0xff00001d, 0x6b3343cf] };
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        List<byte[]> sent = Run(client, server);

        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        StringAssert.Contains(client.Failure.Message, "0xff00001d, 0x6b3343cf");
        Assert.HasCount(1, sent);
    }

    [TestMethod]
    public void Receive_VersionNegotiationListingVersion1ForeignOrLate_IsIgnored()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();

        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(SourceConnectionId, new byte[] { 1 }, [2, QuicPacketCodec.Version1]))));
        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(new byte[] { 5 }, new byte[] { 1 }, [2]))));
        client.Receive(ServerInitial(SourceConnectionId, [8, 8, 8, 8], 0, new QuicPingFrame()));
        Assert.IsEmpty(client.Receive(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(SourceConnectionId, new byte[] { 1 }, [2]))));
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    [DataRow("original_destination_connection_id", DisplayName = "original_destination_connection_id missing")]
    [DataRow("initial_source_connection_id", DisplayName = "initial_source_connection_id different")]
    [DataRow("retry_source_connection_id", DisplayName = "retry_source_connection_id without a Retry")]
    public void Receive_MismatchedServerTransportParameter_ClosesWithTransportParameterError(string parameter)
    {
        Func<QuicTransportParameters, QuicTransportParameters> change = parameter switch
        {
            "original_destination_connection_id" => parameters => parameters with { OriginalDestinationConnectionId = null },
            "initial_source_connection_id" => parameters => parameters with { InitialSourceConnectionId = [1] },
            _ => parameters => parameters with { RetrySourceConnectionId = [1] },
        };
        using QuicTestServer server = new() { EncodeTransportParameters = parameters => change(parameters).Encode() };

        QuicClientHandshake client = AssertClosesWith(server, QuicTransportErrorCode.TransportParameterError, CurlExitCode.CouldntConnect);

        StringAssert.Contains(client.Failure!.Message, parameter);
        client.Dispose();
    }

    [TestMethod]
    public void Receive_MalformedServerTransportParameters_ClosesWithTransportParameterError()
    {
        using QuicTestServer server = new() { EncodeTransportParameters = _ => [0x01, 0x05] };

        AssertClosesWith(server, QuicTransportErrorCode.TransportParameterError, CurlExitCode.CouldntConnect).Dispose();
    }

    [TestMethod]
    public void Receive_ServerChoosesAnotherVersion_ClosesWithVersionNegotiationError()
    {
        using QuicTestServer server = new() { EncodeTransportParameters = parameters => (parameters with { VersionInformation = new QuicVersionInformation(2, [2]) }).Encode() };

        AssertClosesWith(server, QuicTransportErrorCode.VersionNegotiationError, CurlExitCode.CouldntConnect).Dispose();
    }

    [TestMethod]
    public void Receive_ServerChoosesVersion1_Completes()
    {
        using QuicTestServer server = new() { EncodeTransportParameters = parameters => (parameters with { VersionInformation = QuicVersionInformation.Version1Only }).Encode() };
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        Run(client, server);

        Assert.IsTrue(client.IsComplete);
    }

    [TestMethod]
    public void Receive_AcknowledgementOfAPacketNeverSent_ClosesWithProtocolViolation()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();

        byte[] close = client.Receive(ServerInitial(SourceConnectionId, [8], 0, new QuicAckFrame(5, 0, 0, [], null))).Single();

        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, ((QuicConnectionCloseFrame)OpenClientInitial(close).Frames[0]).ErrorCode);
    }

    [TestMethod]
    public void Receive_NoServerTransportParameters_ClosesWithMissingExtension()
    {
        using QuicTestServer server = new() { EncodeTransportParameters = _ => null };

        AssertClosesWith(server, QuicTransportErrorCode.CryptoErrorBase + 109, CurlExitCode.SslConnectError).Dispose();
    }

    [TestMethod]
    public void Receive_NoApplicationProtocol_ClosesWithNoApplicationProtocol()
    {
        using QuicTestServer server = new() { ApplicationProtocol = null };

        AssertClosesWith(server, QuicTransportErrorCode.CryptoErrorBase + 120, CurlExitCode.SslConnectError).Dispose();
    }

    [TestMethod]
    public void Receive_TlsAlertFromTheClient_ClosesWithItsCryptoErrorAndExit35()
    {
        using QuicTestServer server = new() { ApplicationProtocol = "h2" };

        QuicClientHandshake client = AssertClosesWith(server, QuicTransportErrorCode.CryptoErrorBase + (int)TlsAlertDescription.IllegalParameter, CurlExitCode.SslConnectError);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, client.Failure!.TlsFailure!.Alert);
        StringAssert.Contains(client.Failure.Message, "IllegalParameter");
        client.Dispose();
    }

    [TestMethod]
    public void Receive_RejectedCertificate_ClosesWithBadCertificateAndExit60()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(verifier: new QuicTestVerifier { Rejection = "SSL certificate problem: self-signed certificate" });

        Run(client, server);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, client.Failure!.ExitCode);
        Assert.AreEqual("SSL certificate problem: self-signed certificate", client.Failure.Message);
        Assert.AreEqual((ulong)QuicTransportErrorCode.CryptoErrorBase + 42, ClientClose(server).ErrorCode);
    }

    [TestMethod]
    [DataRow(0x128UL, CurlExitCode.CouldntConnect, "TLS alert HandshakeFailure (refused)", DisplayName = "A TLS alert from the server is exit 7")]
    [DataRow(0x02UL, CurlExitCode.WeirdServerReply, "transport error ConnectionRefused (refused)", DisplayName = "CONNECTION_REFUSED is exit 8")]
    [DataRow(0x0aUL, CurlExitCode.CouldntConnect, "transport error ProtocolViolation (refused)", DisplayName = "Another transport error is exit 7")]
    public void Receive_ServerCloseDuringTheHandshake_FailsWithTheMappedExit(ulong errorCode, CurlExitCode exitCode, string description)
    {
        using QuicTestServer server = new() { CloseAfterClientHello = errorCode };
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        List<byte[]> sent = Run(client, server);

        Assert.AreEqual(exitCode, client.Failure!.ExitCode);
        Assert.AreEqual($"The server closed the QUIC connection with {description}.", client.Failure.Message);
        Assert.HasCount(1, sent);
        Assert.IsEmpty(client.Receive(new byte[1200]));
    }

    [TestMethod]
    public void Receive_FramesAfterAClose_AreNotRead()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();
        QuicConnectionCloseFrame close = new(0x0a, 0, ReadOnlyMemory<byte>.Empty);

        byte[] datagram = [.. ServerInitial(SourceConnectionId, [8], 0, close, new QuicPingFrame()), .. ServerInitial(SourceConnectionId, [8], 1, new QuicPingFrame())];

        Assert.IsEmpty(client.Receive(datagram));
        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
    }

    [TestMethod]
    public void Receive_ForbiddenFrame_ClosesWithProtocolViolation()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();

        byte[] close = client.Receive(ServerInitial(SourceConnectionId, [8], 0, new QuicHandshakeDoneFrame())).Single();

        Assert.AreEqual(CurlExitCode.CouldntConnect, client.Failure!.ExitCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, ((QuicConnectionCloseFrame)OpenClientInitial(close, [8]).Frames[0]).ErrorCode);
    }

    [TestMethod]
    public void Receive_UnreadableForeignDuplicateOrUnkeyedPackets_AreDropped()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();
        byte[] ping = ServerInitial(SourceConnectionId, [8], 0, new QuicPingFrame());
        byte[] tampered = [.. ping];
        tampered[^1] ^= 1;

        Assert.IsEmpty(client.Receive(new byte[] { 0x40 }));
        Assert.IsEmpty(client.Receive(tampered));
        Assert.IsEmpty(client.Receive(ServerInitial(new byte[20], [8], 0, new QuicPingFrame())));
        Assert.IsEmpty(client.Receive(ServerLongHeader(QuicPacketType.ZeroRtt, SourceConnectionId, [8], 0, new QuicPingFrame())));
        Assert.IsEmpty(client.Receive((byte[])[0x41, .. SourceConnectionId, .. new byte[30]]));
        Assert.HasCount(1, client.Receive(ping));
        Assert.IsEmpty(client.Receive(ping));
        Assert.IsEmpty(client.Receive(ServerInitial(SourceConnectionId, [9], 1, new QuicPingFrame())));
        Assert.HasCount(1, client.Receive(ServerInitial(SourceConnectionId, [8], 2, new QuicCryptoFrame(5, new byte[] { 1 }), new QuicAckFrame(0, 0, 0, [], null))));
        Assert.AreEqual("08", HexOf(client.PeerConnectionIds!.Current.ConnectionId));
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void Receive_AfterTheHandshakeDiscardedKeysOrForeignIds_AreDropped()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        Run(client, server);

        Assert.IsEmpty(client.Receive(server.Protect(QuicPacketType.Initial, new QuicPingFrame())));
        Assert.IsEmpty(client.Receive(server.Protect(QuicPacketType.Handshake, new QuicPingFrame())));
        Assert.IsEmpty(client.Receive((byte[])[0x41, .. new byte[20], .. new byte[30]]));
        Assert.HasCount(1, client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicPingFrame())));
    }

    [TestMethod]
    public void Start_ClientHelloLargerThanOnePacket_SendsTwoFullDatagrams()
    {
        QuicClientSettings settings = QuicHandshakeTest.CurlSettings with
        {
            Tls = QuicHandshakeTest.CurlSettings.Tls with { FixedExtensions = [.. QuicHandshakeTest.CurlSettings.Tls.FixedExtensions, new TlsExtension((TlsExtensionType)0x7a7a, new byte[1100])] },
        };
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(settings);

        List<byte[]> sent = Run(client, server);

        Assert.IsTrue(sent.Take(2).All(datagram => datagram.Length == 1200));
        Assert.IsTrue(((QuicCryptoFrame)OpenClientInitial(sent[1]).Frames[0]).Offset > 1100);
        Assert.IsTrue(client.IsComplete);
    }

    [TestMethod]
    public void Abandon_AfterStart_ClosesAtEveryLevelWithKeys()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();

        byte[] close = client.Abandon(QuicTransportErrorCode.InternalError, new QuicHandshakeFailure(CurlExitCode.SendError, "timeout")).Single();

        Assert.AreEqual(1200, close.Length);
        Assert.AreEqual(1UL, ((QuicConnectionCloseFrame)OpenClientInitial(close).Frames[0]).ErrorCode);
        Assert.AreEqual(CurlExitCode.SendError, client.Failure!.ExitCode);
        Assert.ThrowsExactly<ArgumentNullException>(() => client.Abandon(QuicTransportErrorCode.NoError, null!));
    }

    [TestMethod]
    public void Constructor_StartAndReceive_RefuseMisuse()
    {
        QuicTestRandomSource random = new();
        QuicTestVerifier verifier = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.Receive(new byte[1]));
        Assert.ThrowsExactly<InvalidOperationException>(() => client.OnLossDetectionTimeout());
        client.Start();
        Assert.ThrowsExactly<InvalidOperationException>(() => client.Start());
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicClientHandshake(null!, random, verifier, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicClientHandshake(QuicHandshakeTest.CurlSettings, null!, verifier, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicClientHandshake(QuicHandshakeTest.CurlSettings with { ConnectionIdLength = 7 }, random, verifier, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicClientHandshake(QuicHandshakeTest.CurlSettings with { ConnectionIdLength = 21 }, random, verifier, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicClientHandshake(QuicHandshakeTest.CurlSettings, random, verifier, null!));
    }

    [TestMethod]
    public void Start_TokenFromAnEarlierConnection_GoesInTheFirstInitial()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client(QuicHandshakeTest.CurlSettings with { Token = new byte[] { 7, 7 } });

        Assert.AreEqual("0707", HexOf(OpenClientInitial(client.Start().Single()).Packet.Token));
    }

    [TestMethod]
    public void OnLossDetectionTimeout_FirstInitialLost_ResendsTheClientHelloInANewPacketAndCompletes()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(clock: clock);
        byte[] lost = client.Start().Single();
        Assert.AreEqual(TimeSpan.FromMilliseconds(999), client.TimeUntilLossDetectionTimeout);

        clock.Advance(999);
        Assert.AreEqual(TimeSpan.Zero, client.TimeUntilLossDetectionTimeout);
        byte[] probe = client.OnLossDetectionTimeout().Single();

        // The probe is a new packet, number 1, carrying the lost packet's CRYPTO data (RFC 9002 section 6.2.4).
        (QuicLongHeaderPacket packet, IReadOnlyList<QuicFrame> frames) = OpenClientInitial(probe);
        QuicCryptoFrame resent = (QuicCryptoFrame)frames[0];
        QuicCryptoFrame original = (QuicCryptoFrame)OpenClientInitial(lost).Frames[0];
        Assert.AreEqual(1U, packet.TruncatedPacketNumber);
        Assert.AreEqual(1200, probe.Length);
        Assert.AreEqual((original.Offset, HexOf(original.Data)), (resent.Offset, HexOf(resent.Data)));
        Assert.AreEqual(1, client.Recovery.ProbeTimeoutCount);

        Exchange(client, server, [probe]);

        Assert.IsTrue(client.IsConfirmed);
        Assert.IsNull(client.Failure);

        // The server acknowledged packet 1 at once, so packet 0, sent 999 ms before, is lost by the time threshold: its CRYPTO data went again and CUBIC cut the window to 0.7 x 12000.
        Assert.AreEqual(2, server.ClientPackets.Count(sent => sent.Type == QuicPacketType.Initial && sent.Frames.OfType<QuicCryptoFrame>().Any(crypto => crypto.Offset == 0)));
        Assert.AreEqual(8400, client.Recovery.Congestion.CongestionWindow);
    }

    [TestMethod]
    public void Receive_InMemoryServer_SamplesTheRttGrowsTheWindowAndDisarmsOnceNothingIsInFlight()
    {
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(QuicHandshakeTest.CurlSettings with { CongestionControl = QuicCongestionControlAlgorithm.NewReno });

        Run(client, server);

        Assert.IsInstanceOfType<QuicNewRenoCongestionController>(client.Recovery.Congestion);
        Assert.IsTrue(client.Recovery.Rtt.HasSample);
        Assert.IsGreaterThan(12000, client.Recovery.Congestion.CongestionWindow);
        Assert.IsTrue(client.Recovery.IsHandshakeConfirmed);
        Assert.AreEqual(TimeSpan.FromMilliseconds(25), client.Recovery.MaxAckDelay);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilLossDetectionTimeout);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_OneRttPacketLostByTheTimeThreshold_ResendsItsNewConnectionIdInANewPacket()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(clock: clock);
        Run(client, server);
        clock.Advance(100);

        // Two RETIRE_CONNECTION_ID frames at 100 ms: the client answers each with a NEW_CONNECTION_ID packet.
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicRetireConnectionIdFrame(1)));
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicRetireConnectionIdFrame(2)));
        QuicSentPacket[] answers = [.. client.Recovery.GetUnacknowledgedPackets(QuicPacketNumberSpaceId.ApplicationData).Where(packet => packet.IsAckEliciting)];
        QuicNewConnectionIdFrame lostFrame = answers[0].Frames.OfType<QuicNewConnectionIdFrame>().Single();

        // Only the second is acknowledged, 50 ms later: the first waits 9/8 x 50 ms from when it went (RFC 9002 section 6.1.2).
        clock.Advance(50);
        client.Receive(server.Protect(QuicPacketType.OneRtt, new QuicAckFrame(answers[1].PacketNumber, 0, 0, [], null)));
        Assert.AreEqual(TimeSpan.FromMilliseconds(6.25), client.TimeUntilLossDetectionTimeout);
        clock.Advance(7);
        IReadOnlyList<byte[]> resent = client.OnLossDetectionTimeout();

        Assert.HasCount(1, resent);
        Assert.AreEqual(0, client.Recovery.ProbeTimeoutCount);
        QuicSentPacket again = client.Recovery.GetUnacknowledgedPackets(QuicPacketNumberSpaceId.ApplicationData).Last();
        Assert.IsGreaterThan(answers[1].PacketNumber, again.PacketNumber);
        Assert.AreEqual(lostFrame, again.Frames.OfType<QuicNewConnectionIdFrame>().Single());
    }

    [TestMethod]
    public void OnLossDetectionTimeout_InitialAcknowledgedAndNothingElseInFlight_SendsAPaddedPingProbe()
    {
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(clock: clock);
        server.Receive(client.Start().Single());
        clock.Advance(500);

        // The ACK alone asks for nothing, but the client cannot know the server can send, so a probe must go (RFC 9002 section 6.2.2.1).
        Assert.IsEmpty(client.Receive(server.Protect(QuicPacketType.Initial, new QuicAckFrame(0, 0, 0, [], null))));
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), client.TimeUntilLossDetectionTimeout);
        clock.Advance(1500);
        byte[] probe = client.OnLossDetectionTimeout().Single();

        (QuicLongHeaderPacket packet, IReadOnlyList<QuicFrame> frames) = OpenClientInitial(probe, server.ServerConnectionId);
        Assert.AreEqual(1200, probe.Length);
        Assert.AreEqual(1U, packet.TruncatedPacketNumber);
        Assert.IsInstanceOfType<QuicPingFrame>(frames[0]);
    }

    [TestMethod]
    public void TimeUntilSend_BurstSent_PacesAtTheWindowOverTheSmoothedRtt()
    {
        ManualTimerTimeProvider clock = new();
        using QuicClientHandshake client = QuicHandshakeTest.Client(clock: clock);
        for (var datagram = 0; datagram < QuicPacer.BurstDatagrams; datagram++)
        {
            Assert.AreEqual(TimeSpan.Zero, client.TimeUntilSend(1200));
            client.OnDatagramSent(1200);
        }

        // 1.25 x 12000 bytes per 333 ms: 1200 bytes take 26.64 ms.
        Assert.AreEqual(26.64, client.TimeUntilSend(1200).TotalMilliseconds, 0.01);
        clock.Advance(27);
        Assert.AreEqual(TimeSpan.Zero, client.TimeUntilSend(1200));
    }

    [TestMethod]
    public void OnLossDetectionTimeout_AfterFailure_SendsNothingAndTheTimerIsOff()
    {
        using QuicClientHandshake client = QuicHandshakeTest.Client();
        client.Start();
        client.Abandon(QuicTransportErrorCode.NoError, new QuicHandshakeFailure(CurlExitCode.OperationTimedOut, "timed out"));

        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.TimeUntilLossDetectionTimeout);
        Assert.IsEmpty(client.OnLossDetectionTimeout());
    }

    /// <summary>Exchanges datagrams between the client and the server until neither has anything to send; returns what the client sent.</summary>
    internal static List<byte[]> Run(QuicClientHandshake client, QuicTestServer server) => Exchange(client, server, client.Start());

    /// <summary>Hands <paramref name="first" /> to the server, then exchanges datagrams until neither side has anything to send; returns what the client sent.</summary>
    internal static List<byte[]> Exchange(QuicClientHandshake client, QuicTestServer server, IEnumerable<byte[]> first)
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

    private static QuicClientHandshake AssertClosesWith(QuicTestServer server, QuicTransportErrorCode errorCode, CurlExitCode exitCode)
    {
        QuicClientHandshake client = QuicHandshakeTest.Client();
        Run(client, server);

        Assert.AreEqual(exitCode, client.Failure!.ExitCode);
        Assert.AreEqual((ulong)errorCode, ClientClose(server).ErrorCode);
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

    private const string FirstInitialSha256 = "1a71b2d54d9d99a9c515ed375e55360931eb534b723bcf45e30079bdc1e67d51";
}
