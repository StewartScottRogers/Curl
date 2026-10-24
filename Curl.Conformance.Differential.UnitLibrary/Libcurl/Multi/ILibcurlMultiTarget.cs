namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>
/// The libcurl calls one multi or share case makes, on one multi handle and one share handle,
/// so the same case replays against Curl's Libcurl and the reference libcurl 8.22.0 (ADR-0793).
/// Easy handles are named by the index their <c>easy</c> step gave them; codes are the C
/// <c>CURLcode</c>, <c>CURLMcode</c> and <c>CURLSHcode</c> numbers.
/// </summary>
public interface ILibcurlMultiTarget : IAsyncDisposable
{
    /// <summary>Creates the next easy handle, with a write callback that collects its body, and sets <paramref name="options"/> on it.</summary>
    /// <returns>Each option's <c>curl_easy_setopt</c> code, in order.</returns>
    IReadOnlyList<int> AddEasy(IReadOnlyList<LibcurlMultiValue> options);

    /// <summary>Calls <c>curl_multi_add_handle</c> with the easy handle at <paramref name="easy"/>, or NULL.</summary>
    int AddHandle(int? easy);

    /// <summary>Calls <c>curl_multi_remove_handle</c> with the easy handle at <paramref name="easy"/>, or NULL.</summary>
    int RemoveHandle(int? easy);

    /// <summary>Calls <c>curl_multi_setopt</c>.</summary>
    int SetMultiOption(LibcurlMultiValue setting);

    /// <summary>Calls <c>curl_multi_perform</c>.</summary>
    ValueTask<(int Code, int Running)> PerformAsync();

    /// <summary>Calls <c>curl_multi_poll</c> with no extra sockets.</summary>
    ValueTask<(int Code, int Ready)> PollAsync(int timeoutMilliseconds);

    /// <summary>Calls <c>curl_multi_wait</c> with no extra sockets.</summary>
    ValueTask<(int Code, int Ready)> WaitAsync(int timeoutMilliseconds);

    /// <summary>Calls <c>curl_multi_info_read</c> until it returns NULL.</summary>
    IReadOnlyList<LibcurlMultiMessage> ReadMessages();

    /// <summary>Gets the body the easy handle at <paramref name="easy"/> collected and its <c>CURLINFO_RESPONSE_CODE</c>.</summary>
    (byte[] Body, long ResponseCode) Body(int easy);

    /// <summary>Counts the handles <c>curl_multi_get_handles</c> returns.</summary>
    int HandleCount();

    /// <summary>Calls <c>curl_easy_cleanup</c> on the easy handle at <paramref name="easy"/>.</summary>
    void CleanupEasy(int easy);

    /// <summary>Calls <c>curl_multi_cleanup</c>.</summary>
    ValueTask CleanupMultiAsync();

    /// <summary>Calls <c>curl_share_setopt</c>; a <c>null</c> kind passes NULL.</summary>
    int SetShareOption(LibcurlMultiValue setting);

    /// <summary>Calls <c>curl_easy_perform</c> on the easy handle at <paramref name="easy"/>, then reads its response code and <c>CURLINFO_NUM_CONNECTS</c>.</summary>
    (int Code, long ResponseCode, long Connects) PerformEasy(int easy);

    /// <summary>Calls <c>curl_share_cleanup</c>.</summary>
    int CleanupShare();
}
