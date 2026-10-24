using System.Text;
using Curl.Conformance.Differential.Protocols.Binary;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Binary;

[TestClass]
public sealed class MqttResponderTests
{
    [TestMethod]
    public async Task RespondAsync_Connect_AnswersConnackAndLogsThePacket()
    {
        var (output, log) = await RunAsync([0x10, 0x02, 0xAB, 0x0A]);

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00 }, output);
        Assert.AreEqual("type=1 flags=0 body=b'\\xab\\n'", log);
    }

    [TestMethod]
    public async Task RespondAsync_Subscribe_AnswersSubackAndPublishesPayload()
    {
        byte[] subscribe = [0x82, 0x08, 0x00, 0x01, 0x00, 0x03, (byte)'a', (byte)'/', (byte)'b', 0x00];

        var (output, log) = await RunAsync(subscribe);

        byte[] expected = [0x90, 0x03, 0x00, 0x01, 0x00, 0x30, 0x0C, 0x00, 0x03, (byte)'a', (byte)'/', (byte)'b', .. "payload"u8];
        CollectionAssert.AreEqual(expected, output);
        Assert.AreEqual("type=8 flags=2 body=b'\\x00\\x01\\x00\\x03a/b\\x00'", log);
    }

    [TestMethod]
    public async Task RespondAsync_TopicLengthPastTheBody_PublishesWhatIsThere()
    {
        var (output, _) = await RunAsync([0x82, 0x06, 0x00, 0x01, 0x00, 0x09, (byte)'a', (byte)'b']);

        byte[] expected = [0x90, 0x03, 0x00, 0x01, 0x00, 0x30, 0x0B, 0x00, 0x02, (byte)'a', (byte)'b', .. "payload"u8];
        CollectionAssert.AreEqual(expected, output);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x82, 0x00 }, new byte[] { 0x90, 0x03, 0x00 })]
    [DataRow(new byte[] { 0x82, 0x03, 0x00, 0x01, 0x00 }, new byte[] { 0x90, 0x03, 0x00, 0x01, 0x00 })]
    public async Task RespondAsync_SubscribeTooShortForATopic_AnswersSubackThenEnds(byte[] input, byte[] expected)
    {
        var (output, _) = await RunAsync([.. input, 0x10, 0x00]);

        CollectionAssert.AreEqual(expected, output);
    }

    [TestMethod]
    public async Task RespondAsync_TopicTooLongForTheLengthByte_AnswersSubackThenEnds()
    {
        byte[] input = [0x82, 0xFE, 0x01, 0x00, 0x01, 0x00, 0xFA, .. Enumerable.Repeat((byte)'t', 250), 0x10, 0x00];

        var (output, log) = await RunAsync(input);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x03, 0x00, 0x01, 0x00 }, output);
        Assert.AreEqual(1, log.Split('\n').Length);
    }

    [TestMethod]
    public async Task RespondAsync_OtherPacketsThenDisconnect_LogsEachAndStopsAtDisconnect()
    {
        byte[] publish = [0x31, 0xC8, 0x01, .. Enumerable.Repeat((byte)'p', 200)];

        var (output, log) = await RunAsync([.. publish, 0xE0, 0x00, 0x10, 0x00]);

        Assert.IsEmpty(output);
        var lines = log.Split('\n');
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("type=3 flags=1 body=b'" + new string('p', 200) + "'", lines[0]);
        Assert.AreEqual("type=14 flags=0 body=b''", lines[1]);
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x10 })]
    [DataRow(new byte[] { 0x10, 0x80 })]
    public async Task RespondAsync_StreamEndsBeforeAPacketIsComplete_EndsWithoutLogging(byte[] input)
    {
        var (output, log) = await RunAsync(input);

        Assert.IsEmpty(output);
        Assert.AreEqual(string.Empty, log);
    }

    private static async Task<(byte[] Output, string Log)> RunAsync(byte[] input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(input);

        await new MqttResponder(log).RespondAsync(connection, CancellationToken.None);

        return (connection.Written, Encoding.UTF8.GetString(log.Take()));
    }
}
