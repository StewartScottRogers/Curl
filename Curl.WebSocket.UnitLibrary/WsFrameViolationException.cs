namespace Curl.WebSocket;

/// <summary>
/// Stops <see cref="WsFrameDecoder" /> at a server frame that breaks RFC 6455, carrying curl
/// 8.21.0's <c>[WS]</c> message; it never leaves the decoder.
/// </summary>
/// <param name="message">curl's message for the violation.</param>
internal sealed class WsFrameViolationException(string message) : Exception(message);
