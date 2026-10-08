using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Adversarial black-box tests of <see cref="MqttProtocolHandler" /> (BL-1513), by the method
/// in <c>Documentation/Wiki/Adversarial-Testing.md</c>: remaining lengths at the edges of
/// each varint width, topics carrying wildcards and NUL, CONNACK return codes and QoS bits
/// out of range, packets cut short or delivered a byte at a time, and the handler run again
/// and on many tasks at once. The oracle is curl 8.21.0's <c>lib/mqtt.c</c>, which decodes
/// a remaining length without checking it is minimal and copies a PUBLISH's body to the
/// output without parsing its topic or QoS; every exchange is replayed through
/// <see cref="ScriptedConnection" />, so nothing here touches a network.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerAdversarialTests
{
    private const string FixedSuffix = "PBadK4E3";

    private const string WeirdServerReply = "Weird server reply";

    private const string ReceiveFailed = "Failure when receiving data from the peer";

    private const string ConnectionDisconnected = "Connection disconnected";

    private static readonly byte[] MeasuredConnect = Bytes("10 18 00 04", "MQTT", "04 02 00 3C 00 0C", "curlPBadK4E3");

    private static readonly byte[] Connack = Bytes("20 02 00 00");

    private static readonly byte[] Suback = Bytes("90 03 00 01 00");

    private static readonly byte[] Disconnect = Bytes("E0 00");

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries: the remaining-length varint at the edges of its widths.

    [TestMethod]
    public async Task ExecuteAsync_PublishRemainingLength127_FitsOneByteAndIsWrittenWhole()
    {
        byte[] body = Concat(Bytes("00 01", "t"), Encoding.ASCII.GetBytes(new string('p', 124)));
        ScriptedConnection connection = new(Connack, Suback, Concat(Bytes("30 7F"), body), Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        CollectionAssert.AreEqual(body, output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(127), result);
        Assert.AreEqual(TransferResult.Success(127), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishRemainingLength128_TakesTwoBytesAndIsWrittenWhole()
    {
        byte[] body = Concat(Bytes("00 01", "t"), Encoding.ASCII.GetBytes(new string('p', 125)));
        ScriptedConnection connection = new(Connack, Suback, Concat(Bytes("30 80 01"), body), Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        CollectionAssert.AreEqual(body, output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(128), result);
        Assert.AreEqual(TransferResult.Success(128), result);
    }

    /// <summary>
    /// 268435455 is the largest length four bytes can carry; the server closes after six
    /// bytes of it. The body is streamed, never allocated whole, so this ends at once as a
    /// partial file with the six bytes written.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PublishRemainingLength268435455ClosedEarly_WritesWhatArrivedThenIsPartialFile()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("30 FF FF FF 7F 00 01", "tabc"));
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 01", "tabc"), output.ToArray());
        Diagnostics.AssertResult(new TransferResult(CurlExitCode.PartialFile, 6, "Transferred a partial file"), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.PartialFile, 6, "Transferred a partial file"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemainingLengthFourContinuationBytesAndNoFifth_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Bytes("20 80 80 80 80"));

        TransferResult result = await RunAsync(connection, "mqtt://h/t", new RecordingStream());

        Diagnostics.AssertResult(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishWithFifthRemainingLengthByte_IsWeirdServerReplyAndWritesNothing()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("30 80 80 80 80 01 00 01", "t"));
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        Assert.AreEqual(0, output.ToArray().Length);
        Diagnostics.AssertResult(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    // Malformed input: fields that are almost right.

    /// <summary>
    /// <c>85 00</c> is 5 in a non-minimal two-byte form; curl's <c>mqtt_decode_len</c> does
    /// not insist on the shortest form, so the PUBLISH is read as five bytes long.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PublishRemainingLengthNotInShortestForm_IsDecodedAndWritten()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("30 85 00 00 01", "thi"), Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 01", "thi"), output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(5), result);
        Assert.AreEqual(TransferResult.Success(5), result);
    }

    /// <summary>
    /// The topic length (255) claims more than the packet holds; curl does not parse the
    /// topic, so the three bytes of the body are written as they are.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PublishTopicLengthPastItsBody_WritesTheBodyAsItIs()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("30 03 00 FF", "A"), Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 FF", "A"), output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(3), result);
        Assert.AreEqual(TransferResult.Success(3), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackHeaderThenClose_IsRecvError()
    {
        ScriptedConnection connection = new(Bytes("20 02"));

        TransferResult result = await RunAsync(connection, "mqtt://h/t", new RecordingStream());

        Diagnostics.AssertResult(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubackTypeByteThenClose_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Connack, Bytes("90"));

        TransferResult result = await RunAsync(connection, "mqtt://h/t", new RecordingStream());

        Diagnostics.AssertResult(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TopicWithEncodedNul_SubscribesWithTheNulByte()
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(connection, "mqtt://h/a%00b", new RecordingStream());

        CollectionAssert.AreEqual(Concat(MeasuredConnect, Bytes("82 08 00 01 00 03", "a", "00", "b", "00")), connection.Written);
    }

    // Invalid partitions: values outside the ranges the specification allows.

    /// <summary>
    /// Return codes 1 to 5 are the refusals MQTT 3.1.1 defines and 0xFF is outside them;
    /// curl names neither, it only reports the two bytes it expected and got, in the
    /// lowercase hex of <c>mqtt_verify_connack</c>'s <c>%02x</c>.
    /// </summary>
    [TestMethod]
    [DataRow("01", DisplayName = "unacceptable protocol version")]
    [DataRow("02", DisplayName = "identifier rejected")]
    [DataRow("03", DisplayName = "server unavailable")]
    [DataRow("04", DisplayName = "bad user name or password")]
    [DataRow("ff", DisplayName = "undefined return code")]
    public async Task ExecuteAsync_ConnackReturnCodeOtherThanZero_IsWeirdServerReplyAndSubscribesNothing(string returnCode)
    {
        ScriptedConnection connection = new(Bytes("20 02 00 " + returnCode));

        TransferResult result = await RunAsync(connection, "mqtt://h/t", new RecordingStream());

        TransferResult expected = new(CurlExitCode.WeirdServerReply, 0, "Expected 0000 but got 00" + returnCode);
        Diagnostics.AssertResult(expected, result);
        Assert.AreEqual(expected, result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    /// <summary>
    /// QoS 3 is reserved and the DUP and RETAIN bits are set; curl copies the body without
    /// looking at the flags, so it is written as it is.
    /// </summary>
    [TestMethod]
    [DataRow("36", DisplayName = "QoS 3")]
    [DataRow("3F", DisplayName = "QoS 3, DUP and RETAIN")]
    public async Task ExecuteAsync_PublishWithReservedQoS_WritesTheBodyAsItIs(string typeByte)
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes(typeByte + " 05 00 01", "tok"), Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 01", "tok"), output.ToArray());
        Diagnostics.AssertResult(TransferResult.Success(5), result);
        Assert.AreEqual(TransferResult.Success(5), result);
    }

    /// <summary>
    /// <c>+</c> and an encoded <c>#</c> reach the SUBSCRIBE as the wildcards they are; a
    /// bare <c>#</c> starts the URL's fragment, which is no part of the topic.
    /// </summary>
    [TestMethod]
    [DataRow("mqtt://h/a/+", "a/+", DisplayName = "single-level wildcard")]
    [DataRow("mqtt://h/a/%23", "a/#", DisplayName = "encoded multi-level wildcard")]
    [DataRow("mqtt://h/a/#", "a/", DisplayName = "bare hash starts the fragment")]
    public async Task ExecuteAsync_TopicWithWildcard_SubscribesToItAsWritten(string url, string topic)
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(connection, url, new RecordingStream());

        byte[] subscribe = Concat(Bytes("82"), [(byte)(5 + topic.Length)], Bytes("00 01 00"), [(byte)topic.Length], Encoding.ASCII.GetBytes(topic), Bytes("00"));
        CollectionAssert.AreEqual(Concat(MeasuredConnect, subscribe), connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubackBeforeConnack_IsTakenAsConnackOfWrongLength()
    {
        ScriptedConnection connection = new(Suback);

        TransferResult result = await RunAsync(connection, "mqtt://h/t", new RecordingStream());

        TransferResult expected = new(CurlExitCode.WeirdServerReply, 0, "CONNACK expected Remaining Length 2, got 3");
        Diagnostics.AssertResult(expected, result);
        Assert.AreEqual(expected, result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    // State and concurrency: delivery a byte at a time, reuse, and parallel transfers.

    [TestMethod]
    public async Task ExecuteAsync_MeasuredExchangeOneBytePerRead_WritesTheSameBytesAndResult()
    {
        byte[] stream = Concat(Connack, Suback, Bytes("30 0C 00 05", "a/b/c", string.Empty, "HELLO"));
        ScriptedConnection connection = new([.. stream.Select(single => new[] { single })]);
        RecordingStream output = new();

        TransferResult result = await RunAsync(connection, "mqtt://h/a/b/c", output);

        CollectionAssert.AreEqual(Bytes("00 05", "a/b/c", string.Empty, "HELLO"), output.ToArray());
        Diagnostics.AssertResult(new TransferResult(CurlExitCode.RecvError, 12, ConnectionDisconnected), result);
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 12, ConnectionDisconnected), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoByteRemainingLengthSplitAtEveryOffset_WritesThePublishWhole()
    {
        byte[] body = Concat(Bytes("00 01", "t"), Encoding.ASCII.GetBytes(new string('p', 200)));
        byte[] publish = Concat(Bytes("30 CB 01"), body);
        for (int split = 1; split < 4; split++)
        {
            ScriptedConnection connection = new(Connack, Suback, publish[..split], publish[split..], Disconnect);
            RecordingStream output = new();

            TransferResult result = await RunAsync(connection, "mqtt://h/t", output);

            Diagnostics.Act("split offset", split);
            CollectionAssert.AreEqual(body, output.ToArray(), $"split at {split}");
            Assert.AreEqual(TransferResult.Success(203), result, $"split at {split}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerRunTwice_SecondRunMatchesTheFirst()
    {
        ScriptedConnection first = new(Connack, Suback, Bytes("30 07 00 01", "tHELL"), Disconnect);
        ScriptedConnection second = new(Connack, Suback, Bytes("30 07 00 01", "tHELL"), Disconnect);
        MqttProtocolHandler handler = new(new SequenceConnector(first, second), () => FixedSuffix);

        TransferResult firstResult = await handler.ExecuteAsync(Context("mqtt://h/t", new RecordingStream()));
        TransferResult secondResult = await handler.ExecuteAsync(Context("mqtt://h/t", new RecordingStream()));

        Diagnostics.ActResult(secondResult);
        Assert.AreEqual(firstResult, secondResult);
        Assert.AreEqual(TransferResult.Success(7), secondResult);
        CollectionAssert.AreEqual(first.Written, second.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_SixteenTransfersAtOnceOnOneHandler_EachGetsTheResultOfARunAlone()
    {
        ScriptedConnection[] connections = [.. Enumerable.Range(0, 16).Select(index =>
            new ScriptedConnection(Connack, Suback, Bytes("30 08 00 01", "t" + index.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + "ABCD"), Disconnect))];
        MqttProtocolHandler handler = new(new SequenceConnector(connections), () => FixedSuffix);
        RecordingStream[] outputs = [.. connections.Select(_ => new RecordingStream())];

        TransferResult[] results = await Task.WhenAll(outputs.Select(output =>
            Task.Run(async () => await handler.ExecuteAsync(Context("mqtt://h/t", output)))));

        Diagnostics.Act("results", string.Join("; ", results));
        CollectionAssert.AreEqual(Enumerable.Repeat(TransferResult.Success(8), 16).ToArray(), results);
        foreach (ScriptedConnection connection in connections)
        {
            CollectionAssert.AreEqual(Concat(MeasuredConnect, Bytes("82 06 00 01 00 01", "t", "00")), connection.Written);
            Assert.IsTrue(connection.IsDisposed);
        }

        CollectionAssert.AreEquivalent(
            Enumerable.Range(0, 16).Select(index => "t" + index.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + "ABCD").ToArray(),
            outputs.Select(output => Encoding.ASCII.GetString(output.ToArray()[2..])).ToArray());
    }

    private static TransferContext Context(string url, Stream output) =>
        new() { Url = CurlUrl.Parse(url), Output = output };

    /// <summary>Alternates hex and ASCII: hex, text, hex, text, and so on.</summary>
    private static byte[] Bytes(params string[] parts) =>
        Concat([.. parts.Select((part, index) => index % 2 == 0
            ? Convert.FromHexString(part.Replace(" ", string.Empty, StringComparison.Ordinal))
            : Encoding.ASCII.GetBytes(part))]);

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>
    /// Runs the transfer with the fixed identifier suffix, writing its URL and scripted reads
    /// as ARRANGE lines and its result, bytes sent and output as ACT lines.
    /// </summary>
    private async Task<TransferResult> RunAsync(ScriptedConnection connection, string url, RecordingStream output)
    {
        TransferContext context = Context(url, output);
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads(connection.Reads);

        TransferResult result = await new MqttProtocolHandler(FakeConnector.For(connection), () => FixedSuffix).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActPackets("sent", connection.Written);
        Diagnostics.ActOutput(output);
        return result;
    }

    /// <summary>An <see cref="IConnector" /> that hands out its connections one per connect, in order.</summary>
    private sealed class SequenceConnector(params IConnection[] connections) : IConnector
    {
        private int next = -1;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connections[Interlocked.Increment(ref next)]));
    }
}
