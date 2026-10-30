namespace Curl.WebSocket;

/// <summary>
/// An upload stream that also gives the whole messages it uploads, so the ws handler sends each of
/// <see cref="Messages" /> as one final masked frame of its own opcode, in order, instead of framing the stream's
/// bytes as binary frames of up to 64 KiB (BL-4253). An extension's subprotocol (graphql-transport-ws, WAMP, STOMP)
/// needs one text frame per message, and may reference this shared layer where it may not reference the handler.
/// </summary>
public interface IWsUploadMessageSource
{
    /// <summary>Gets the messages to send, in order.</summary>
    IReadOnlyList<WsUploadMessage> Messages { get; }
}
