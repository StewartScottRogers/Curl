using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Kafka.Client;

/// <summary>
/// What a <c>kafka://host[:port]/topic[/partition][?offset=N]</c> URL names (ADR-1076): the URL-decoded topic, the
/// partition (0 when the path names none) and the offset a consume starts at (0 when the query names none).
/// </summary>
internal sealed record KafkaAddress(string Topic, int Partition, long Offset)
{
    private const string OffsetKey = "offset=";

    /// <summary>Reads the address from <paramref name="url"/>, or throws an exit 3 <see cref="KafkaClientException"/>.</summary>
    internal static KafkaAddress Parse(CurlUrl url)
    {
        string[] segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length switch
        {
            0 => throw Malformed(KafkaClientMessages.NoTopic),
            > 2 => throw Malformed(KafkaClientMessages.PathTooLong),
            _ => new KafkaAddress(Uri.UnescapeDataString(segments[0]), PartitionOf(segments), OffsetOf(url.Query)),
        };
    }

    private static int PartitionOf(string[] segments) =>
        segments.Length == 1 ? 0
        : int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out int partition) ? partition
        : throw Malformed(KafkaClientMessages.BadPartition);

    private static long OffsetOf(string? query) =>
        string.IsNullOrEmpty(query) ? 0
        : query.StartsWith(OffsetKey, StringComparison.Ordinal)
            && long.TryParse(query.AsSpan(OffsetKey.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long offset) ? offset
        : throw Malformed(KafkaClientMessages.BadQuery);

    private static KafkaClientException Malformed(string message) => new(CurlExitCode.UrlMalformat, message);
}
