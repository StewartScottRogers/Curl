namespace Curl.Quic;

/// <summary>Shared set-up for the stream tests: a client whose handshake with the in-memory server is complete, and ways to pass frames between them.</summary>
internal static class QuicStreamTest
{
    /// <summary>Client transport parameters with small limits, so the tests reach them: 100 bytes per stream and on the connection, 2 streams of each type.</summary>
    public static QuicTransportParameters SmallClientLimits { get; } = QuicTransportParameters.CurlClientDefaults with
    {
        InitialMaxData = 100,
        InitialMaxStreamDataBidiLocal = 100,
        InitialMaxStreamDataBidiRemote = 100,
        InitialMaxStreamDataUni = 100,
        InitialMaxStreamsBidi = 2,
        InitialMaxStreamsUni = 2,
    };

    /// <summary>Gives the server generous limits unless a test says otherwise.</summary>
    public static QuicTransportParameters GenerousServerLimits(QuicTransportParameters parameters) => parameters with
    {
        InitialMaxData = 1 << 20,
        InitialMaxStreamDataBidiLocal = 1 << 16,
        InitialMaxStreamDataBidiRemote = 1 << 16,
        InitialMaxStreamDataUni = 1 << 16,
        InitialMaxStreamsBidi = 8,
        InitialMaxStreamsUni = 8,
    };

    /// <summary>Returns a client whose handshake with <paramref name="server" /> has completed and been confirmed.</summary>
    public static QuicClientHandshake Connect(QuicTestServer server, QuicTransportParameters? clientParameters = null)
    {
        QuicClientHandshake client = QuicHandshakeTest.Client(QuicHandshakeTest.CurlSettings with { TransportParameters = clientParameters ?? SmallClientLimits });
        QuicClientHandshakeTests.Run(client, server);
        Assert.IsTrue(client.IsConfirmed);
        return client;
    }

    /// <summary>Sends <paramref name="frames" /> from the server in one 1-RTT packet, then exchanges datagrams until both sides are quiet.</summary>
    public static void Deliver(QuicClientHandshake client, QuicTestServer server, params QuicFrame[] frames) =>
        QuicClientHandshakeTests.Exchange(client, server, client.Receive(server.Protect(QuicPacketType.OneRtt, frames)));

    /// <summary>Sends what the client has queued, then exchanges datagrams until both sides are quiet.</summary>
    public static void Flush(QuicClientHandshake client, QuicTestServer server) =>
        QuicClientHandshakeTests.Exchange(client, server, client.TakeDatagramsToSend());

    /// <summary>Returns the frames of type <typeparamref name="T" /> the client has sent in 1-RTT packets, in order.</summary>
    public static List<T> Sent<T>(QuicTestServer server)
        where T : QuicFrame =>
        [.. server.ClientPackets.Where(packet => packet.Type == QuicPacketType.OneRtt).SelectMany(packet => packet.Frames).OfType<T>()];

    /// <summary>Reads everything a stream holds now.</summary>
    public static byte[] ReadAll(QuicStream stream)
    {
        byte[] buffer = new byte[stream.ReadableLength];
        Assert.AreEqual(buffer.Length, stream.Read(buffer));
        return buffer;
    }

    /// <summary>Returns <paramref name="length" /> bytes counting up from zero.</summary>
    public static byte[] Bytes(int length) => [.. Enumerable.Range(0, length).Select(value => (byte)value)];
}
