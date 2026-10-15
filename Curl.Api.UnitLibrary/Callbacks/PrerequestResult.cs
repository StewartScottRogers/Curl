namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="PrerequestCallback"/> returns: libcurl's <c>CURL_PREREQFUNC_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum PrerequestResult
{
    /// <summary><c>CURL_PREREQFUNC_OK</c> (0): send the request.</summary>
    Ok = 0,

    /// <summary><c>CURL_PREREQFUNC_ABORT</c> (1): fail the transfer with <c>CURLE_ABORTED_BY_CALLBACK</c>.</summary>
    Abort = 1,
}
