using Curl.Extended.Protocol.Kafka.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>Checks that Metadata and Produce are tried again while an auto-created topic has no leader ready, within a shared bound.</summary>
[TestClass]
public sealed class KafkaLeaderRetryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    private static readonly (int NodeId, string Host, int Port)[] Brokers = [(1, "broker", 9092)];

    [TestMethod]
    [DataRow((short)3)]
    [DataRow((short)5)]
    public async Task ExecuteAsync_TopicHasNoLeaderYet_AsksMetadataAgainAndProduces(short pendingError)
    {
        ScriptedKafkaConnection connection = new(
            KafkaTestBytes.MetadataResponse(1, pendingError, Brokers, (pendingError, 0, -1)),
            KafkaTestBytes.MetadataLeader(2, "broker", 9092),
            KafkaTestBytes.ProduceResponse(3, 0, 17));
        ImmediateTimeProvider time = new();

        TransferResult result = await RunAsync(connection, time);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, connection.Writes.Count);
        CollectionAssert.AreEqual(KafkaRequests.Produce(3, "new", 0, KafkaRecordBatch.Encode("A"u8, Now.ToUnixTimeMilliseconds())), connection.Writes[2]);
        Assert.AreEqual(1, time.TimersCreated);
    }

    [TestMethod]
    public async Task ExecuteAsync_TopicNeverGetsALeader_StopsAfterTenAskingsWithTheBrokerError()
    {
        byte[][] answers = Enumerable.Range(1, 10).Select(id => KafkaTestBytes.MetadataResponse(id, 3, Brokers, (3, 0, -1))).ToArray();
        ScriptedKafkaConnection connection = new(answers);
        ImmediateTimeProvider time = new();

        TransferResult result = await RunAsync(connection, time);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "UNKNOWN_TOPIC_OR_PARTITION");
        Assert.AreEqual(10, connection.Writes.Count);
        Assert.AreEqual(9, time.TimersCreated);
    }

    [TestMethod]
    [DataRow((short)3)]
    [DataRow((short)5)]
    [DataRow((short)6)]
    public async Task ExecuteAsync_ProduceFindsNoReadyLeader_AsksMetadataAgainAndSendsTheSameRecord(short pendingError)
    {
        ScriptedKafkaConnection connection = new(
            KafkaTestBytes.MetadataLeader(1, "broker", 9092),
            KafkaTestBytes.ProduceResponse(2, pendingError),
            KafkaTestBytes.MetadataLeader(3, "broker", 9092),
            KafkaTestBytes.ProduceResponse(4, 0, 17));
        ImmediateTimeProvider time = new();

        TransferResult result = await RunAsync(connection, time);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(4, connection.Writes.Count);
        CollectionAssert.AreEqual(KafkaRequests.Metadata(3, "new"), connection.Writes[2]);
        CollectionAssert.AreEqual(KafkaRequests.Produce(4, "new", 0, KafkaRecordBatch.Encode("A"u8, Now.ToUnixTimeMilliseconds())), connection.Writes[3]);
        Assert.AreEqual(1, time.TimersCreated);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProduceNeverFindsAReadyLeader_StopsAfterTenSendingsWithTheBrokerError()
    {
        // Odd correlation ids answer the Metadata requests, even ones the Produce requests.
        byte[][] answers = Enumerable.Range(1, 20)
            .Select(id => id % 2 == 1 ? KafkaTestBytes.MetadataLeader(id, "broker", 9092) : KafkaTestBytes.ProduceResponse(id, 6))
            .ToArray();
        ScriptedKafkaConnection connection = new(answers);
        ImmediateTimeProvider time = new();

        TransferResult result = await RunAsync(connection, time);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "NOT_LEADER_OR_FOLLOWER");
        Assert.AreEqual(20, connection.Writes.Count);
        Assert.AreEqual(9, time.TimersCreated);
    }

    [TestMethod]
    public async Task ExecuteAsync_MetadataUsedEveryRetry_ProduceErrorIsNotRetried()
    {
        byte[][] answers =
        [
            .. Enumerable.Range(1, 9).Select(id => KafkaTestBytes.MetadataResponse(id, 5, Brokers, (5, 0, -1))),
            KafkaTestBytes.MetadataLeader(10, "broker", 9092),
            KafkaTestBytes.ProduceResponse(11, 6),
        ];
        ScriptedKafkaConnection connection = new(answers);
        ImmediateTimeProvider time = new();

        TransferResult result = await RunAsync(connection, time);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "NOT_LEADER_OR_FOLLOWER");
        Assert.AreEqual(11, connection.Writes.Count);
        Assert.AreEqual(9, time.TimersCreated);
    }

    [TestMethod]
    public async Task ExecuteAsync_OtherMetadataError_IsNotRetried()
    {
        ScriptedKafkaConnection connection = new(KafkaTestBytes.MetadataResponse(1, 29, Brokers, (29, 0, -1)));
        ImmediateTimeProvider time = new();

        TransferResult result = await RunAsync(connection, time);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(1, connection.Writes.Count);
        Assert.AreEqual(0, time.TimersCreated);
    }

    private static async Task<TransferResult> RunAsync(ScriptedKafkaConnection connection, ImmediateTimeProvider time)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("kafka://broker/new"),
            Output = new MemoryStream(),
            PostData = "A"u8.ToArray(),
            TimeProvider = time,
        };
        return await new KafkaProtocolHandler(new ScriptedKafkaConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);
    }

    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public int TimersCreated { get; private set; }

        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TimersCreated++;
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoOpTimer();
        }
    }

    private sealed class NoOpTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
