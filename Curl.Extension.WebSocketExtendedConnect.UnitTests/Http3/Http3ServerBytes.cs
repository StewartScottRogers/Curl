using Curl.Extension.WebSocketExtendedConnect.Http3;
using Curl.Http2;
using Curl.Http3;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3;

/// <summary>Builds what an HTTP/3 server sends, and opens a WebSocket against an in-memory one.</summary>
internal static class Http3ServerBytes
{
    /// <summary>The masking key every test frame uses.</summary>
    internal static readonly IWebSocketRandomSource FixedRandomSource = new FixedMaskRandomSource();

    /// <summary>A control stream: its type byte 0x00 and a SETTINGS frame, then any other frames.</summary>
    internal static byte[] ControlStream(IReadOnlyList<Http3Setting> settings, params Http3Frame[] after) =>
        [0x00, .. new Http3SettingsFrame(settings).ToBytes(), .. after.SelectMany(frame => frame.ToBytes())];

    /// <summary>A control stream whose SETTINGS enable extended CONNECT (RFC 9220 section 3).</summary>
    internal static byte[] EnablingControlStream() => ControlStream([new(Http3SettingIdentifier.EnableConnectProtocol, 1)]);

    /// <summary>A HEADERS frame carrying <paramref name="fields" />, encoded with no dynamic table.</summary>
    internal static byte[] Headers(params HeaderField[] fields) =>
        new Http3HeadersFrame(new QpackEncoder(maximumTableCapacity: 0, maximumBlockedStreams: 0).EncodeFieldSection(0, fields)).ToBytes();

    /// <summary>A DATA frame carrying <paramref name="payload" />.</summary>
    internal static byte[] Data(params byte[] payload) => new Http3DataFrame(payload).ToBytes();

    /// <summary>Opens a WebSocket on a connection whose server enables extended CONNECT and answers 200.</summary>
    internal static async Task<Http3ExtendedConnectWebSocket> OpenAsync(InMemoryMultiplexedConnection connection)
    {
        connection.AddPeerStream(EnablingControlStream());
        connection.RequestStream.Receive(Headers(new(":status", "200"), new("sec-websocket-protocol", "chat")));
        Http3ExtendedConnectWebSocket webSocket = await Http3ExtendedConnectClient.OpenAsync(connection, "https", "/chat", "example.com", ["chat"], FixedRandomSource, CancellationToken.None);
        connection.RequestStream.Written.SetLength(0);
        return webSocket;
    }

    private sealed class FixedMaskRandomSource : IWebSocketRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Fill(0x5A);
    }
}
