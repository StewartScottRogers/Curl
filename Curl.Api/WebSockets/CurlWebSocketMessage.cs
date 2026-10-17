using System.Buffers.Binary;
using System.Text;

namespace Curl.Api;

/// <summary>
/// One whole WebSocket message a <see cref="CurlWebSocket"/> received: a text or binary message with
/// its fragments joined, or one control frame.
/// </summary>
/// <param name="Type">What kind of message it is.</param>
/// <param name="Data">The message's payload; for a close frame, the status code and reason as sent.</param>
public sealed record CurlWebSocketMessage(CurlWebSocketMessageType Type, ReadOnlyMemory<byte> Data)
{
    /// <summary>The payload decoded as UTF-8.</summary>
    public string Text => Encoding.UTF8.GetString(Data.Span);

    /// <summary>
    /// For a close frame with a payload, the status code its first two bytes carry (big-endian);
    /// otherwise <see langword="null"/>.
    /// </summary>
    public ushort? CloseStatus =>
        Type == CurlWebSocketMessageType.Close && Data.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(Data.Span) : null;

    /// <summary>For a close frame, the UTF-8 reason after the status code; otherwise empty.</summary>
    public string CloseReason =>
        CloseStatus is null ? string.Empty : Encoding.UTF8.GetString(Data.Span[2..]);
}
