namespace Curl.Extension.Kafka.Wire.KafkaSasl;

/// <summary>Builds the broker's side of the protocol guide's SaslHandshake and SaslAuthenticate exchanges.</summary>
internal static class KafkaSaslTestBytes
{
    /// <summary>Gives a framed SaslHandshake v1 response enabling <paramref name="mechanisms"/>.</summary>
    public static byte[] HandshakeResponse(int correlationId, short errorCode, params string[] mechanisms)
    {
        KafkaWriter body = new KafkaWriter().Int32(correlationId).Int16(errorCode).Int32(mechanisms.Length);
        foreach (string mechanism in mechanisms)
        {
            body.String(mechanism);
        }

        return KafkaTestBytes.Framed(body);
    }

    /// <summary>Gives a framed SaslAuthenticate v0 response with an empty <c>auth_bytes</c>.</summary>
    public static byte[] AuthenticateResponse(int correlationId, short errorCode, string? errorMessage = null)
    {
        KafkaWriter body = new KafkaWriter().Int32(correlationId).Int16(errorCode);
        body = errorMessage is null ? body.NullString() : body.String(errorMessage);
        return KafkaTestBytes.Framed(body.Bytes([]));
    }
}
