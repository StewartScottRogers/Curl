namespace Curl.Extended.Protocol.Kafka.Wire;

/// <summary>
/// Reads the Produce v3 and Fetch v4 response bodies (after response header v0's <c>correlation_id</c>) for the one
/// topic and partition a request named; a response holding other than one topic or partition is malformed.
/// </summary>
internal static class KafkaResponses
{
    /// <summary>Reads Produce v3: the partition's <c>error_code</c> and <c>base_offset</c>.</summary>
    internal static KafkaPartitionResponse ReadProduce(ReadOnlyMemory<byte> body)
    {
        KafkaReader reader = new(body);
        ExpectOne(reader);
        reader.NullableString();
        ExpectOne(reader);
        reader.Int32();
        short errorCode = reader.Int16();
        long baseOffset = reader.Int64();
        return new KafkaPartitionResponse(errorCode, baseOffset, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Reads Fetch v4: the partition's <c>error_code</c>, <c>high_watermark</c> and <c>records</c>.</summary>
    internal static KafkaPartitionResponse ReadFetch(ReadOnlyMemory<byte> body)
    {
        KafkaReader reader = new(body);
        reader.Int32();
        ExpectOne(reader);
        reader.NullableString();
        ExpectOne(reader);
        reader.Int32();
        short errorCode = reader.Int16();
        long highWatermark = reader.Int64();
        reader.Int64();
        for (int aborted = reader.Int32(); aborted > 0; aborted--)
        {
            reader.Int64();
            reader.Int64();
        }

        return new KafkaPartitionResponse(errorCode, highWatermark, reader.NullableBytes() ?? ReadOnlyMemory<byte>.Empty);
    }

    private static void ExpectOne(KafkaReader reader)
    {
        int count = reader.Int32();
        if (count != 1)
        {
            throw new FormatException($"Kafka response holds {count} entries where the request named one");
        }
    }
}
