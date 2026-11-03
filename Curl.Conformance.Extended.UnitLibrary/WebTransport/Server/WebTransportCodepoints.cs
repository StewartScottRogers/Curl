namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>The codepoints of WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) the reference server uses.</summary>
public static class WebTransportCodepoints
{
    /// <summary>The signal value that opens a WebTransport bidirectional stream (WT_STREAM).</summary>
    public const long BidirectionalStreamSignal = 0x41;

    /// <summary>The stream type of a WebTransport unidirectional stream.</summary>
    public const long UnidirectionalStreamType = 0x54;

    /// <summary>The type of the WT_CLOSE_SESSION capsule.</summary>
    public const long CloseSessionCapsule = 0x2843;

    /// <summary>The identifier of SETTINGS_WT_MAX_SESSIONS.</summary>
    public const long MaxSessionsSetting = 0x14e9cd29;
}
