namespace Curl.Http2;

/// <summary>
/// The flag bits of RFC 9113 section 6. Their meaning depends on the frame type and two
/// share one bit, so they are byte constants rather than an enum.
/// </summary>
public static class Http2FrameFlags
{
    /// <summary>No flag set.</summary>
    public const byte None = 0x0;

    /// <summary>END_STREAM on DATA and HEADERS: the sender's last frame on the stream.</summary>
    public const byte EndStream = 0x1;

    /// <summary>ACK on SETTINGS and PING: this frame acknowledges the peer's.</summary>
    public const byte Acknowledgement = 0x1;

    /// <summary>END_HEADERS on HEADERS, PUSH_PROMISE and CONTINUATION: the header block is complete.</summary>
    public const byte EndHeaders = 0x4;

    /// <summary>PADDED on DATA, HEADERS and PUSH_PROMISE: a pad length byte leads the payload.</summary>
    public const byte Padded = 0x8;

    /// <summary>PRIORITY on HEADERS: the stream dependency and weight follow the pad length.</summary>
    public const byte Priority = 0x20;
}
