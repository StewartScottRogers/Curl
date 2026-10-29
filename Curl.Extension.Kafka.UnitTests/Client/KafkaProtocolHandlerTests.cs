using System.Text;
using Curl.Extension.Kafka.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Kafka.Client;

/// <summary>Checks the client exchange against the protocol guide's Produce and Fetch APIs and "Error Codes".</summary>
[TestClass]
public sealed class KafkaProtocolHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    [TestMethod]
    public async Task ExecuteAsync_Body_ProducesOneRecordToTheUrlsPartition()
    {
        ScriptedKafkaConnection connection = new(KafkaTestBytes.ProduceResponse(1, 0, 17));

        TransferResult result = await ExecuteAsync(connection, "kafka://broker/orders/2", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(KafkaRequests.Produce(1, "orders", 2, KafkaRecordBatch.Encode("hi"u8, Now.ToUnixTimeMilliseconds())), connection.Writes.Single());
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProduceRefused_EndsWithWeirdServerReplyNamingTheError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(KafkaTestBytes.ProduceResponse(1, 3)), "kafka://broker/orders", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Kafka broker answered error 3 (UNKNOWN_TOPIC_OR_PARTITION) for topic orders partition 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_FetchesFromTheOffsetUntilTheHighWatermark()
    {
        byte[] first = KafkaTestBytes.Batch(4, 1, 2, [.. KafkaTestBytes.Record(0, "a"u8.ToArray()), .. KafkaTestBytes.Record(1, "b"u8.ToArray())]);
        byte[] second = KafkaTestBytes.Batch(6, 0, 1, KafkaTestBytes.Record(0, "c"u8.ToArray()));
        ScriptedKafkaConnection connection = new(KafkaTestBytes.FetchResponse(1, 0, 7, first), KafkaTestBytes.FetchResponse(2, 0, 7, second));
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "kafka://broker/orders/1?offset=5", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("b\nc\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(4L, result.BytesTransferred);
        CollectionAssert.AreEqual(new[] { KafkaRequests.Fetch(1, "orders", 1, 5), KafkaRequests.Fetch(2, "orders", 1, 6) }.SelectMany(bytes => bytes).ToArray(), connection.Writes.SelectMany(bytes => bytes).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_FetchWithNoWholeBatch_StopsRatherThanFetchingTheSameOffsetAgain()
    {
        ScriptedKafkaConnection connection = new(KafkaTestBytes.FetchResponse(1, 0, 100, [0, 0, 0]));

        TransferResult result = await ExecuteAsync(connection, "kafka://broker/orders", new MemoryStream());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1, connection.Writes.Count);
    }

    [TestMethod]
    [DataRow((short)1, "Kafka broker answered error 1 (OFFSET_OUT_OF_RANGE) for topic orders partition 0")]
    [DataRow((short)999, "Kafka broker answered error 999 (UNNAMED) for topic orders partition 0")]
    public async Task ExecuteAsync_FetchRefused_EndsWithWeirdServerReplyNamingTheError(short errorCode, string message)
    {
        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(KafkaTestBytes.FetchResponse(1, errorCode, 0, null)), "kafka://broker/orders", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_CorruptBatch_EndsWithWeirdServerReply()
    {
        byte[] batch = KafkaTestBytes.Batch(0, 0, 1, KafkaTestBytes.Record(0, "a"u8.ToArray()));
        batch[^2] ^= 0xFF;

        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(KafkaTestBytes.FetchResponse(1, 0, 1, batch)), "kafka://broker/orders", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Kafka record batch fails its CRC-32C check", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TruncatedResponseBody_EndsWithWeirdServerReply()
    {
        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(KafkaTestBytes.Framed(new KafkaWriter().Int32(1))), "kafka://broker/orders", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(KafkaReader.Truncated, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WrongCorrelationId_EndsWithWeirdServerReply()
    {
        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(KafkaTestBytes.ProduceResponse(9, 0)), "kafka://broker/orders", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Kafka response has correlation id 9, not 1", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionEndsBeforeTheResponse_EndsWithRecvError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(), "kafka://broker/orders", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Connection disconnected", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteFails_EndsWithSendError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection { FailsWrites = true }, "kafka://broker/orders", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending Kafka Produce request", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputFails_EndsWithWriteError()
    {
        byte[] batch = KafkaTestBytes.Batch(0, 0, 1, KafkaTestBytes.Record(0, "abc"u8.ToArray()));

        TransferResult result = await ExecuteAsync(new ScriptedKafkaConnection(KafkaTestBytes.FetchResponse(1, 0, 1, batch)), "kafka://broker/orders", new FailingStream());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 4 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("kafka://broker/t", 9092, false)]
    [DataRow("kafkas://broker/t", 9093, true)]
    [DataRow("KAFKAS://broker:7000/t", 7000, true)]
    public async Task ExecuteAsync_Url_ConnectsToItsPortWithTlsForKafkas(string url, int port, bool useTls)
    {
        ScriptedKafkaConnector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new KafkaProtocolHandler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("broker", connector.Target!.Host);
        Assert.AreEqual(port, connector.Target.Port);
        Assert.AreEqual(useTls, connector.Target.UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoTopic_EndsWithUrlMalformatBeforeConnecting()
    {
        ScriptedKafkaConnector connector = new(ConnectResult.Refused("x"));

        TransferResult result = await new KafkaProtocolHandler(connector).ExecuteAsync(Context("kafka://broker/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(KafkaClientMessages.NoTopic, result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new KafkaProtocolHandler(new ScriptedKafkaConnector(ConnectResult.Refused("x"))).ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_AreKafkaAndKafkas() =>
        CollectionAssert.AreEqual(new[] { "kafka", "kafkas" }, new KafkaProtocolHandler(new ScriptedKafkaConnector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedKafkaConnection connection, string url, Stream output, string? body = null) =>
        new KafkaProtocolHandler(new ScriptedKafkaConnector(ConnectResult.Connected(connection))).ExecuteAsync(Context(url, output, body));

    private static TransferContext Context(string url, Stream output, string? body = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        PostData = body is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(body),
        TimeProvider = new FixedTimeProvider(Now),
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("full");
    }
}
