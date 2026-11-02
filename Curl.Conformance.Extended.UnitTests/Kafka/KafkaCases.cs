using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Kafka;

/// <summary>What the Kafka cases share: the curl entry point, the known failures and a one-broker cluster.</summary>
internal static class KafkaCases
{
    internal const int Port = 9092;

    internal const string Topic = "orders";

    internal static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    internal static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    internal static readonly KafkaReferenceNode ThisBroker = new(1, "localhost", Port);

    /// <summary>Metadata for a cluster whose partition 0 of <see cref="Topic"/> is led by <paramref name="leaderId"/>.</summary>
    internal static KafkaMetadataApi Metadata(int leaderId, params KafkaReferenceNode[] others) =>
        new([ThisBroker, .. others], [new KafkaReferenceTopic(Topic, 0, [new KafkaReferencePartition(0, 0, leaderId)])]);

    internal static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
