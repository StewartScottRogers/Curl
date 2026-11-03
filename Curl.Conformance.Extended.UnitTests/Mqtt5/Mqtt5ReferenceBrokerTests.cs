using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Pins the MQTT 5 reference broker's own checks (BL-4517) with hand-made client bytes, the faults curl
/// itself never sends.
/// </summary>
[TestClass]
public sealed class Mqtt5ReferenceBrokerTests
{
    private static readonly byte[] MinimalConnectBody = [0, 4, .. "MQTT"u8, 5, 0x02, 0, 0, 0, 0, 1, (byte)'c'];

    [TestMethod]
    public async Task ServeAsync_ConnectThenUnansweredAndAnsweredPackets_SendsConnAckAndResponses()
    {
        Mqtt5ReferenceBroker broker = new(
            [0x20, 0x03, 0x01, 0x00, 0x00],
            new Dictionary<int, Func<Mqtt5WirePacket, byte[]>> { [12] = _ => [0xD0, 0x00], [Mqtt5WirePacket.Subscribe] = _ => [] });

        byte[] sent = await ServeAsync(broker, [.. Mqtt5WirePacket.Frame(0x10, MinimalConnectBody), 0xC0, 0x00, 0x82, 0x00, 0xE0, 0x00]);

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x03, 0x01, 0x00, 0x00, 0xD0, 0x00 }, sent);
        Assert.AreEqual("c", broker.Connect!.ClientIdentifier);
        Assert.HasCount(4, broker.ReceivedPackets);
    }

    [TestMethod]
    public async Task ServeAsync_NoResponders_SendsOnlyTheDefaultConnAck()
    {
        Mqtt5ReferenceBroker broker = new();

        byte[] sent = await ServeAsync(broker, [.. Mqtt5WirePacket.Frame(0x10, MinimalConnectBody), 0x62, 0x00]);

        CollectionAssert.AreEqual(Mqtt5ReferenceBroker.SuccessConnAck, sent);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xC0, 0x00 }, "Mqtt5:MQTT-3.1.0-1")]
    [DataRow(new byte[] { 0x11, 0x00 }, "Mqtt5:MQTT-2.1.3-1")]
    [DataRow(new byte[] { 0x80, 0x00 }, "Mqtt5:MQTT-2.1.3-1")]
    [DataRow(new byte[] { 0x10, 0x80, 0x00 }, "Mqtt5:MQTT-1.5.5-1")]
    [DataRow(new byte[] { 0x10, 0xFF, 0xFF, 0xFF, 0xFF }, "Mqtt5:MQTT-1.5.5-1")]
    [DataRow(new byte[] { 0x10, 0x05, 0x00 }, "Mqtt5:MQTT5-2.1-1")]
    [DataRow(new byte[] { 0x10 }, "Mqtt5:MQTT5-2.1-1")]
    public async Task ServeAsync_FaultyFraming_RaisesTheBrokenRequirement(byte[] clientBytes, string requirementId)
    {
        Mqtt5ConformanceViolationException violation = await Assert.ThrowsExactlyAsync<Mqtt5ConformanceViolationException>(() => ServeAsync(new Mqtt5ReferenceBroker(), clientBytes));

        Assert.AreEqual(requirementId, violation.RequirementId);
    }

    [TestMethod]
    public async Task ServeAsync_SecondConnect_RaisesMqtt310_1()
    {
        byte[] connect = Mqtt5WirePacket.Frame(0x10, MinimalConnectBody);

        Mqtt5ConformanceViolationException violation = await Assert.ThrowsExactlyAsync<Mqtt5ConformanceViolationException>(() => ServeAsync(new Mqtt5ReferenceBroker(), [.. connect, .. connect]));

        StringAssert.Contains(violation.Message, "second time");
    }

    [TestMethod]
    public void Frame_LongBody_EncodesTheLengthInTwoBytes()
    {
        Assert.AreEqual("30C801", Convert.ToHexString(Mqtt5WirePacket.Frame(0x30, new byte[200])[..3]));
    }

    [TestMethod]
    public void Parse_EveryPropertyTypeAndAWill_ReadsThemAll()
    {
        byte[] properties = [0x11, 0, 0, 0, 1, 0x21, 0, 1, 0x19, 1, 0x15, 0, 1, (byte)'a', 0x16, 0, 1, 9, 0x26, 0, 1, (byte)'k', 0, 0, 0x26, 0, 0, 0, 0];
        byte[] willProperties = [0x18, 0, 0, 0, 0];
        byte[] body =
        [
            0, 4, .. "MQTT"u8, 5, 0x2E | 0xC0, 0, 60, (byte)properties.Length, .. properties, 0, 1, (byte)'c',
            (byte)willProperties.Length, .. willProperties, 0, 1, (byte)'w', 0, 1, 7, 0, 1, (byte)'u', 0, 1, 8,
        ];

        Mqtt5ReceivedConnect connect = Mqtt5ReceivedConnect.Parse(body);

        CollectionAssert.AreEqual(new[] { 0x11, 0x21, 0x19, 0x15, 0x16, 0x26, 0x26 }, connect.PropertyIdentifiers.ToArray());
        Assert.IsTrue(connect.CleanStart);
        Assert.AreEqual("w", connect.WillTopic);
        Assert.AreEqual(60, connect.KeepAlive);
        Assert.AreEqual("u", connect.UserName);
        CollectionAssert.AreEqual(new byte[] { 8 }, connect.Password);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 116, 5, 2, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT-3.1.2-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 4, 2, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT5-3.1.2.2-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 3, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT-3.1.2-3")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 0x0A, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT-3.1.2-11")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 0x1C, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT-3.1.2-12")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 0x22, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT-3.1.2-13")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 2, 0x01, 0, 0, 0 }, "Mqtt5:MQTT5-2.2.2-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 4, 0x17, 1, 0x17, 1, 0, 0 }, "Mqtt5:MQTT5-2.2.2-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 0, 0, 0, 9 }, "Mqtt5:MQTT-3.1.3-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 0, 0, 1, 0 }, "Mqtt5:MQTT-1.5.4-2")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 0, 0, 1, 0xC0 }, "Mqtt5:MQTT-1.5.4-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 0, 0, 5 }, "Mqtt5:MQTT5-1.5-1")]
    [DataRow(new byte[] { 0, 4, 77, 81, 84, 84, 5, 2, 0, 0, 4, 0x11, 0, 0, 0, 0, 0 }, "Mqtt5:MQTT5-1.5-1")]
    public void Parse_FaultyConnect_RaisesTheBrokenRequirement(byte[] body, string requirementId)
    {
        Mqtt5ConformanceViolationException violation = Assert.ThrowsExactly<Mqtt5ConformanceViolationException>(() => Mqtt5ReceivedConnect.Parse(body));

        Assert.AreEqual(requirementId, violation.RequirementId);
    }

    private static async Task<byte[]> ServeAsync(Mqtt5ReferenceBroker broker, byte[] clientBytes)
    {
        using MemoryStream fromServer = new();
        using DuplexStream stream = new(new MemoryStream(clientBytes), fromServer);
        await broker.ServeAsync(new ReferenceConnection(stream, new WireExchange(1883, ReferenceTransport.Tcp)), CancellationToken.None);
        return fromServer.ToArray();
    }

    // Reads the client's bytes from one stream and keeps the server's in another.
    private sealed class DuplexStream(Stream reads, Stream writes) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => writes.Flush();

        public override int Read(byte[] buffer, int offset, int count) => reads.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => writes.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            reads.Dispose();
            base.Dispose(disposing);
        }
    }
}
