using Curl.Extended.Protocol.Kafka.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>Checks that <c>-T</c> publishes the file's bytes as <c>-d</c> does, never consuming.</summary>
[TestClass]
public sealed class KafkaUploadTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    [TestMethod]
    public async Task ExecuteAsync_Upload_ProducesTheFilesBytesAsTheRecordValue()
    {
        ScriptedKafkaConnection connection = new(KafkaTestBytes.MetadataLeader(1, "broker", 9092), KafkaTestBytes.ProduceResponse(2, 0, 17));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("kafka://broker/orders/2"),
            Output = new MemoryStream(),
            Upload = new MemoryStream("filebody"u8.ToArray()),
            TimeProvider = new FixedTimeProvider(Now),
        };

        TransferResult result = await new KafkaProtocolHandler(new ScriptedKafkaConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(KafkaRequests.Produce(2, "orders", 2, KafkaRecordBatch.Encode("filebody"u8, Now.ToUnixTimeMilliseconds())), connection.Writes[1]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
