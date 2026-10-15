namespace Curl.Api.EngineContracts;

/// <summary>
/// The bitmask <c>curl_easy_pause</c> takes (<c>CURLPAUSE_*</c>), with upstream curl 8.22.0's
/// numbers from <c>include/curl/curl.h</c>. A clear bit unpauses that direction.
/// </summary>
[Flags]
public enum CurlPause
{
    /// <summary><c>CURLPAUSE_CONT</c> (0), also <c>CURLPAUSE_RECV_CONT</c> and <c>CURLPAUSE_SEND_CONT</c>: unpause both directions.</summary>
    Continue = 0,

    /// <summary><c>CURLPAUSE_RECV</c> (<c>1 &lt;&lt; 0</c>): pause receiving.</summary>
    Receive = 1 << 0,

    /// <summary><c>CURLPAUSE_SEND</c> (<c>1 &lt;&lt; 2</c>): pause sending.</summary>
    Send = 1 << 2,

    /// <summary><c>CURLPAUSE_ALL</c> (<c>CURLPAUSE_RECV | CURLPAUSE_SEND</c>): pause both directions.</summary>
    All = Receive | Send,
}
