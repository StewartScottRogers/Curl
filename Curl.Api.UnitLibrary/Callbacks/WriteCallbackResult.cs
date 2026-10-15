namespace Curl.Api.Callbacks;

/// <summary>
/// The magic values a <see cref="WriteCallback"/> or <see cref="HeaderCallback"/> may return in
/// place of a byte count, with upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public static class WriteCallbackResult
{
    /// <summary><c>CURL_WRITEFUNC_PAUSE</c> (<c>0x10000001</c>): pause the transfer (write callback only).</summary>
    public const long Pause = 0x10000001;

    /// <summary><c>CURL_WRITEFUNC_ERROR</c> (<c>0xFFFFFFFF</c>): fail the transfer with <c>CURLE_WRITE_ERROR</c>.</summary>
    public const long Error = 0xFFFFFFFF;
}
