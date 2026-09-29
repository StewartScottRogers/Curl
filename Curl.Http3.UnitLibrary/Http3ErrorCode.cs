namespace Curl.Http3;

/// <summary>
/// The HTTP/3 application error codes RFC 9114 section 8.1 defines. QPACK's own codes are
/// <see cref="QpackErrorCode" />.
/// </summary>
public enum Http3ErrorCode : long
{
    /// <summary><c>H3_NO_ERROR</c> (<c>0x0100</c>): no error.</summary>
    NoError = 0x0100,

    /// <summary><c>H3_GENERAL_PROTOCOL_ERROR</c> (<c>0x0101</c>): a protocol error no more specific code covers.</summary>
    GeneralProtocolError = 0x0101,

    /// <summary><c>H3_INTERNAL_ERROR</c> (<c>0x0102</c>): an internal error.</summary>
    InternalError = 0x0102,

    /// <summary><c>H3_STREAM_CREATION_ERROR</c> (<c>0x0103</c>): the peer created a stream it may not.</summary>
    StreamCreationError = 0x0103,

    /// <summary><c>H3_CLOSED_CRITICAL_STREAM</c> (<c>0x0104</c>): a control or QPACK stream was closed.</summary>
    ClosedCriticalStream = 0x0104,

    /// <summary><c>H3_FRAME_UNEXPECTED</c> (<c>0x0105</c>): a frame arrived where it is not permitted.</summary>
    FrameUnexpected = 0x0105,

    /// <summary><c>H3_FRAME_ERROR</c> (<c>0x0106</c>): a frame's layout or length is invalid.</summary>
    FrameError = 0x0106,

    /// <summary><c>H3_EXCESSIVE_LOAD</c> (<c>0x0107</c>): the peer is generating excessive load.</summary>
    ExcessiveLoad = 0x0107,

    /// <summary><c>H3_ID_ERROR</c> (<c>0x0108</c>): a stream ID or push ID was used incorrectly.</summary>
    IdError = 0x0108,

    /// <summary><c>H3_SETTINGS_ERROR</c> (<c>0x0109</c>): a <c>SETTINGS</c> frame's content is invalid.</summary>
    SettingsError = 0x0109,

    /// <summary><c>H3_MISSING_SETTINGS</c> (<c>0x010a</c>): the control stream did not start with <c>SETTINGS</c>.</summary>
    MissingSettings = 0x010a,

    /// <summary><c>H3_REQUEST_REJECTED</c> (<c>0x010b</c>): the server rejected a request without processing it.</summary>
    RequestRejected = 0x010b,

    /// <summary><c>H3_REQUEST_CANCELLED</c> (<c>0x010c</c>): the request or its response was cancelled.</summary>
    RequestCancelled = 0x010c,

    /// <summary><c>H3_REQUEST_INCOMPLETE</c> (<c>0x010d</c>): the request stream ended before the request was complete.</summary>
    RequestIncomplete = 0x010d,

    /// <summary><c>H3_MESSAGE_ERROR</c> (<c>0x010e</c>): an HTTP message was malformed.</summary>
    MessageError = 0x010e,

    /// <summary><c>H3_CONNECT_ERROR</c> (<c>0x010f</c>): a CONNECT request's TCP connection was reset or closed abnormally.</summary>
    ConnectError = 0x010f,

    /// <summary><c>H3_VERSION_FALLBACK</c> (<c>0x0110</c>): the request should be retried over HTTP/1.1.</summary>
    VersionFallback = 0x0110,
}
