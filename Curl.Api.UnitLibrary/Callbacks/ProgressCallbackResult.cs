namespace Curl.Api.Callbacks;

/// <summary>
/// The magic value a <see cref="ProgressCallback"/> or <see cref="TransferInfoCallback"/> may
/// return, with upstream curl 8.22.0's number from <c>include/curl/curl.h</c>. Zero carries on;
/// any other value but this one aborts the transfer with <c>CURLE_ABORTED_BY_CALLBACK</c>.
/// </summary>
public static class ProgressCallbackResult
{
    /// <summary>
    /// <c>CURL_PROGRESSFUNC_CONTINUE</c> (<c>0x10000001</c>): carry on and let libcurl's own
    /// progress meter run as if no callback were set.
    /// </summary>
    public const int Continue = 0x10000001;
}
