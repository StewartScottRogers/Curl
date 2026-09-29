namespace Curl.Http3;

/// <summary>
/// The HTTP/3 frame types RFC 9114 section 7.2 defines, and the HTTP/2 frame types section
/// 7.2.8 reserves because they have no HTTP/3 meaning.
/// </summary>
public enum Http3FrameType : long
{
    /// <summary><c>DATA</c> (<c>0x00</c>): request or response content.</summary>
    Data = 0x00,

    /// <summary><c>HEADERS</c> (<c>0x01</c>): a QPACK-encoded field section.</summary>
    Headers = 0x01,

    /// <summary>HTTP/2's <c>PRIORITY</c> (<c>0x02</c>), reserved.</summary>
    ReservedHttp2Priority = 0x02,

    /// <summary><c>CANCEL_PUSH</c> (<c>0x03</c>): a server push is cancelled.</summary>
    CancelPush = 0x03,

    /// <summary><c>SETTINGS</c> (<c>0x04</c>): the sender's configuration.</summary>
    Settings = 0x04,

    /// <summary><c>PUSH_PROMISE</c> (<c>0x05</c>): a server push's request.</summary>
    PushPromise = 0x05,

    /// <summary>HTTP/2's <c>PING</c> (<c>0x06</c>), reserved.</summary>
    ReservedHttp2Ping = 0x06,

    /// <summary><c>GOAWAY</c> (<c>0x07</c>): the sender is shutting the connection down.</summary>
    Goaway = 0x07,

    /// <summary>HTTP/2's <c>WINDOW_UPDATE</c> (<c>0x08</c>), reserved.</summary>
    ReservedHttp2WindowUpdate = 0x08,

    /// <summary>HTTP/2's <c>CONTINUATION</c> (<c>0x09</c>), reserved.</summary>
    ReservedHttp2Continuation = 0x09,

    /// <summary><c>MAX_PUSH_ID</c> (<c>0x0d</c>): the largest push ID the client allows.</summary>
    MaxPushId = 0x0d,
}
