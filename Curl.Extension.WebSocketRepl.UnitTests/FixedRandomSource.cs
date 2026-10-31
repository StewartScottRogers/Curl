using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl;

/// <summary>Fills every request with the same bytes, repeated, so keys and masks are known.</summary>
internal sealed class FixedRandomSource(params byte[] bytes) : IWebSocketRandomSource
{
    public void Fill(Span<byte> destination)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = bytes.Length == 0 ? (byte)0 : bytes[index % bytes.Length];
        }
    }
}
