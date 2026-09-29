namespace Curl.Tls;

/// <summary>What <see cref="Tls13TestServer" /> sends for one ClientHello: the ServerHello (or HelloRetryRequest) at the Initial level, and the messages that follow at the Handshake level.</summary>
internal sealed record TestServerFlight(byte[] ServerHello, List<byte[]> HandshakeMessages)
{
    public bool IsHelloRetryRequest => HandshakeMessages.Count == 0;

    public byte[] Handshake => [.. HandshakeMessages.SelectMany(message => message)];
}
