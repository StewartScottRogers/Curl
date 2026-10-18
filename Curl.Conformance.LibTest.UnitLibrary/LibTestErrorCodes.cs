using Curl.Api.Codes;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The <c>TEST_ERR_*</c> codes of curl 8.22.0's <c>tests/libtest/first.h</c>: the codes a libtest
/// program's <c>result</c> takes when a step fails outside libcurl. Upstream defines each as one of
/// the <c>CURLE_OBSOLETE*</c> codes, so every value lies in the <c>CURLcode</c> range and below 127.
/// </summary>
public static class LibTestErrorCodes
{
    /// <summary><c>TEST_ERR_MAJOR_BAD</c>, <c>CURLE_OBSOLETE20</c> (20): a step failed badly, as a failed <c>curl_slist_append</c>.</summary>
    public const CurlCode MajorBad = CurlCode.Obsolete20;

    /// <summary><c>TEST_ERR_RUNS_FOREVER</c>, <c>CURLE_OBSOLETE24</c> (24): the program ran past its hang timeout.</summary>
    public const CurlCode RunsForever = CurlCode.Obsolete24;

    /// <summary><c>TEST_ERR_EASY_INIT</c>, <c>CURLE_OBSOLETE29</c> (29): <c>curl_easy_init</c> gave no handle.</summary>
    public const CurlCode EasyInit = CurlCode.Obsolete29;

    /// <summary><c>TEST_ERR_MULTI</c>, <c>CURLE_OBSOLETE32</c> (32): a <c>curl_multi_*</c> call failed.</summary>
    public const CurlCode Multi = CurlCode.Obsolete32;

    /// <summary><c>TEST_ERR_NUM_HANDLES</c>, <c>CURLE_OBSOLETE34</c> (34): <c>curl_multi_perform</c> gave a negative running count.</summary>
    public const CurlCode NumHandles = CurlCode.Obsolete34;

    /// <summary><c>TEST_ERR_SELECT</c>, <c>CURLE_OBSOLETE40</c> (40): <c>select()</c> failed.</summary>
    public const CurlCode Select = CurlCode.Obsolete40;

    /// <summary><c>TEST_ERR_SUCCESS</c>, <c>CURLE_OBSOLETE41</c> (41): a check meant to fail succeeded.</summary>
    public const CurlCode Success = CurlCode.Obsolete41;

    /// <summary><c>TEST_ERR_FAILURE</c>, <c>CURLE_OBSOLETE44</c> (44): a check failed.</summary>
    public const CurlCode Failure = CurlCode.Obsolete44;

    /// <summary><c>TEST_ERR_USAGE</c>, <c>CURLE_OBSOLETE46</c> (46): the program was given the wrong arguments.</summary>
    public const CurlCode Usage = CurlCode.Obsolete46;

    /// <summary><c>TEST_ERR_FOPEN</c>, <c>CURLE_OBSOLETE50</c> (50): a file could not be opened.</summary>
    public const CurlCode FileOpen = CurlCode.Obsolete50;

    /// <summary><c>TEST_ERR_FSTAT</c>, <c>CURLE_OBSOLETE51</c> (51): a file's size could not be read.</summary>
    public const CurlCode FileStat = CurlCode.Obsolete51;

    /// <summary><c>TEST_ERR_BAD_TIMEOUT</c>, <c>CURLE_OBSOLETE57</c> (57): a timeout value was out of range.</summary>
    public const CurlCode BadTimeout = CurlCode.Obsolete57;
}
