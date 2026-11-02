namespace Curl.Conformance.Extended.Kafka;

/// <summary>A broker as Metadata lists it.</summary>
/// <param name="NodeId">Its node_id.</param>
/// <param name="Host">Its host.</param>
/// <param name="Port">Its port.</param>
public sealed record KafkaReferenceNode(int NodeId, string Host, int Port);
