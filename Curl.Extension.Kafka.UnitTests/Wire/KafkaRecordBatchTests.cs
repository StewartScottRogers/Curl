using System.IO.Compression;
using System.Text;

namespace Curl.Extension.Kafka.Wire;

/// <summary>Checks record batches against the protocol guide's "Messages": "Record Batch", "Record" and "Control Batches".</summary>
[TestClass]
public sealed class KafkaRecordBatchTests
{
    [TestMethod]
    public void Encode_WritesAMagic2BatchOfOneRecordWithItsCrcAndLength()
    {
        byte[] batch = KafkaRecordBatch.Encode("hi"u8, 0x1234);

        KafkaReader reader = new(batch);
        Assert.AreEqual(0L, reader.Int64());
        Assert.AreEqual(batch.Length - 12, reader.Int32());
        Assert.AreEqual(-1, reader.Int32());
        Assert.AreEqual((sbyte)2, reader.Int8());
        Assert.AreEqual(Crc32C.Compute(batch.AsSpan(21)), reader.UInt32());
        Assert.AreEqual((short)0, reader.Int16());
        Assert.AreEqual(0, reader.Int32());
        Assert.AreEqual(0x1234L, reader.Int64());
        Assert.AreEqual(0x1234L, reader.Int64());
        Assert.AreEqual(-1L, reader.Int64());
        Assert.AreEqual((short)-1, reader.Int16());
        Assert.AreEqual(-1, reader.Int32());
        Assert.AreEqual(1, reader.Int32());
        CollectionAssert.AreEqual(KafkaTestBytes.Record(0, "hi"u8.ToArray()), reader.Take(reader.Remaining).ToArray());
    }

    [TestMethod]
    public void Decode_EncodedBatch_GivesItsRecordAndTheNextOffset()
    {
        KafkaRecordSet set = KafkaRecordBatch.Decode(KafkaRecordBatch.Encode("hi"u8, 1), 0);

        Assert.AreEqual(1, set.Records.Count);
        Assert.AreEqual(0L, set.Records[0].Offset);
        Assert.AreEqual("hi", Encoding.UTF8.GetString(set.Records[0].Value.Span));
        Assert.AreEqual(1L, set.NextOffset);
    }

    [TestMethod]
    public void Decode_KeepsRecordsAtOrAfterTheOffsetAndGivesNullValuesAsEmpty()
    {
        byte[] records = [.. KafkaTestBytes.Record(0, "a"u8.ToArray()), .. KafkaTestBytes.Record(1, null), .. KafkaTestBytes.Record(2, "c"u8.ToArray())];
        byte[] fetched = [.. KafkaTestBytes.Batch(10, 2, 3, records), .. KafkaTestBytes.Batch(13, 0, 1, KafkaTestBytes.Record(0, "d"u8.ToArray()))];

        KafkaRecordSet set = KafkaRecordBatch.Decode(fetched, 11);

        CollectionAssert.AreEqual(new[] { 11L, 12L, 13L }, set.Records.Select(record => record.Offset).ToArray());
        CollectionAssert.AreEqual(new[] { "", "c", "d" }, set.Records.Select(record => Encoding.UTF8.GetString(record.Value.Span)).ToArray());
        Assert.AreEqual(14L, set.NextOffset);
    }

    [TestMethod]
    public void Decode_PartialBatchAtTheEnd_IsIgnored()
    {
        byte[] whole = KafkaTestBytes.Batch(0, 0, 1, KafkaTestBytes.Record(0, "a"u8.ToArray()));
        byte[] fetched = [.. whole, .. whole.AsSpan(0, whole.Length - 1), 0];

        KafkaRecordSet set = KafkaRecordBatch.Decode(fetched[..^1], 0);

        Assert.AreEqual(1, set.Records.Count);
        Assert.AreEqual(1L, set.NextOffset);
    }

    [TestMethod]
    public void Decode_ControlBatch_IsSkippedButMovesTheNextOffset()
    {
        KafkaRecordSet set = KafkaRecordBatch.Decode(KafkaTestBytes.Batch(5, 0, 1, KafkaTestBytes.Record(0, [0, 0]), KafkaRecordBatch.ControlFlag), 5);

        Assert.AreEqual(0, set.Records.Count);
        Assert.AreEqual(6L, set.NextOffset);
    }

    [TestMethod]
    public void Decode_EmptyRecords_GivesNothingAndTheSameOffset()
    {
        KafkaRecordSet set = KafkaRecordBatch.Decode(ReadOnlyMemory<byte>.Empty, 7);

        Assert.AreEqual(0, set.Records.Count);
        Assert.AreEqual(7L, set.NextOffset);
    }

    [TestMethod]
    public void Decode_GzipBatch_ReadsItsRecords()
    {
        using MemoryStream compressed = new();
        using (GZipStream gzip = new(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(KafkaTestBytes.Record(0, "z"u8.ToArray()));
        }

        KafkaRecordSet set = KafkaRecordBatch.Decode(KafkaTestBytes.Batch(0, 0, 1, compressed.ToArray(), KafkaRecordBatch.GzipCodec), 0);

        Assert.AreEqual("z", Encoding.UTF8.GetString(set.Records.Single().Value.Span));
    }

    [TestMethod]
    [DataRow(KafkaRecordBatch.SnappyCodec)]
    [DataRow(KafkaRecordBatch.Lz4Codec)]
    [DataRow(KafkaRecordBatch.ZstdCodec)]
    public void Decode_SnappyLz4OrZstdBatch_ReadsItsRecords(int codec)
    {
        byte[] records = [.. KafkaTestBytes.Record(0, "first"u8.ToArray()), .. KafkaTestBytes.Record(1, "second"u8.ToArray())];

        KafkaRecordSet set = KafkaRecordBatch.Decode(KafkaTestBytes.Batch(0, 1, 2, Compressed(codec, records), (short)codec), 0);

        CollectionAssert.AreEqual(new[] { "first", "second" }, set.Records.Select(record => Encoding.UTF8.GetString(record.Value.Span)).ToArray());
    }

    [TestMethod]
    [DataRow((short)KafkaRecordBatch.GzipCodec, "Kafka record batch holds bad gzip data")]
    [DataRow((short)KafkaRecordBatch.SnappyCodec, "Kafka record batch holds bad snappy data")]
    [DataRow((short)KafkaRecordBatch.Lz4Codec, "Kafka record batch holds bad lz4 data")]
    [DataRow((short)KafkaRecordBatch.ZstdCodec, "Kafka record batch holds bad zstd data")]
    [DataRow((short)5, "Kafka record batch uses compression codec 5, which the kafka extension does not read")]
    public void Decode_UnreadableCompression_Throws(short attributes, string message) =>
        Assert.AreEqual(message, Assert.ThrowsExactly<FormatException>(() => KafkaRecordBatch.Decode(KafkaTestBytes.Batch(0, 0, 1, [1, 2, 3], attributes), 0)).Message);

    [TestMethod]
    public void Decode_OtherMagic_Throws() =>
        Assert.AreEqual(
            "Kafka record batch has magic 1, not 2",
            Assert.ThrowsExactly<FormatException>(() => KafkaRecordBatch.Decode(KafkaTestBytes.Batch(0, 0, 0, [], magic: 1), 0)).Message);

    [TestMethod]
    public void Decode_CorruptBatch_FailsItsCrcCheck()
    {
        byte[] batch = KafkaTestBytes.Batch(0, 0, 1, KafkaTestBytes.Record(0, "a"u8.ToArray()));
        batch[^2] ^= 0xFF;

        Assert.AreEqual("Kafka record batch fails its CRC-32C check", Assert.ThrowsExactly<FormatException>(() => KafkaRecordBatch.Decode(batch, 0)).Message);
    }

    [TestMethod]
    public void Decode_MoreRecordsCountedThanHeld_ThrowsTruncated() =>
        Assert.AreEqual(KafkaReader.Truncated, Assert.ThrowsExactly<FormatException>(() => KafkaRecordBatch.Decode(KafkaTestBytes.Batch(0, 1, 2, KafkaTestBytes.Record(0, [1])), 0)).Message);

    /// <summary>
    /// Hand-assembles the smallest stream each codec's format allows: a raw snappy stream of one literal, an LZ4 frame
    /// of one stored block, and a single-segment zstd frame of one raw block.
    /// </summary>
    private static byte[] Compressed(int codec, byte[] plain) => codec switch
    {
        KafkaRecordBatch.SnappyCodec => [(byte)plain.Length, (byte)((plain.Length - 1) << 2), .. plain],
        KafkaRecordBatch.Lz4Codec => [0x04, 0x22, 0x4D, 0x18, 0x60, 0x40, 0x00, .. BitConverter.GetBytes(plain.Length | int.MinValue), .. plain, 0, 0, 0, 0],
        _ => [0x28, 0xB5, 0x2F, 0xFD, 0x20, (byte)plain.Length, (byte)((plain.Length << 3) | 1), (byte)(plain.Length >> 5), 0, .. plain],
    };
}
