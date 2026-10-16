namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_PROGRESSFUNCTION</c> (deprecated upstream in favour of <see cref="TransferInfoCallback"/>):
/// reports progress in floating-point byte counts. <c>CURLOPT_PROGRESSDATA</c> is the delegate's closure.
/// </summary>
/// <param name="downloadTotal">The bytes expected to download, or 0 when unknown.</param>
/// <param name="downloadNow">The bytes downloaded so far.</param>
/// <param name="uploadTotal">The bytes expected to upload, or 0 when unknown.</param>
/// <param name="uploadNow">The bytes uploaded so far.</param>
/// <returns>0 to carry on, <see cref="ProgressCallbackResult.Continue"/>, or any other value to abort.</returns>
public delegate int ProgressCallback(double downloadTotal, double downloadNow, double uploadTotal, double uploadNow);
