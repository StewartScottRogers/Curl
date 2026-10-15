namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_XFERINFOFUNCTION</c>: reports progress in <c>curl_off_t</c> byte counts.
/// <c>CURLOPT_XFERINFODATA</c> is the delegate's closure.
/// </summary>
/// <param name="downloadTotal">The bytes expected to download, or 0 when unknown.</param>
/// <param name="downloadNow">The bytes downloaded so far.</param>
/// <param name="uploadTotal">The bytes expected to upload, or 0 when unknown.</param>
/// <param name="uploadNow">The bytes uploaded so far.</param>
/// <returns>0 to carry on, <see cref="ProgressCallbackResult.Continue"/>, or any other value to abort.</returns>
public delegate int TransferInfoCallback(long downloadTotal, long downloadNow, long uploadTotal, long uploadNow);
