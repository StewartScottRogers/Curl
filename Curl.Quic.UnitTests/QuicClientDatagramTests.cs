using System.Text;
using Curl.Testing;
using static Curl.Quic.QuicStreamTest;

namespace Curl.Quic;

/// <summary>DATAGRAM frames (RFC 9221) carried by a client whose handshake with the in-memory server is complete.</summary>
[TestClass]
public sealed class QuicClientDatagramTests
{
    private const ulong Advertised = 65535;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SendDatagram_ServerDidNotAdvertiseMaxDatagramFrameSize_IsRefused()
    {
        Diagnostics.Arrange("server max_datagram_frame_size", "absent");
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, Advertised);

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() => client.SendDatagram("hi"u8.ToArray()));

        Diagnostics.Act("max datagram payload length", client.MaxDatagramPayloadLength);
        Diagnostics.Act("error", error.Message);
        Diagnostics.Assert("max datagram payload length", 0, client.MaxDatagramPayloadLength);
        Assert.AreEqual(0, client.MaxDatagramPayloadLength);
        Assert.Contains("max_datagram_frame_size", error.Message);
    }

    [TestMethod]
    public void MaxDatagramPayloadLength_BeforeTheServersTransportParameters_IsZero()
    {
        Diagnostics.Arrange("handshake", "not started");
        using QuicClientConnectionState client = QuicClientConnectionStateTest.Client(QuicClientConnectionStateTest.CurlSettings with { TransportParameters = SmallClientLimits with { MaxDatagramFrameSize = Advertised } });

        int length = client.MaxDatagramPayloadLength;

        Diagnostics.Act("max datagram payload length", length);
        Diagnostics.Assert("max datagram payload length", 0, length);
        Assert.AreEqual(0, length);
    }

    [TestMethod]
    public void SendDatagram_ServerAdvertisesOnlyTheFrameOverhead_IsRefused()
    {
        Diagnostics.Arrange("server max_datagram_frame_size", QuicDatagramFrame.Overhead);
        using QuicTestServer server = AdvertisingServer(QuicDatagramFrame.Overhead);
        using QuicClientConnectionState client = ConnectClient(server, Advertised);

        Diagnostics.Act("max datagram payload length", client.MaxDatagramPayloadLength);
        Diagnostics.Assert("max datagram payload length", 0, client.MaxDatagramPayloadLength);
        Assert.AreEqual(0, client.MaxDatagramPayloadLength);
        Assert.ThrowsExactly<InvalidOperationException>(() => client.SendDatagram(ReadOnlyMemory<byte>.Empty));
    }

    [TestMethod]
    public void SendDatagram_BothAdvertise_SendsAnAckElicitingDatagramFrame()
    {
        Diagnostics.Arrange("max_datagram_frame_size (client, server)", (Advertised, Advertised));
        using QuicTestServer server = AdvertisingServer(Advertised);
        using QuicClientConnectionState client = ConnectClient(server, Advertised);

        client.SendDatagram("hello"u8.ToArray());
        Flush(client, server);

        QuicDatagramFrame sent = Sent<QuicDatagramFrame>(server).Single();
        string text = Encoding.ASCII.GetString(sent.Data.Span);
        Diagnostics.Act("max datagram payload length", client.MaxDatagramPayloadLength);
        Diagnostics.Act("datagram sent", text);
        Diagnostics.Assert("max datagram payload length", QuicClientConnectionState.MaximumDatagramPayloadLength, client.MaxDatagramPayloadLength);
        Assert.AreEqual(QuicClientConnectionState.MaximumDatagramPayloadLength, client.MaxDatagramPayloadLength);
        Diagnostics.Assert("datagram sent", "hello", text);
        Assert.AreEqual("hello", text);
        Diagnostics.Assert("ack-eliciting", true, sent.IsAckEliciting);
        Assert.IsTrue(sent.IsAckEliciting);
    }

    [TestMethod]
    public void SendDatagram_LongerThanTheServerAllows_IsRefused()
    {
        Diagnostics.Arrange("server max_datagram_frame_size", 100);
        using QuicTestServer server = AdvertisingServer(100);
        using QuicClientConnectionState client = ConnectClient(server, Advertised);

        client.SendDatagram(Bytes(97));
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => client.SendDatagram(Bytes(98)));

        Diagnostics.Act("error", error.Message);
        Diagnostics.Assert("max datagram payload length", 97, client.MaxDatagramPayloadLength);
        Assert.AreEqual(97, client.MaxDatagramPayloadLength);
        Assert.AreEqual("data", error.ParamName);
    }

    [TestMethod]
    [DataRow(0UL, 1, DisplayName = "client did not advertise")]
    [DataRow(10UL, 9, DisplayName = "frame larger than the client advertised")]
    public void Receive_DatagramTheClientDidNotAllow_ClosesWithProtocolViolation(ulong clientAdvertised, int length)
    {
        Diagnostics.Arrange("client max_datagram_frame_size, datagram length", (clientAdvertised, length));
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, clientAdvertised);

        Deliver(client, server, new QuicDatagramFrame(Bytes(length)));

        ulong errorCode = Sent<QuicConnectionCloseFrame>(server).Single().ErrorCode;
        Diagnostics.Act("CONNECTION_CLOSE error code", errorCode);
        Diagnostics.Assert("CONNECTION_CLOSE error code", (ulong)QuicTransportErrorCode.ProtocolViolation, errorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, errorCode);
        Assert.IsNull(client.TakeReceivedDatagram());
    }

    [TestMethod]
    public void Receive_DatagramsTheClientAllowed_AreTakenInOrder()
    {
        Diagnostics.Arrange("client max_datagram_frame_size", 11);
        using QuicTestServer server = new();
        using QuicClientConnectionState client = ConnectClient(server, 11);

        Deliver(client, server, new QuicDatagramFrame(Bytes(9)), new QuicDatagramFrame("b"u8.ToArray(), HasLength: false));

        ReadOnlyMemory<byte>? first = client.TakeReceivedDatagram();
        ReadOnlyMemory<byte>? second = client.TakeReceivedDatagram();
        Diagnostics.Act("datagram lengths", (first?.Length, second?.Length));
        Diagnostics.Assert("datagram lengths", ((int?)9, (int?)1), (first?.Length, second?.Length));
        Assert.AreEqual(9, first!.Value.Length);
        Assert.AreEqual((byte)'b', second!.Value.Span[0]);
        Assert.IsNull(client.TakeReceivedDatagram());
        Assert.IsNull(client.Failure);
    }

    [TestMethod]
    public void OnLossDetectionTimeout_LostDatagram_IsNeverSentAgain()
    {
        Diagnostics.Arrange("datagram", "5 bytes, its packet never delivered");
        ManualTimerTimeProvider clock = new();
        using QuicTestServer server = AdvertisingServer(Advertised);
        using QuicClientConnectionState client = Connect(server, SmallClientLimits with { MaxDatagramFrameSize = Advertised }, clock);
        client.SendDatagram(Bytes(5));
        IReadOnlyList<byte[]> firstFlight = client.TakeDatagramsToSend();
        Assert.IsNotEmpty(firstFlight);

        using (Diagnostics.Phase("loss detection"))
        {
            clock.Advance((long)client.TimeUntilLossDetectionTimeout.TotalMilliseconds);
            QuicClientConnectionStateTests.Exchange(client, server, client.OnLossDetectionTimeout());
            clock.Advance((long)client.TimeUntilLossDetectionTimeout.TotalMilliseconds);
            QuicClientConnectionStateTests.Exchange(client, server, client.OnLossDetectionTimeout());
        }

        int oneRttPackets = server.ClientPackets.Count(packet => packet.Type == QuicPacketType.OneRtt);
        int datagramFrames = Sent<QuicDatagramFrame>(server).Count;
        Diagnostics.Act("1-RTT packets the server received", oneRttPackets);
        Diagnostics.Act("DATAGRAM frames the server received", datagramFrames);
        Assert.IsGreaterThan(0, oneRttPackets);
        Diagnostics.Assert("DATAGRAM frames the server received", 0, datagramFrames);
        Assert.AreEqual(0, datagramFrames);
    }

    [TestMethod]
    public void TakeDatagramsToSend_MoreDatagramsThanTheCongestionWindow_SendsEachInItsOwnPacketAndHoldsTheRest()
    {
        Diagnostics.Arrange("datagrams queued", "30 of 1100 bytes");
        using QuicTestServer server = AdvertisingServer(Advertised);
        using QuicClientConnectionState client = ConnectClient(server, Advertised);
        for (var index = 0; index < 30; index++)
        {
            client.SendDatagram(Bytes(QuicClientConnectionState.MaximumDatagramPayloadLength));
        }

        IReadOnlyList<byte[]> sent = client.TakeDatagramsToSend();

        Diagnostics.Act("datagrams sent", sent.Count);
        Diagnostics.Act("largest datagram", sent.Max(datagram => datagram.Length));
        Assert.IsGreaterThan(0, sent.Count);
        Assert.IsLessThan(30, sent.Count, "the congestion window holds some back");
        Assert.IsLessThanOrEqualTo(QuicDatagramAssembler.DatagramSize, sent.Max(datagram => datagram.Length));
    }

    private static QuicTestServer AdvertisingServer(ulong maxDatagramFrameSize) =>
        new() { ConfigureTransportParameters = parameters => parameters with { MaxDatagramFrameSize = maxDatagramFrameSize } };

    private QuicClientConnectionState ConnectClient(QuicTestServer server, ulong maxDatagramFrameSize)
    {
        using (Diagnostics.Phase("handshake"))
        {
            return Connect(server, SmallClientLimits with { MaxDatagramFrameSize = maxDatagramFrameSize });
        }
    }
}
