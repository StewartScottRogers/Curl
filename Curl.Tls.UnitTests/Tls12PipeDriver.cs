namespace Curl.Tls;

/// <summary>Connects <see cref="Tls12ClientConnection" /> to <see cref="Tls12RecordTestServer" /> over an <see cref="InMemoryPipe" />.</summary>
internal static class Tls12PipeDriver
{
    public static readonly Tls12ClientSettings DefaultSettings = new()
    {
        ServerName = "localhost",
        MinimumVersion = TlsProtocolVersion.Tls10,
        CipherSuites = [.. Tls12CipherSuite.All.Select(suite => suite.Code)],
    };

    /// <summary>Runs a whole handshake on both ends and returns the connected client, the server and the server's end of the pipe.</summary>
    public static async Task<(Tls12ClientStream Client, Tls12RecordTestServer Server, Stream ServerEnd)> ConnectAsync(
        Tls12TestServer? testServer = null,
        Tls12ClientSettings? settings = null,
        int handshakeRecordLength = Tls12RecordWriteState.MaximumFragmentLength)
    {
        (Task<Tls12ConnectResult> pending, Tls12RecordTestServer server, Stream serverEnd) = Start(testServer, settings, handshakeRecordLength: handshakeRecordLength);
        await server.HandshakeAsync();
        Tls12ConnectResult result = await pending;
        Assert.IsNull(result.Failure);
        return (result.Stream!, server, serverEnd);
    }

    /// <summary>
    /// Starts a handshake whose server end the test runs by hand, and returns the client's
    /// pending result with the server and its end of the pipe.
    /// </summary>
    public static (Task<Tls12ConnectResult> Result, Tls12RecordTestServer Server, Stream ServerEnd) Start(
        Tls12TestServer? testServer = null,
        Tls12ClientSettings? settings = null,
        IServerCertificateVerifier? verifier = null,
        int handshakeRecordLength = Tls12RecordWriteState.MaximumFragmentLength)
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        Tls12RecordTestServer server = new(serverEnd, testServer ?? new Tls12TestServer(TestServerCredential.Ed25519()))
        {
            HandshakeRecordLength = handshakeRecordLength,
        };
        Task<Tls12ConnectResult> result = Tls12ClientConnection.ConnectAsync(
            clientEnd, settings ?? DefaultSettings, SystemTlsRandomSource.Instance, verifier ?? new RecordingCertificateVerifier(), CancellationToken.None);
        return (result, server, serverEnd);
    }

    /// <summary>Reads the alert the client sent and returns its description, checking it is fatal.</summary>
    public static async Task<TlsAlertDescription> ReceiveFatalAlertAsync(Tls12RecordTestServer server)
    {
        Tls12OutgoingMessage alert = (await server.ReceiveAsync())!;
        Assert.AreEqual(TlsContentType.Alert, alert.ContentType);
        Assert.HasCount(2, alert.Bytes);
        Assert.AreEqual(2, alert.Bytes[0]);
        return (TlsAlertDescription)alert.Bytes[1];
    }
}
