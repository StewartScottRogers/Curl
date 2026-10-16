namespace Curl.Api.Codes;

/// <summary>
/// The result of a libcurl header API function: libcurl's <c>CURLHcode</c> (<c>CURLHE_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/header.h</c>.
/// </summary>
public enum CurlHeaderCode
{
    /// <summary><c>CURLHE_OK</c> (0): the function succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURLHE_BADINDEX</c> (1): the header exists, but not with this index.</summary>
    BadIndex = 1,

    /// <summary><c>CURLHE_MISSING</c> (2): no such header exists.</summary>
    Missing = 2,

    /// <summary><c>CURLHE_NOHEADERS</c> (3): no headers exist yet.</summary>
    NoHeaders = 3,

    /// <summary><c>CURLHE_NOREQUEST</c> (4): no request with this number was made.</summary>
    NoRequest = 4,

    /// <summary><c>CURLHE_OUT_OF_MEMORY</c> (5): a memory allocation failed.</summary>
    OutOfMemory = 5,

    /// <summary><c>CURLHE_BAD_ARGUMENT</c> (6): a function argument was not valid.</summary>
    BadArgument = 6,

    /// <summary><c>CURLHE_NOT_BUILT_IN</c> (7): the header API was not compiled in.</summary>
    NotBuiltIn = 7,
}
