namespace Curl.Extension.Amqp0;

/// <summary>
/// The AMQP 0-9-1 reply codes (section 1.1, "Constants") a fault in the wire format closes the connection with.
/// </summary>
internal enum Amqp0ReplyCode : ushort
{
    /// <summary>501 frame-error: a frame the recipient could not decode (bad frame-end, oversized, unknown type).</summary>
    FrameError = 501,

    /// <summary>502 syntax-error: a frame whose fields hold illegal values, such as a truncated field.</summary>
    SyntaxError = 502,

    /// <summary>503 command-invalid: a method the recipient does not know (unknown class or method id).</summary>
    CommandInvalid = 503,

    /// <summary>505 unexpected-frame: a frame out of place, such as a method frame where a content body belongs.</summary>
    UnexpectedFrame = 505,
}
