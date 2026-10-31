using System.Text;

namespace Curl.Extension.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>
/// Writes the two requests SASL PLAIN authentication sends (ADR-1082), each with its <c>INT32</c> size prefix and request
/// header v1: SaslHandshake v1 naming the mechanism, and SaslAuthenticate v0 carrying the RFC 4616 message, the last
/// versions of each before flexible versions.
/// </summary>
internal static class KafkaSaslRequests
{
    /// <summary>The SaslHandshake API key.</summary>
    internal const short HandshakeApiKey = 17;

    /// <summary>The SaslAuthenticate API key.</summary>
    internal const short AuthenticateApiKey = 36;

    /// <summary>The SaslHandshake version sent: v1, after which SaslAuthenticate carries the mechanism's bytes.</summary>
    internal const short HandshakeVersion = 1;

    /// <summary>The SaslAuthenticate version sent.</summary>
    internal const short AuthenticateVersion = 0;

    /// <summary>The one mechanism the extension offers.</summary>
    internal const string PlainMechanism = "PLAIN";

    /// <summary>Gives SaslHandshake v1 naming <see cref="PlainMechanism"/>.</summary>
    internal static byte[] Handshake(int correlationId) =>
        Framed(Header(HandshakeApiKey, HandshakeVersion, correlationId).String(PlainMechanism));

    /// <summary>Gives SaslAuthenticate v0 carrying <see cref="PlainMessage"/> for <paramref name="userName"/> and <paramref name="password"/>.</summary>
    internal static byte[] Authenticate(int correlationId, string userName, string password) =>
        Framed(Header(AuthenticateApiKey, AuthenticateVersion, correlationId).Bytes(PlainMessage(userName, password)));

    /// <summary>
    /// Gives RFC 4616's PLAIN message in UTF-8: an empty authorization identity, NUL, the authentication identity
    /// <paramref name="userName"/>, NUL and <paramref name="password"/>.
    /// </summary>
    internal static byte[] PlainMessage(string userName, string password) => Encoding.UTF8.GetBytes($"\0{userName}\0{password}");

    private static KafkaWriter Header(short apiKey, short apiVersion, int correlationId) =>
        new KafkaWriter().Int16(apiKey).Int16(apiVersion).Int32(correlationId).String(KafkaRequests.ClientId);

    private static byte[] Framed(KafkaWriter request)
    {
        byte[] body = request.ToArray();
        return new KafkaWriter().Int32(body.Length).Raw(body).ToArray();
    }
}
