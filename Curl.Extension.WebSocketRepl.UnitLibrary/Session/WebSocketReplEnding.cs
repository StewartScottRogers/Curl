namespace Curl.Extension.WebSocketRepl.Session;

/// <summary>Why an interactive WebSocket session ended.</summary>
public enum WebSocketReplEnding
{
    /// <summary>The client sent a close frame (<c>/close</c> or the end of input) and the server answered it.</summary>
    ClosedByClient,

    /// <summary>The server sent a close frame first and the client answered it (RFC 6455 section 5.5.1).</summary>
    ClosedByServer,

    /// <summary>The connection ended with no closing handshake finished.</summary>
    ConnectionEnded,

    /// <summary>The server sent a frame RFC 6455 forbids; the client sent close code 1002 (section 7.4.1).</summary>
    ProtocolViolation,
}
