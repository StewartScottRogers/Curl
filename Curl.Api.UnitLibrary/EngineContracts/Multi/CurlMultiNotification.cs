namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The notifications <c>curl_multi_notify_enable</c> switches on (<c>CURLMNOTIFY_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/multi.h</c>.
/// </summary>
public enum CurlMultiNotification
{
    /// <summary><c>CURLMNOTIFY_INFO_READ</c> (0): a message waits for <c>curl_multi_info_read</c>.</summary>
    InfoRead = 0,

    /// <summary><c>CURLMNOTIFY_EASY_DONE</c> (1): a transfer finished.</summary>
    EasyDone = 1,
}
