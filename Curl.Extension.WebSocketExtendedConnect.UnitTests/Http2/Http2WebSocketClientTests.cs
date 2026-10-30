using Curl.Http2;
using Curl.WebSocket;
using Curl.WebSocket.Fakes;

using static Curl.Extension.WebSocketExtendedConnect.Http2.ScriptedServerFrames;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

[TestClass]
public sealed class Http2WebSocketClientTests
{
    [TestMethod]
    public async Task OpenAsync_SettingAdvertised_SendsExtendedConnectOnANewStream()
    {
        // RFC 8441 section 4: :method CONNECT with :protocol, no END_STREAM, since the stream carries the WebSocket.
        var (webSocket, stream) = await OpenOverAsync();

        var headers = (await stream.ReadClientFramesAsync()).Single(frame => frame.Type == Http2FrameType.Headers);
        var fields = new HpackDecoder().Decode(Http2FramePayloadParser.ParseHeaders(headers).Fragment.Span);
        Assert.AreEqual(StreamId, webSocket.StreamId);
        Assert.AreEqual(StreamId, headers.StreamId);
        Assert.IsFalse(headers.HasFlag(Http2FrameFlags.EndStream));
        CollectionAssert.AreEqual(
            new HeaderField[]
            {
                new(":method", "CONNECT"), new(":protocol", "websocket"), new(":scheme", "https"), new(":path", "/chat"),
                new(":authority", "example.com"), new("sec-websocket-version", "13"), new("sec-websocket-protocol", "chat"),
            },
            fields.ToArray());
    }

    [TestMethod]
    public async Task OpenAsync_SettingsAcknowledgementFirst_WaitsForTheServersSettings()
    {
        // RFC 8441 section 3: the client waits for the server's own SETTINGS, which an ACK of the client's is not.
        var stream = new ScriptedServerStream(Http2FrameFactory.CreateSettingsAcknowledgement(), ServerSettings(EnableConnectProtocol), Response("200"));

        var webSocket = await OpenAsync(stream);

        Assert.IsFalse(webSocket.IsClosedByServer);
    }

    [TestMethod]
    public async Task OpenAsync_SettingNotAdvertised_FailsNamingTheSetting()
    {
        var message = await OpenFailureAsync(ServerSettings(new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100)), Response("200"));

        StringAssert.Contains(message, "did not advertise SETTINGS_ENABLE_CONNECT_PROTOCOL");
    }

    [TestMethod]
    public async Task OpenAsync_ClosedBeforeSettings_Fails()
    {
        var message = await OpenFailureAsync();

        StringAssert.Contains(message, "before sending its SETTINGS");
    }

    [TestMethod]
    public async Task OpenAsync_Non2xxResponse_FailsNamingTheStatus()
    {
        // RFC 8441 section 5: only a 2xx response opens the WebSocket.
        var message = await OpenFailureAsync(ServerSettings(EnableConnectProtocol), Response("404"));

        StringAssert.Contains(message, ":status 404, not 2xx");
    }

    [TestMethod]
    public async Task OpenAsync_ResponseWithoutStatus_FailsSayingNone()
    {
        var noStatus = Http2FrameFactory.CreateHeaders(StreamId, new HpackEncoder().Encode([new("server", "x")]), isEndStream: false, isEndHeaders: true);

        var message = await OpenFailureAsync(ServerSettings(EnableConnectProtocol), noStatus);

        StringAssert.Contains(message, ":status (none), not 2xx");
    }

    [TestMethod]
    public async Task OpenAsync_ClosedBeforeResponse_Fails()
    {
        var message = await OpenFailureAsync(ServerSettings(EnableConnectProtocol));

        StringAssert.Contains(message, "before answering the extended CONNECT");
    }

    [TestMethod]
    public async Task OpenAsync_DataBeforeResponse_Fails()
    {
        var message = await OpenFailureAsync(ServerSettings(EnableConnectProtocol), ServerData([0x81, 0x00]));

        StringAssert.Contains(message, "sent DATA before answering");
    }

    [TestMethod]
    public async Task OpenAsync_StreamResetBeforeResponse_FailsNamingTheErrorCode()
    {
        var message = await OpenFailureAsync(ServerSettings(EnableConnectProtocol), Http2FrameFactory.CreateRstStream(StreamId, Http2ErrorCode.RefusedStream));

        StringAssert.Contains(message, "reset the WebSocket's HTTP/2 stream with RefusedStream");
    }

}
