namespace Curl.Conformance.Extended.Kafka;

/// <summary>
/// One API the reference broker serves. Each API is its own type, so later parts (Produce and Fetch in
/// <c>Kafka/Records/</c>, compression in <c>Kafka/Compression/</c>) add APIs without editing the broker.
/// </summary>
public interface IKafkaReferenceApi
{
    /// <summary>Gets the api_key this API answers.</summary>
    short ApiKey { get; }

    /// <summary>Answers one request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The response body, after the response header's correlation id.</returns>
    byte[] Answer(KafkaReferenceRequest request);
}
