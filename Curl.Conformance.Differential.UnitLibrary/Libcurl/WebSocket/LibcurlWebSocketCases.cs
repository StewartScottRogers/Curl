using static Curl.Conformance.Differential.Libcurl.WebSocket.LibcurlWebSocketFrames;
using static Curl.Conformance.Differential.Libcurl.WebSocket.LibcurlWebSocketStep;

namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>
/// The WebSocket differential cases (BL-3240): <c>curl_ws_send</c>, <c>curl_ws_recv</c> and
/// <c>curl_ws_meta</c> on a connect-only handle, and a callback-mode perform, over text, binary,
/// fragmented, ping, pong and close frames. The tester covered no WebSocket, so these are new.
/// </summary>
public static class LibcurlWebSocketCases
{
    private const int TextFlag = 1;
    private const int BinaryFlag = 2;
    private const int ContinuationFlag = 4;
    private const int CloseFlag = 8;
    private const int PingFlag = 16;
    private const int OffsetFlag = 32;
    private const int PongFlag = 64;

    private static readonly byte[] NormalClosure = [0x03, 0xE8];

    /// <summary>Gets every case, in report order.</summary>
    public static IReadOnlyList<LibcurlWebSocketCase> All { get; } =
    [
        ConnectOnly("ws_recv_text_binary_close", [.. Frame(Text, "hello"u8.ToArray()), .. Frame(Binary, [1, 2, 3]), .. Frame(Close, [.. NormalClosure, .. "bye"u8])], Receive(), Receive(), Receive(), Receive()),
        ConnectOnly("ws_send_text_binary_close", [], Send("hi"u8.ToArray(), TextFlag), Send([0, 255, 7], BinaryFlag), Send(NormalClosure, CloseFlag), Receive()),
        ConnectOnly("ws_recv_fragments", [.. Frame(Text, "hel"u8.ToArray(), final: false), .. Frame(Continuation, "lo"u8.ToArray())], Receive(), Receive(), Send([], CloseFlag), Receive()),
        ConnectOnly("ws_recv_small_buffer", Frame(Text, "0123456789"u8.ToArray()), Receive(4), Receive(4), Receive(4), Send([], CloseFlag)),
        ConnectOnly("ws_send_fragments", [], Send("abc"u8.ToArray(), TextFlag | ContinuationFlag), Send("def"u8.ToArray(), TextFlag | ContinuationFlag), Send("g"u8.ToArray(), TextFlag), Send([], CloseFlag)),
        ConnectOnly("ws_send_offset", [], Send("abcd"u8.ToArray(), BinaryFlag | OffsetFlag, 8), Send("efgh"u8.ToArray(), BinaryFlag | OffsetFlag), Send([], CloseFlag)),
        ConnectOnly("ws_recv_ping_auto_pong", Frame(Ping, "p1"u8.ToArray()), Receive(), Send([], CloseFlag), Receive()),
        ConnectOnly("ws_send_ping_then_pong", [], Send("x"u8.ToArray(), PingFlag), Receive(), Send("y"u8.ToArray(), PongFlag), Send([], CloseFlag)),
        ConnectOnly("ws_recv_extended_length", Frame(Binary, [.. Enumerable.Range(0, 300).Select(value => (byte)value)]), Receive(1024), Send([], CloseFlag)),
        ConnectOnly("ws_meta_outside_callback", Frame(Text, "m"u8.ToArray()), Meta(), Receive(), Meta(), Send([], CloseFlag)),
        new("ws_server_close_then_recv", Frame(Close, NormalClosure), true, true, [Receive(), Receive()]),
        new("ws_callback_mode", [.. Frame(Text, "hello"u8.ToArray()), .. Frame(Binary, [1, 2, 3]), .. Frame(Ping, "p"u8.ToArray()), .. Frame(Close, NormalClosure)], true, false, []),
    ];

    private static LibcurlWebSocketCase ConnectOnly(string name, byte[] serverFrames, params LibcurlWebSocketStep[] steps) =>
        new(name, serverFrames, false, true, steps);
}
