namespace Curl.Tls;

/// <summary>Connects <see cref="Tls13ClientConnection" /> to <see cref="Tls13RecordTestServer" /> over an <see cref="InMemoryPipe" />.</summary>
internal static class Tls13PipeDriver
{
    public static readonly Tls13ClientSettings DefaultSettings = new() { ServerName = "localhost" };

    /// <summary>Runs a whole handshake on both ends and returns the connected client, the server and the server's end of the pipe.</summary>
    public static async Task<(Tls13ClientStream Client, Tls13RecordTestServer Server, Stream ServerEnd)> ConnectAsync(
        Tls13TestServer? testServer = null,
        Tls13ClientSettings? settings = null,
        bool serverSendsChangeCipherSpec = false)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, testServer ?? new Tls13TestServer(TestServerCredential.Ed25519()))
        {
            SendChangeCipherSpec = serverSendsChangeCipherSpec,
        };
        Task serverHandshake = server.HandshakeAsync();
        Tls13ConnectResult result = await Tls13ClientConnection.ConnectAsync(
            clientEnd, settings ?? DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None);
        await serverHandshake;
        Assert.IsNull(result.Failure);
        return (result.Stream!, server, serverEnd);
    }

    /// <summary>
    /// Starts a handshake whose server end the test runs by hand, and returns the client's
    /// pending result with the server and its end of the pipe.
    /// </summary>
    public static (Task<Tls13ConnectResult> Result, Tls13RecordTestServer Server, Stream ServerEnd) Start(
        Tls13TestServer? testServer = null,
        Tls13ClientSettings? settings = null,
        IServerCertificateVerifier? verifier = null)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls13RecordTestServer server = new(serverEnd, testServer ?? new Tls13TestServer(TestServerCredential.Ed25519()));
        Task<Tls13ConnectResult> result = Tls13ClientConnection.ConnectAsync(
            clientEnd, settings ?? DefaultSettings, SystemTlsRandomSource.Instance, verifier ?? new RecordingCertificateVerifier(), CancellationToken.None);
        return (result, server, serverEnd);
    }

    /// <summary>Reads application data from the client stream until <paramref name="length" /> bytes have arrived.</summary>
    public static async Task<byte[]> ReadAsync(Stream stream, int length)
    {
        byte[] received = new byte[length];
        await stream.ReadExactlyAsync(received);
        return received;
    }

    /// <summary>Reads the alert the client sent and returns its description, checking it is fatal.</summary>
    public static async Task<TlsAlertDescription> ReceiveFatalAlertAsync(Tls13RecordTestServer server)
    {
        Tls13RecordContent alert = (await server.ReceiveAsync())!;
        Assert.AreEqual(TlsContentType.Alert, alert.Type);
        Assert.HasCount(2, alert.Content);
        Assert.AreEqual(2, alert.Content[0]);
        return (TlsAlertDescription)alert.Content[1];
    }
}
