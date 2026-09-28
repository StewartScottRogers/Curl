namespace Curl.Protocol.Ws;

/// <summary>The RFC 6455 frame opcodes curl 8.21.0 accepts; any other value fails with 56.</summary>
internal enum WsOpcode
{
    /// <summary>A continuation fragment of the message in progress.</summary>
    Continuation = 0x0,

    /// <summary>A text message, or its first fragment.</summary>
    Text = 0x1,

    /// <summary>A binary message, or its first fragment.</summary>
    Binary = 0x2,

    /// <summary>A close frame.</summary>
    Close = 0x8,

    /// <summary>A ping, answered with a pong carrying the same payload.</summary>
    Ping = 0x9,

    /// <summary>A pong.</summary>
    Pong = 0xA,
}
