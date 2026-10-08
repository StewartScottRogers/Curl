using Curl.Testing;

namespace Curl.Tls;

/// <summary>Runs <see cref="Tls12ClientHandshake" /> against <see cref="Tls12TestServer" />, with hooks to replace what the server sends.</summary>
internal static class Tls12HandshakeDriver
{
    public static readonly Tls12ClientSettings DefaultSettings = new() { ServerName = "localhost" };

    /// <summary>
    /// Runs a whole handshake and returns every message the client sent after its hello,
    /// with its last completion and failure; the server checks the client's flight whenever
    /// the client sends one. Given <paramref name="diagnostics" />, it writes a PHASE line for
    /// each flight.
    /// </summary>
    public static Tls12HandshakeOutput Run(
        Tls12ClientHandshake client,
        Tls12TestServer server,
        Func<List<Tls12OutgoingMessage>, List<Tls12OutgoingMessage>>? replaceFlight = null,
        Func<List<Tls12OutgoingMessage>, List<Tls12OutgoingMessage>>? replaceFinalFlight = null,
        TestDiagnostics? diagnostics = null)
    {
        List<Tls12OutgoingMessage> flight;
        using (diagnostics?.Phase("client hello and the server's first flight"))
        {
            flight = server.Answer(client.Start().MessagesToSend[0].Bytes);
        }

        Tls12HandshakeOutput output;
        using (diagnostics?.Phase("client takes the server's first flight"))
        {
            output = Deliver(client, (replaceFlight ?? (original => original))(flight));
        }

        if (output.Failure is not null || output.MessagesToSend.Count == 0)
        {
            return output;
        }

        List<Tls12OutgoingMessage> finalFlight;
        using (diagnostics?.Phase("server takes the client's flight"))
        {
            finalFlight = server.ReceiveClientFlight(output.MessagesToSend);
        }

        if (output.IsComplete)
        {
            return output;
        }

        Tls12HandshakeOutput final;
        using (diagnostics?.Phase("client takes the server's final flight"))
        {
            final = Deliver(client, (replaceFinalFlight ?? (original => original))(finalFlight));
        }

        return final with { MessagesToSend = [.. output.MessagesToSend, .. final.MessagesToSend] };
    }

    /// <summary>Passes each message to the client as its record type would arrive, until it fails.</summary>
    public static Tls12HandshakeOutput Deliver(Tls12ClientHandshake client, IEnumerable<Tls12OutgoingMessage> messages)
    {
        List<Tls12OutgoingMessage> sent = [];
        Tls12HandshakeOutput output = new([], client.IsComplete, client.Failure);
        foreach (Tls12OutgoingMessage message in messages)
        {
            output = message.ContentType == TlsContentType.ChangeCipherSpec
                ? client.ReceiveChangeCipherSpec(message.Bytes)
                : client.ReceiveHandshake(message.Bytes);
            sent.AddRange(output.MessagesToSend);
            if (output.Failure is not null)
            {
                break;
            }
        }

        return output with { MessagesToSend = sent };
    }

    public static Tls12ClientHandshake Client(Tls12ClientSettings? settings = null, IServerCertificateVerifier? verifier = null) =>
        new(settings ?? DefaultSettings, SystemTlsRandomSource.Instance, verifier ?? new RecordingCertificateVerifier());

    public static List<Tls12OutgoingMessage> Replace(List<Tls12OutgoingMessage> flight, HandshakeType type, byte[] replacement) =>
        [.. flight.Select(message => IsHandshake(message, type) ? new Tls12OutgoingMessage(TlsContentType.Handshake, replacement) : message)];

    public static List<Tls12OutgoingMessage> Remove(List<Tls12OutgoingMessage> flight, HandshakeType type) =>
        [.. flight.Where(message => !IsHandshake(message, type))];

    public static List<Tls12OutgoingMessage> Tamper(List<Tls12OutgoingMessage> flight, HandshakeType type) =>
        [.. flight.Select(message => IsHandshake(message, type) ? new Tls12OutgoingMessage(TlsContentType.Handshake, FlipLastByte(message.Bytes)) : message)];

    public static byte[] Body(Tls12OutgoingMessage message) => HandshakeMessageReader.Read(message.Bytes).Message!.Body;

    public static void AssertFails(TlsAlertDescription alert, Tls12HandshakeOutput output)
    {
        Assert.IsNotNull(output.Failure);
        Assert.AreEqual(alert, output.Failure.Alert);
        Assert.IsFalse(output.IsComplete);
    }

    private static bool IsHandshake(Tls12OutgoingMessage message, HandshakeType type) =>
        message.ContentType == TlsContentType.Handshake && (HandshakeType)message.Bytes[0] == type;

    private static byte[] FlipLastByte(byte[] message)
    {
        byte[] tampered = [.. message];
        tampered[^1] ^= 0x01;
        return tampered;
    }
}
