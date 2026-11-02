using Curl.Conformance.Extended.Kafka;
using Curl.Conformance.Extended.Kafka.Records;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Records;

/// <summary>The reference Fetch API's edges that no curl run reaches.</summary>
[TestClass]
public sealed class KafkaFetchApiTests
{
    [TestMethod]
    public void Answer_NegativeFetchOffset_IsOffsetOutOfRangeWithNullRecords()
    {
        byte[] body = new KafkaRecordsWriter().Int32(-1).Int32(500).Int32(1).Int32(1024).Int8(0)
            .Int32(1).String("orders").Int32(1).Int32(0).Int64(-1).Int32(1024).ToArray();

        KafkaRecordsReader answer = new(new KafkaFetchApi(new KafkaReferenceLog()).Answer(new KafkaReferenceRequest(1, 4, 1, "curl", body)));

        Assert.AreEqual((0, 1, "orders", 1, 0), (answer.Int32(), answer.Int32(), answer.NullableString(), answer.Int32(), answer.Int32()));
        Assert.AreEqual((KafkaFetchApi.OffsetOutOfRange, 0L, 0L, 0), (answer.Int16(), answer.Int64(), answer.Int64(), answer.Int32()));
        Assert.IsNull(answer.NullableBytes());
    }
}
