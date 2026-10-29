namespace Curl.Extension.Kafka.Wire;

/// <summary>Builds the broker's side of the protocol guide's exchanges: record batches and framed responses.</summary>
internal static class KafkaTestBytes
{
    /// <summary>Gives a record ("Record") with no key and no headers.</summary>
    public static byte[] Record(int offsetDelta, byte[]? value)
    {
        KafkaWriter body = new KafkaWriter().Int8(0).VarLong(0).VarInt(offsetDelta).VarInt(-1);
        body = value is null ? body.VarInt(-1) : body.VarInt(value.Length).Raw(value);
        byte[] bytes = body.VarInt(0).ToArray();
        return new KafkaWriter().VarInt(bytes.Length).Raw(bytes).ToArray();
    }

    /// <summary>Gives a record batch ("Record Batch") holding <paramref name="records"/> with a correct CRC-32C.</summary>
    public static byte[] Batch(long baseOffset, int lastOffsetDelta, int count, byte[] records, short attributes = 0, sbyte magic = KafkaRecordBatch.Magic)
    {
        byte[] covered = new KafkaWriter().Int16(attributes).Int32(lastOffsetDelta).Int64(0).Int64(0).Int64(-1).Int16(-1).Int32(-1).Int32(count).Raw(records).ToArray();
        return new KafkaWriter().Int64(baseOffset).Int32(9 + covered.Length).Int32(0).Int8(magic).UInt32(Crc32C.Compute(covered)).Raw(covered).ToArray();
    }

    /// <summary>Gives a framed Produce v3 response.</summary>
    public static byte[] ProduceResponse(int correlationId, short errorCode, long baseOffset = 0) =>
        Framed(new KafkaWriter().Int32(correlationId).Int32(1).String("t").Int32(1).Int32(0).Int16(errorCode).Int64(baseOffset).Int64(-1).Int32(0));

    /// <summary>Gives a framed Fetch v4 response for one partition.</summary>
    public static byte[] FetchResponse(int correlationId, short errorCode, long highWatermark, byte[]? records, int abortedTransactions = 0)
    {
        KafkaWriter body = new KafkaWriter().Int32(correlationId).Int32(0).Int32(1).String("t").Int32(1).Int32(0).Int16(errorCode).Int64(highWatermark).Int64(highWatermark).Int32(abortedTransactions);
        for (int index = 0; index < abortedTransactions; index++)
        {
            body.Int64(1).Int64(2);
        }

        return Framed(records is null ? body.Int32(-1) : body.Bytes(records));
    }

    /// <summary>
    /// Gives a framed Metadata v4 response for one topic: <paramref name="brokers"/> by node id, host and port, and
    /// <paramref name="partitions"/> by error code, index and leader id, each with one replica and one in-sync replica.
    /// </summary>
    public static byte[] MetadataResponse(int correlationId, short topicErrorCode, (int NodeId, string Host, int Port)[] brokers, params (short ErrorCode, int Index, int LeaderId)[] partitions)
    {
        KafkaWriter body = new KafkaWriter().Int32(correlationId).Int32(0).Int32(brokers.Length);
        foreach ((int nodeId, string host, int port) in brokers)
        {
            body.Int32(nodeId).String(host).Int32(port).NullString();
        }

        body.String("cluster").Int32(0).Int32(1).Int16(topicErrorCode).String("t").Int8(0).Int32(partitions.Length);
        foreach ((short errorCode, int index, int leaderId) in partitions)
        {
            body.Int16(errorCode).Int32(index).Int32(leaderId).Int32(1).Int32(leaderId).Int32(1).Int32(leaderId);
        }

        return Framed(body);
    }

    /// <summary>Gives a framed Metadata v4 response naming <paramref name="host"/>:<paramref name="port"/> as the leader of partitions 0 to 2.</summary>
    public static byte[] MetadataLeader(int correlationId, string host, int port) =>
        MetadataResponse(correlationId, 0, [(1, host, port)], (0, 0, 1), (0, 1, 1), (0, 2, 1));

    /// <summary>Gives <paramref name="body"/> with its <c>INT32</c> size prefix.</summary>
    public static byte[] Framed(KafkaWriter body)
    {
        byte[] bytes = body.ToArray();
        return new KafkaWriter().Int32(bytes.Length).Raw(bytes).ToArray();
    }

    /// <summary>Gives the bytes after a framed response's size and correlation id.</summary>
    public static ReadOnlyMemory<byte> BodyOf(byte[] framed) => framed.AsMemory(8);
}
