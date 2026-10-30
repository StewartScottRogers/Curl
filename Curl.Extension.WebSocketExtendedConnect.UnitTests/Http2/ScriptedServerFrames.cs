using Curl.Http2;
using Curl.WebSocket;
using Curl.WebSocket.Fakes;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

/// <summary>The server frames and the opening the extended CONNECT tests replay, built from recorded frame shapes.</summary>
internal static class ScriptedServerFrames
{
    internal const int StreamId = 1;

    internal static readonly Http2Setting EnableConnectProtocol = new((Http2SettingIdentifier)0x8, 1);

    internal static Http2Frame ServerSettings(params Http2Setting[] settings) => Http2FrameFactory.CreateSettings(settings);

    internal static Http2Frame Response(string status) =>
        Http2FrameFactory.CreateHeaders(StreamId, new HpackEncoder().Encode([new(":status", status)]), isEndStream: false, isEndHeaders: true);

    internal static Http2Frame ServerData(byte[] bytes, bool isEndStream = false) => Http2FrameFactory.CreateData(StreamId, bytes, isEndStream);

    internal static Task<Http2WebSocket> OpenAsync(ScriptedServerStream stream) =>
        Http2WebSocketClient.OpenAsync(stream, "https", "/chat", "example.com", ["chat"], new FixedRandomSource(), CancellationToken.None);

    internal static async Task<string> OpenFailureAsync(params Http2Frame[] serverFrames)
    {
        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => OpenAsync(new ScriptedServerStream(serverFrames)));
        return failure.Message;
    }

    internal static async Task<(Http2WebSocket WebSocket, ScriptedServerStream Stream)> OpenOverAsync(params Http2Frame[] framesAfterResponse)
    {
        var stream = new ScriptedServerStream([ServerSettings(EnableConnectProtocol), Response("200"), .. framesAfterResponse]);
        return (await OpenAsync(stream), stream);
    }
}
