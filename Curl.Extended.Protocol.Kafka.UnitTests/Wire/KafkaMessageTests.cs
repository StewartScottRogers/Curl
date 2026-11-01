using Curl.Extended.Protocol.Kafka.Client;

namespace Curl.Extended.Protocol.Kafka.Wire;

/// <summary>
/// Checks requests and responses against the protocol guide's "Common Request and Response Structure", "Headers",
/// Produce Request (Version: 3), Fetch Request (Version: 4) and their responses.
/// </summary>
[TestClass]
public sealed class KafkaMessageTests
{
    private static readonly byte[] RequestHeaderTail = [0, 0, 0, 7, 0, 4, (byte)'c', (byte)'u', (byte)'r', (byte)'l'];

    [TestMethod]
    public void Produce_IsSizedHeaderV1ThenAcksTimeoutTopicPartitionAndRecords()
    {
        byte[] request = KafkaRequests.Produce(7, "t", 2, [0xAB]);

        byte[] expected =
        [
            0, 0, 0, 42, 0, 0, 0, 3, .. RequestHeaderTail,
            0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0x75, 0x30,
            0, 0, 0, 1, 0, 1, (byte)'t', 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1, 0xAB,
        ];
        CollectionAssert.AreEqual(expected, request);
    }

    [TestMethod]
    public void Fetch_IsSizedHeaderV1ThenLimitsIsolationTopicPartitionAndOffset()
    {
        byte[] request = KafkaRequests.Fetch(7, "t", 2, 0x0102);

        byte[] expected =
        [
            0, 0, 0, 58, 0, 1, 0, 4, .. RequestHeaderTail,
            0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0x01, 0xF4, 0, 0, 0, 1, 0, 0x10, 0, 0, 0,
            0, 0, 0, 1, 0, 1, (byte)'t', 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 1, 2, 0, 0x10, 0, 0,
        ];
        CollectionAssert.AreEqual(expected, request);
    }

    [TestMethod]
    public void ReadProduce_GivesThePartitionsErrorAndBaseOffset()
    {
        KafkaPartitionResponse response = KafkaResponses.ReadProduce(KafkaTestBytes.BodyOf(KafkaTestBytes.ProduceResponse(1, 6, 42)));

        Assert.AreEqual((short)6, response.ErrorCode);
        Assert.AreEqual(42L, response.Offset);
    }

    [TestMethod]
    public void ReadFetch_SkipsAbortedTransactionsAndGivesHighWatermarkAndRecords()
    {
        KafkaPartitionResponse response = KafkaResponses.ReadFetch(KafkaTestBytes.BodyOf(KafkaTestBytes.FetchResponse(1, 0, 9, [1, 2], abortedTransactions: 2)));

        Assert.AreEqual((short)0, response.ErrorCode);
        Assert.AreEqual(9L, response.Offset);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, response.Records.ToArray());
    }

    [TestMethod]
    public void ReadFetch_NullRecords_GivesEmptyRecords() =>
        Assert.AreEqual(0, KafkaResponses.ReadFetch(KafkaTestBytes.BodyOf(KafkaTestBytes.FetchResponse(1, 0, 0, null))).Records.Length);

    [TestMethod]
    public void ReadProduce_TwoTopics_Throws() =>
        Assert.AreEqual(
            "Kafka response holds 2 entries where the request named one",
            Assert.ThrowsExactly<FormatException>(() => KafkaResponses.ReadProduce(new KafkaWriter().Int32(2).ToArray())).Message);

    [TestMethod]
    public async Task ReadAsync_ResponseInChunks_GivesTheBodyAfterTheCorrelationId()
    {
        byte[] framed = KafkaTestBytes.Framed(new KafkaWriter().Int32(5).Raw([1, 2, 3]));
        ScriptedKafkaConnection connection = new(framed[..3], framed[3..9], framed[9..]);

        ReadOnlyMemory<byte> body = await KafkaResponseReader.ReadAsync(connection, 5, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, body.ToArray());
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(KafkaResponseReader.MaxResponseSize + 1)]
    public async Task ReadAsync_SizeOutOfRange_Throws(int size)
    {
        ScriptedKafkaConnection connection = new(new KafkaWriter().Int32(size).ToArray());

        FormatException thrown = await Assert.ThrowsExactlyAsync<FormatException>(async () => await KafkaResponseReader.ReadAsync(connection, 1, CancellationToken.None));

        Assert.AreEqual($"Kafka response size {size} is out of range", thrown.Message);
    }

    [TestMethod]
    public async Task ReadAsync_OtherCorrelationId_Throws()
    {
        ScriptedKafkaConnection connection = new(KafkaTestBytes.Framed(new KafkaWriter().Int32(2)));

        FormatException thrown = await Assert.ThrowsExactlyAsync<FormatException>(async () => await KafkaResponseReader.ReadAsync(connection, 1, CancellationToken.None));

        Assert.AreEqual("Kafka response has correlation id 2, not 1", thrown.Message);
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionEndsInsideTheResponse_ThrowsEndOfStream() =>
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await KafkaResponseReader.ReadAsync(new ScriptedKafkaConnection([0, 0, 0, 8, 0]), 1, CancellationToken.None));
}
