using Curl.Conformance.Extended.Kafka.Records;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Records;

/// <summary>The reference broker's own record batch codec, on edges no curl run reaches.</summary>
[TestClass]
public sealed class KafkaReferenceRecordBatchTests
{
    [TestMethod]
    public void Read_WrittenBatchWithKeysAndHeaders_RoundTripsEveryField()
    {
        byte[] bytes = KafkaReferenceRecordBatch.Write(5, [new KafkaReferenceRecord(0, -3, "k"u8.ToArray(), null, [new("h", null), new("g", "v"u8.ToArray())])], 0, 100);

        KafkaReferenceRecordBatch batch = KafkaReferenceRecordBatch.Read(bytes);

        Assert.AreEqual((5L, true, 100L, 97L), (batch.BaseOffset, batch.CrcMatches, batch.BaseTimestamp, batch.MaxTimestamp));
        KafkaReferenceRecord record = batch.Records.Single();
        Assert.AreEqual((-3L, "k", null), (record.TimestampDelta, System.Text.Encoding.UTF8.GetString(record.Key!), record.Value));
        Assert.AreEqual(("h", "g", "v"), (record.Headers[0].Key, record.Headers[1].Key, System.Text.Encoding.UTF8.GetString(record.Headers[1].Value!)));
        Assert.IsNull(record.Headers[0].Value);
    }

    [TestMethod]
    public void Crc32C_CheckString_IsTheCastagnoliCheckValue() =>
        Assert.AreEqual(0xE3069283u, KafkaReferenceCrc32C.Compute("123456789"u8));

    [TestMethod]
    public void NullableBytes_LengthMinusOne_IsNull() =>
        Assert.IsNull(new KafkaRecordsReader(new KafkaRecordsWriter().NullableBytes(null).ToArray()).NullableBytes());

    [TestMethod]
    public void VarLong_LargeNegativeValue_RoundTrips() =>
        Assert.AreEqual(long.MinValue, new KafkaRecordsReader(new KafkaRecordsWriter().VarLong(long.MinValue).ToArray()).VarLong());
}
