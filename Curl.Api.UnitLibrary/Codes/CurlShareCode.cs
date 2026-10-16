namespace Curl.Api.Codes;

/// <summary>
/// The result of a libcurl share function: libcurl's <c>CURLSHcode</c> (<c>CURLSHE_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public enum CurlShareCode
{
    /// <summary><c>CURLSHE_OK</c> (0): the function succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURLSHE_BAD_OPTION</c> (1): a share option was not recognised.</summary>
    BadOption = 1,

    /// <summary><c>CURLSHE_IN_USE</c> (2): the share object is in use and cannot be changed.</summary>
    InUse = 2,

    /// <summary><c>CURLSHE_INVALID</c> (3): the share handle is not valid.</summary>
    Invalid = 3,

    /// <summary><c>CURLSHE_NOMEM</c> (4): a memory allocation failed.</summary>
    NoMem = 4,

    /// <summary><c>CURLSHE_NOT_BUILT_IN</c> (5): the requested sharing was not compiled in.</summary>
    NotBuiltIn = 5,
}
