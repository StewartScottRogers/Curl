namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// What a <c>CURLMOPT_PUSHFUNCTION</c> callback answers to a server push (<c>CURL_PUSH_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
public enum CurlPushResult
{
    /// <summary><c>CURL_PUSH_OK</c> (0): accept the pushed stream; its transfer joins the multi handle.</summary>
    Ok = 0,

    /// <summary><c>CURL_PUSH_DENY</c> (1): refuse the pushed stream.</summary>
    Deny = 1,

    /// <summary><c>CURL_PUSH_ERROROUT</c> (2): refuse the pushed stream and fail the parent transfer.</summary>
    ErrorOut = 2,
}
