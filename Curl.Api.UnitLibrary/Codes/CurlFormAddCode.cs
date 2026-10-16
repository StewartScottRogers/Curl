namespace Curl.Api.Codes;

/// <summary>
/// The result of libcurl's deprecated <c>curl_formadd</c>: libcurl's <c>CURLFORMcode</c>
/// (<c>CURL_FORMADD_*</c>), with upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public enum CurlFormAddCode
{
    /// <summary><c>CURL_FORMADD_OK</c> (0): the function succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURL_FORMADD_MEMORY</c> (1): a memory allocation failed.</summary>
    Memory = 1,

    /// <summary><c>CURL_FORMADD_OPTION_TWICE</c> (2): an option was given twice for one part.</summary>
    OptionTwice = 2,

    /// <summary><c>CURL_FORMADD_NULL</c> (3): a null pointer was given where a string was expected.</summary>
    Null = 3,

    /// <summary><c>CURL_FORMADD_UNKNOWN_OPTION</c> (4): an option was not recognised.</summary>
    UnknownOption = 4,

    /// <summary><c>CURL_FORMADD_INCOMPLETE</c> (5): a part was incomplete.</summary>
    Incomplete = 5,

    /// <summary><c>CURL_FORMADD_ILLEGAL_ARRAY</c> (6): an option not allowed in an array was used in one.</summary>
    IllegalArray = 6,

    /// <summary><c>CURL_FORMADD_DISABLED</c> (7): the form API was not compiled in.</summary>
    Disabled = 7,
}
