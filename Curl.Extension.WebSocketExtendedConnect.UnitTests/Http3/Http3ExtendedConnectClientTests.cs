using Curl.Extension.WebSocketExtendedConnect.Http3;
using Curl.Extension.WebSocketExtendedConnect.Wire;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3;

[TestClass]
public sealed class Http3ExtendedConnectClientTests
{
    [TestMethod]
    public async Task OpenAsync_ServerEnablesAndAnswers200_SendsTheExtendedConnectAndOpens()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.EnablingControlStream());
        connection.RequestStream.Receive(Http3ServerBytes.Headers(new(":status", "200"), new("sec-websocket-protocol", "chat")));

        await using Http3ExtendedConnectWebSocket webSocket = await OpenAsync(connection);

        // RFC 9220 section 3: the extended CONNECT of RFC 8441 section 4, on an HTTP/3 request stream left open.
        byte[] expected = Http3ServerBytes.Headers([.. ExtendedConnectRequestFields.Build("https", "/chat", "example.com", ["chat"])]);
        CollectionAssert.AreEqual(expected, connection.RequestStream.Written.ToArray());
        Assert.IsFalse(connection.RequestStream.IsWriteEnded);
        Assert.AreEqual("chat", webSocket.Subprotocol);
    }

    [TestMethod]
    public async Task OpenAsync_InterimResponseAndDataBeforeTheFinal_SkipsThemAndOpens()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.ControlStream([new(Http3SettingIdentifier.EnableConnectProtocol, 1)], new Http3GoawayFrame(4)));
        connection.RequestStream.Receive([.. Http3ServerBytes.Headers(new HeaderField(":status", "103")), .. Http3ServerBytes.Data(0x01), .. Http3ServerBytes.Headers(new HeaderField(":status", "204"))]);

        await using Http3ExtendedConnectWebSocket webSocket = await OpenAsync(connection);

        Assert.IsNull(webSocket.Subprotocol);
    }

    [TestMethod]
    public async Task OpenAsync_SettingsWithoutEnableConnectProtocol_FailsAndClosesTheConnection()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.ControlStream([new(Http3SettingIdentifier.EnableConnectProtocol, 0)]));

        Http3ExtendedConnectException failure = await Assert.ThrowsExactlyAsync<Http3ExtendedConnectException>(() => OpenAsync(connection));

        // RFC 9220 section 3: without SETTINGS_ENABLE_CONNECT_PROTOCOL of 1 the client must not send :protocol.
        StringAssert.Contains(failure.Message, "SETTINGS_ENABLE_CONNECT_PROTOCOL");
        Assert.AreEqual(0, connection.RequestStream.Written.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_PeerStreamsEndBeforeAnySettings_FailsWithTheConnectionError()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.FailAccepting(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, "lost"));

        Http3ExtendedConnectException failure = await Assert.ThrowsExactlyAsync<Http3ExtendedConnectException>(() => OpenAsync(connection));

        StringAssert.Contains(failure.Message, "SETTINGS_ENABLE_CONNECT_PROTOCOL");
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("403", "403")]
    [DataRow(null, "(none)")]
    public async Task OpenAsync_ResponseIsNot2xx_FailsNamingTheStatus(string? status, string named)
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.EnablingControlStream());
        connection.RequestStream.Receive(Http3ServerBytes.Headers(new HeaderField(status is null ? "server" : ":status", status ?? "test")));

        Http3ExtendedConnectException failure = await Assert.ThrowsExactlyAsync<Http3ExtendedConnectException>(() => OpenAsync(connection));

        StringAssert.Contains(failure.Message, $":status {named}, not 2xx");
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task OpenAsync_StreamEndsBeforeTheResponse_Fails()
    {
        InMemoryMultiplexedConnection connection = new();
        connection.AddPeerStream(Http3ServerBytes.EnablingControlStream());
        connection.RequestStream.EndReceiving();

        Http3ExtendedConnectException failure = await Assert.ThrowsExactlyAsync<Http3ExtendedConnectException>(() => OpenAsync(connection));

        StringAssert.Contains(failure.Message, "before it responded");
    }

    private static async Task<Http3ExtendedConnectWebSocket> OpenAsync(InMemoryMultiplexedConnection connection) =>
        await Http3ExtendedConnectClient.OpenAsync(connection, "https", "/chat", "example.com", ["chat"], Http3ServerBytes.FixedRandomSource, CancellationToken.None);
}
