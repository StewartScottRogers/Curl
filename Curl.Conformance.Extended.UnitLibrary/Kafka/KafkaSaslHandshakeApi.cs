namespace Curl.Conformance.Extended.Kafka;

/// <summary>
/// SaslHandshake v1 (api_key 17): answers the enabled mechanisms, with UNSUPPORTED_SASL_MECHANISM when the requested
/// one is not among them.
/// </summary>
/// <param name="mechanisms">The mechanisms the broker enables.</param>
public sealed class KafkaSaslHandshakeApi(params IReadOnlyList<string> mechanisms) : IKafkaReferenceApi
{
    /// <summary>The error code UNSUPPORTED_SASL_MECHANISM.</summary>
    public const short UnsupportedSaslMechanism = 33;

    /// <inheritdoc/>
    public short ApiKey => 17;

    /// <inheritdoc/>
    public byte[] Answer(KafkaReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string mechanism = new KafkaWireReader(request.Body).String();
        KafkaWireWriter writer = new KafkaWireWriter()
            .Int16(mechanisms.Contains(mechanism) ? (short)0 : UnsupportedSaslMechanism)
            .Int32(mechanisms.Count);
        foreach (string enabled in mechanisms)
        {
            writer.String(enabled);
        }

        return writer.ToArray();
    }
}
