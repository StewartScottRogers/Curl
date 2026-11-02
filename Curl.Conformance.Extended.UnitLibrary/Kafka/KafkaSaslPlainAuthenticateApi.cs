using System.Text;

namespace Curl.Conformance.Extended.Kafka;

/// <summary>
/// SaslAuthenticate v0 (api_key 36) for PLAIN: reads the RFC 4616 message, <c>[authzid] NUL authcid NUL passwd</c>,
/// and accepts it when the authentication identity and password are the broker's; otherwise it answers
/// SASL_AUTHENTICATION_FAILED with <see cref="FailureMessage"/>.
/// </summary>
/// <param name="userName">The accepted authentication identity.</param>
/// <param name="password">The accepted password.</param>
public sealed class KafkaSaslPlainAuthenticateApi(string userName, string password) : IKafkaReferenceApi
{
    /// <summary>The error code SASL_AUTHENTICATION_FAILED.</summary>
    public const short SaslAuthenticationFailed = 58;

    /// <summary>The error_message of a refused authentication.</summary>
    public const string FailureMessage = "Authentication failed: Invalid username or password";

    /// <inheritdoc/>
    public short ApiKey => 36;

    /// <inheritdoc/>
    public byte[] Answer(KafkaReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string[] fields = Encoding.UTF8.GetString(new KafkaWireReader(request.Body).Bytes()).Split('\0');
        bool accepted = fields.Length == 3 && fields[1] == userName && fields[2] == password;
        return new KafkaWireWriter()
            .Int16(accepted ? (short)0 : SaslAuthenticationFailed)
            .String(accepted ? null : FailureMessage)
            .Bytes([])
            .ToArray();
    }
}
