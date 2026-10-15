namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="SeekCallback"/> returns: libcurl's <c>CURL_SEEKFUNC_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum SeekResult
{
    /// <summary><c>CURL_SEEKFUNC_OK</c> (0): the seek succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURL_SEEKFUNC_FAIL</c> (1): fail the transfer.</summary>
    Fail = 1,

    /// <summary><c>CURL_SEEKFUNC_CANTSEEK</c> (2): seeking is impossible; libcurl may work around it by reading.</summary>
    CantSeek = 2,
}
