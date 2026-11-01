using Curl.Original.Protocol.Ws;

namespace Curl.Conformance;

/// <summary>
/// The WebSocket randomness of upstream's debug build under <c>CURL_WS_FORCE_ZERO_MASK</c> (lib/ws.c), which
/// upstream's WebSocket cases 2700 to 2723 set so each sent frame's mask is zero and the server sees the payload
/// unmasked: a fill of one frame mask's length is zeros, and any other fill, the <c>Sec-WebSocket-Key</c>, stays
/// random from <see cref="SystemWebSocketRandomSource" />, as the debug build's key does (BL-2698, ADR-0698).
/// </summary>
internal sealed class ZeroMaskWebSocketRandomSource : IWebSocketRandomSource
{
    private readonly SystemWebSocketRandomSource keySource = new();

    /// <inheritdoc />
    public void Fill(Span<byte> destination)
    {
        if (destination.Length == WsFrameEncoder.MaskLength)
        {
            destination.Clear();
            return;
        }

        keySource.Fill(destination);
    }
}
