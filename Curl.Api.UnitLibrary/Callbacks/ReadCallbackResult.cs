namespace Curl.Api.Callbacks;

/// <summary>
/// The magic values a <see cref="ReadCallback"/> may return in place of a byte count, with
/// upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public static class ReadCallbackResult
{
    /// <summary><c>CURL_READFUNC_ABORT</c> (<c>0x10000000</c>): stop the transfer with <c>CURLE_ABORTED_BY_CALLBACK</c>.</summary>
    public const long Abort = 0x10000000;

    /// <summary><c>CURL_READFUNC_PAUSE</c> (<c>0x10000001</c>): pause sending.</summary>
    public const long Pause = 0x10000001;
}
