namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="TrailerCallback"/> returns: libcurl's <c>CURL_TRAILERFUNC_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum TrailerResult
{
    /// <summary><c>CURL_TRAILERFUNC_OK</c> (0): send the trailers the callback added.</summary>
    Ok = 0,

    /// <summary><c>CURL_TRAILERFUNC_ABORT</c> (1): fail the transfer with <c>CURLE_ABORTED_BY_CALLBACK</c>.</summary>
    Abort = 1,
}
