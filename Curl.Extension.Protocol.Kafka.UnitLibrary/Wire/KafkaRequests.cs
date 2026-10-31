namespace Curl.Extension.Protocol.Kafka.Wire;

/// <summary>
/// Writes the three requests the extension sends, each with its <c>INT32</c> size prefix ("Common Request and Response
/// Structure") and request header v1 (<c>request_api_key</c>, <c>request_api_version</c>, <c>correlation_id</c>,
/// <c>client_id</c>): Metadata v4, Produce v3 and Fetch v4, the oldest versions Kafka 4.0 still accepts and the last
/// before flexible versions.
/// </summary>
internal static class KafkaRequests
{
    /// <summary>The Produce API key.</summary>
    internal const short ProduceApiKey = 0;

    /// <summary>The Fetch API key.</summary>
    internal const short FetchApiKey = 1;

    /// <summary>The Metadata API key.</summary>
    internal const short MetadataApiKey = 3;

    /// <summary>The Produce version sent.</summary>
    internal const short ProduceVersion = 3;

    /// <summary>The Fetch version sent.</summary>
    internal const short FetchVersion = 4;

    /// <summary>The Metadata version sent.</summary>
    internal const short MetadataVersion = 4;

    /// <summary>The <c>client_id</c> every request names.</summary>
    internal const string ClientId = "curl";

    /// <summary>The <c>acks</c> Produce asks for: -1, every in-sync replica.</summary>
    internal const short AllReplicas = -1;

    /// <summary>How long the broker may take over a Produce.</summary>
    internal const int ProduceTimeoutMilliseconds = 30000;

    /// <summary>How long the broker may wait for <see cref="FetchMinBytes"/> before it answers a Fetch.</summary>
    internal const int FetchMaxWaitMilliseconds = 500;

    /// <summary>The bytes a Fetch waits for.</summary>
    internal const int FetchMinBytes = 1;

    /// <summary>The most bytes a Fetch asks for, for the response and for the one partition.</summary>
    internal const int FetchMaxBytes = 1024 * 1024;

    /// <summary>Gives Produce v3 for one record batch to one partition.</summary>
    internal static byte[] Produce(int correlationId, string topic, int partition, byte[] batch) =>
        Framed(Header(ProduceApiKey, ProduceVersion, correlationId)
            .NullString()
            .Int16(AllReplicas)
            .Int32(ProduceTimeoutMilliseconds)
            .Int32(1)
            .String(topic)
            .Int32(1)
            .Int32(partition)
            .Bytes(batch));

    /// <summary>Gives Fetch v4 for one partition from <paramref name="offset"/>, reading uncommitted records.</summary>
    internal static byte[] Fetch(int correlationId, string topic, int partition, long offset) =>
        Framed(Header(FetchApiKey, FetchVersion, correlationId)
            .Int32(-1)
            .Int32(FetchMaxWaitMilliseconds)
            .Int32(FetchMinBytes)
            .Int32(FetchMaxBytes)
            .Int8(0)
            .Int32(1)
            .String(topic)
            .Int32(1)
            .Int32(partition)
            .Int64(offset)
            .Int32(FetchMaxBytes));

    /// <summary>Gives Metadata v4 for one topic, with <c>allow_auto_topic_creation</c> false.</summary>
    internal static byte[] Metadata(int correlationId, string topic) =>
        Framed(Header(MetadataApiKey, MetadataVersion, correlationId)
            .Int32(1)
            .String(topic)
            .Int8(0));

    private static KafkaWriter Header(short apiKey, short apiVersion, int correlationId) =>
        new KafkaWriter().Int16(apiKey).Int16(apiVersion).Int32(correlationId).String(ClientId);

    private static byte[] Framed(KafkaWriter request)
    {
        byte[] body = request.ToArray();
        return new KafkaWriter().Int32(body.Length).Raw(body).ToArray();
    }
}
