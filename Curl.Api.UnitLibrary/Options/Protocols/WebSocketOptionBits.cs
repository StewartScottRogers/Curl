namespace Curl.Api.Options.Protocols;

/// <summary>
/// The <c>CURLWS_*</c> bits <c>CURLOPT_WS_OPTIONS</c> accepts. Each member's value is
/// libcurl's bit.
/// </summary>
[Flags]
public enum WebSocketOptionBits
{
    /// <summary>No bit set: libcurl frames and unframes the data and answers pings (libcurl's default).</summary>
    None = 0,

    /// <summary><c>CURLWS_RAW_MODE</c> (1 &lt;&lt; 0): pass the WebSocket stream through raw, without framing.</summary>
    RawMode = 1 << 0,

    /// <summary><c>CURLWS_NOAUTOPONG</c> (1 &lt;&lt; 1): do not answer a ping with a pong automatically.</summary>
    NoAutoPong = 1 << 1,
}
