using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>The <c>kafka://</c> and <c>kafkas://</c> extension's description and handler factory (ADR-1076).</summary>
public static class KafkaExtension
{
    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "kafka",
        "Produces and consumes Kafka records: kafka://host/topic[/partition] with -d produces one record whose value is the data, otherwise it consumes the partition from ?offset=N (default 0) to its end and writes each record value and a newline; kafkas:// adds TLS.",
        ["kafka", "kafkas"],
        [],
        new Uri("https://kafka.apache.org/protocol"));

    /// <summary>Creates the handler over curl's connector.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new KafkaProtocolHandler(services.Connector);
    }
}
