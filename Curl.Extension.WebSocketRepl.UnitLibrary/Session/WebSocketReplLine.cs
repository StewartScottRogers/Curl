using System.Text;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl.Session;

/// <summary>
/// Turns one typed line into the frame it sends: <c>/binary &lt;text&gt;</c> sends the text's UTF-8 bytes as a
/// binary message, <c>/ping [text]</c> a ping, <c>/close</c> a close with code 1000, <c>//...</c> the line
/// without its first slash as text, and every other line itself as text.
/// </summary>
internal static class WebSocketReplLine
{
    /// <summary>The close code for a normal closure (RFC 6455 section 7.4.1), big-endian.</summary>
    public static readonly byte[] NormalClosure = [0x03, 0xE8];

    private const string BinaryCommand = "/binary ";

    private const string PingCommand = "/ping";

    /// <summary>Reads <paramref name="line" />.</summary>
    /// <param name="line">The line as typed, without its line ending.</param>
    /// <returns>The frame's opcode and unmasked payload.</returns>
    public static (WsOpcode Opcode, byte[] Payload) Read(string line) => line switch
    {
        "/close" => (WsOpcode.Close, NormalClosure),
        PingCommand => (WsOpcode.Ping, []),
        _ when line.StartsWith(PingCommand + " ", StringComparison.Ordinal) => (WsOpcode.Ping, PingPayload(line[(PingCommand.Length + 1)..])),
        _ when line.StartsWith(BinaryCommand, StringComparison.Ordinal) => (WsOpcode.Binary, Encoding.UTF8.GetBytes(line[BinaryCommand.Length..])),
        _ when line.StartsWith("//", StringComparison.Ordinal) => (WsOpcode.Text, Encoding.UTF8.GetBytes(line[1..])),
        _ => (WsOpcode.Text, Encoding.UTF8.GetBytes(line)),
    };

    // A control frame carries at most 125 bytes (RFC 6455 section 5.5), so a longer ping text is cut.
    private static byte[] PingPayload(string text)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text);
        return payload.Length > WsFrameEncoder.LongestControlPayloadLength
            ? payload[..WsFrameEncoder.LongestControlPayloadLength]
            : payload;
    }
}
