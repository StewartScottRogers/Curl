namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>One message <c>curl_multi_info_read</c> returned.</summary>
/// <param name="Type">The <c>CURLMSG</c> number.</param>
/// <param name="Easy">The index of the easy handle it names, or -1 when it names none of the case's.</param>
/// <param name="Result">The transfer's <c>CURLcode</c>.</param>
/// <param name="Queue">The messages still queued after it.</param>
public sealed record LibcurlMultiMessage(int Type, int Easy, int Result, int Queue);
