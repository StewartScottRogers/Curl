namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>
/// Fetch v4 (api_key 1): reads the request, keeps it for the case to check, and answers the one partition with its
/// high watermark, a last stable offset equal to it, <paramref name="abortedTransactions"/> and the log's batches from
/// the fetch offset, cut at <paramref name="responseMaxBytes"/> so the last may be partial. A fetch offset past the
/// high watermark is answered OFFSET_OUT_OF_RANGE with null records.
/// </summary>
/// <param name="log">The partition's log.</param>
/// <param name="responseMaxBytes">The most record bytes one response carries.</param>
/// <param name="abortedTransactions">The aborted transactions to list, each a producer id and first offset.</param>
public sealed class KafkaFetchApi(KafkaReferenceLog log, int responseMaxBytes = int.MaxValue, params IReadOnlyList<KeyValuePair<long, long>> abortedTransactions) : IKafkaReferenceApi
{
    /// <summary>The error code OFFSET_OUT_OF_RANGE.</summary>
    public const short OffsetOutOfRange = 1;

    private readonly List<KafkaReferenceFetch> fetches = [];

    /// <summary>Gets every Fetch request read, in order.</summary>
    public IReadOnlyList<KafkaReferenceFetch> Fetches => fetches;

    /// <inheritdoc/>
    public short ApiKey => 1;

    /// <inheritdoc/>
    public byte[] Answer(KafkaReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        KafkaRecordsReader reader = new(request.Body);
        int replicaId = reader.Int32();
        int maxWait = reader.Int32();
        int minBytes = reader.Int32();
        int maxBytes = reader.Int32();
        sbyte isolation = reader.Int8();
        reader.Int32();
        string topic = reader.NullableString()!;
        reader.Int32();
        KafkaReferenceFetch fetch = new(replicaId, maxWait, minBytes, maxBytes, isolation, topic, reader.Int32(), reader.Int64(), reader.Int32());
        fetches.Add(fetch);
        bool inRange = fetch.FetchOffset >= 0 && fetch.FetchOffset <= log.HighWatermark;
        KafkaRecordsWriter writer = new KafkaRecordsWriter().Int32(0).Int32(1).String(topic).Int32(1)
            .Int32(fetch.Partition).Int16(inRange ? (short)0 : OffsetOutOfRange).Int64(log.HighWatermark).Int64(log.HighWatermark)
            .Int32(abortedTransactions.Count);
        foreach (KeyValuePair<long, long> aborted in abortedTransactions)
        {
            writer.Int64(aborted.Key).Int64(aborted.Value);
        }

        return writer.NullableBytes(inRange ? log.ReadFrom(fetch.FetchOffset, responseMaxBytes) : null).ToArray();
    }
}
