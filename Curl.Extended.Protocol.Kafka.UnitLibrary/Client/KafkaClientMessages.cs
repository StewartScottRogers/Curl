namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>The error messages a Kafka transfer ends with (ADR-1076).</summary>
internal static class KafkaClientMessages
{
    /// <summary>The URL names no topic.</summary>
    internal const string NoTopic = "No Kafka topic found in the URL";

    /// <summary>The URL's path has more than a topic and a partition.</summary>
    internal const string PathTooLong = "Kafka URL path is /topic or /topic/partition";

    /// <summary>The URL's partition is not a number from 0 up.</summary>
    internal const string BadPartition = "Kafka partition in the URL is not a number";

    /// <summary>The URL's query is not <c>offset=</c> and a number from 0 up.</summary>
    internal const string BadQuery = "Kafka URL query is offset=<number>";

    /// <summary>The connection ended where a response was due.</summary>
    internal const string Disconnected = "Connection disconnected";

    private static readonly Dictionary<short, string> ErrorNames = new()
    {
        [-1] = "UNKNOWN_SERVER_ERROR",
        [1] = "OFFSET_OUT_OF_RANGE",
        [2] = "CORRUPT_MESSAGE",
        [3] = "UNKNOWN_TOPIC_OR_PARTITION",
        [5] = "LEADER_NOT_AVAILABLE",
        [6] = "NOT_LEADER_OR_FOLLOWER",
        [7] = "REQUEST_TIMED_OUT",
        [10] = "MESSAGE_TOO_LARGE",
        [19] = "NOT_ENOUGH_REPLICAS",
        [29] = "TOPIC_AUTHORIZATION_FAILED",
        [87] = "INVALID_RECORD",
    };

    /// <summary>Sending the <paramref name="api"/> request failed.</summary>
    internal static string SendFailed(string api) => $"Failed sending Kafka {api} request";

    /// <summary>The broker answered <paramref name="errorCode"/> for the partition.</summary>
    internal static string BrokerError(short errorCode, string topic, int partition) =>
        $"Kafka broker answered error {errorCode} ({ErrorNames.GetValueOrDefault(errorCode, "UNNAMED")}) for topic {topic} partition {partition}";

    /// <summary>Writing <paramref name="length"/> bytes to the output failed.</summary>
    internal static string OutputWriteFailed(int length) => $"Failure writing output to destination, passed {length} returned 0";
}
