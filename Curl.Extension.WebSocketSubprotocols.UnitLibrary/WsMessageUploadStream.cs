using Curl.WebSocket;

namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// An empty upload that gives the ws handler whole messages instead of bytes (BL-4253): the handler sends each of
/// <see cref="Messages" /> as one final masked frame of its own opcode, in order.
/// </summary>
/// <param name="messages">The messages to send.</param>
internal sealed class WsMessageUploadStream(IReadOnlyList<WsUploadMessage> messages) : MemoryStream, IWsUploadMessageSource
{
    /// <inheritdoc />
    public IReadOnlyList<WsUploadMessage> Messages => messages;
}
