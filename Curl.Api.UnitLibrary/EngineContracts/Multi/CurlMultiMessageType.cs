namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The kind of a message <c>curl_multi_info_read</c> returns (<c>CURLMSG</c>), with upstream curl
/// 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
public enum CurlMultiMessageType
{
    /// <summary><c>CURLMSG_NONE</c> (0): no message; never returned.</summary>
    None = 0,

    /// <summary><c>CURLMSG_DONE</c> (1): a transfer finished; its result is in the message.</summary>
    Done = 1,
}
