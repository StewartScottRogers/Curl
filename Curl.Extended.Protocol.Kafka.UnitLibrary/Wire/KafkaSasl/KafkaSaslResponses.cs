namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>Reads the SaslHandshake v1 and SaslAuthenticate v0 response bodies (after response header v0's <c>correlation_id</c>).</summary>
internal static class KafkaSaslResponses
{
    /// <summary>Reads SaslHandshake v1: its <c>error_code</c> and the <c>mechanisms</c> the broker enables.</summary>
    internal static KafkaSaslHandshakeResponse ReadHandshake(ReadOnlyMemory<byte> body)
    {
        KafkaReader reader = new(body);
        short errorCode = reader.Int16();
        int count = reader.Int32();
        List<string> mechanisms = [];
        for (int index = 0; index < count; index++)
        {
            mechanisms.Add(reader.NullableString() ?? string.Empty);
        }

        return new KafkaSaslHandshakeResponse(errorCode, mechanisms);
    }

    /// <summary>Reads SaslAuthenticate v0: its <c>error_code</c> and <c>error_message</c>.</summary>
    internal static KafkaSaslAuthenticateResponse ReadAuthenticate(ReadOnlyMemory<byte> body)
    {
        KafkaReader reader = new(body);
        short errorCode = reader.Int16();
        string? errorMessage = reader.NullableString();
        reader.NullableBytes();
        return new KafkaSaslAuthenticateResponse(errorCode, errorMessage);
    }
}
