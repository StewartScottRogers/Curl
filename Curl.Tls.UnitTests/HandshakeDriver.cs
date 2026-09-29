namespace Curl.Tls;

/// <summary>Runs <see cref="Tls13ClientHandshake" /> against <see cref="Tls13TestServer" />, with hooks to replace what the server sends.</summary>
internal static class HandshakeDriver
{
    public static readonly Tls13ClientSettings DefaultSettings = new() { ServerName = "localhost" };

    /// <summary>Runs a whole handshake and returns the client's last output; the server checks the client's final flight when there is one.</summary>
    public static Tls13HandshakeOutput Run(
        Tls13ClientHandshake client,
        Tls13TestServer server,
        Func<byte[], byte[]>? replaceServerHello = null,
        Func<List<byte[]>, List<byte[]>>? replaceFlight = null)
    {
        TestServerFlight flight = server.Answer(client.Start().BytesToSend[0].Bytes);
        if (flight.IsHelloRetryRequest)
        {
            Tls13HandshakeOutput retry = client.Receive(TlsEncryptionLevel.Initial, flight.ServerHello);
            if (retry.Failure is not null)
            {
                return retry;
            }

            flight = server.Answer(retry.BytesToSend[0].Bytes);
        }

        Tls13HandshakeOutput keys = client.Receive(TlsEncryptionLevel.Initial, (replaceServerHello ?? (hello => hello))(flight.ServerHello));
        if (keys.Failure is not null)
        {
            return keys;
        }

        List<byte[]> messages = (replaceFlight ?? (original => original))(flight.HandshakeMessages);
        Tls13HandshakeOutput finished = client.Receive(TlsEncryptionLevel.Handshake, [.. messages.SelectMany(message => message)]);
        if (finished.IsComplete)
        {
            server.ReceiveClientFlight(finished.BytesToSend[0].Bytes);
        }

        return finished;
    }

    public static Tls13ClientHandshake Client(Tls13ClientSettings? settings = null, IServerCertificateVerifier? verifier = null) =>
        new(settings ?? DefaultSettings, SystemTlsRandomSource.Instance, verifier ?? new RecordingCertificateVerifier());

    public static List<byte[]> Replace(List<byte[]> flight, HandshakeType type, byte[] replacement) =>
        [.. flight.Select(message => (HandshakeType)message[0] == type ? replacement : message)];

    public static List<byte[]> Tamper(List<byte[]> flight, HandshakeType type) =>
        [.. flight.Select(message => (HandshakeType)message[0] == type ? FlipLastByte(message) : message)];

    private static byte[] FlipLastByte(byte[] message)
    {
        byte[] tampered = [.. message];
        tampered[^1] ^= 0x01;
        return tampered;
    }
}
