namespace Curl.Api.Codes;

/// <summary>
/// The result of a libcurl URL API function: libcurl's <c>CURLUcode</c> (<c>CURLUE_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/urlapi.h</c>.
/// </summary>
public enum CurlUrlCode
{
    /// <summary><c>CURLUE_OK</c> (0): the function succeeded.</summary>
    Ok = 0,

    /// <summary><c>CURLUE_BAD_HANDLE</c> (1): the URL handle is not valid.</summary>
    BadHandle = 1,

    /// <summary><c>CURLUE_BAD_PARTPOINTER</c> (2): the pointer for the part was not valid.</summary>
    BadPartPointer = 2,

    /// <summary><c>CURLUE_MALFORMED_INPUT</c> (3): the URL could not be parsed.</summary>
    MalformedInput = 3,

    /// <summary><c>CURLUE_BAD_PORT_NUMBER</c> (4): the port number is not valid.</summary>
    BadPortNumber = 4,

    /// <summary><c>CURLUE_UNSUPPORTED_SCHEME</c> (5): the scheme is not supported.</summary>
    UnsupportedScheme = 5,

    /// <summary><c>CURLUE_URLDECODE</c> (6): URL decoding failed.</summary>
    UrlDecode = 6,

    /// <summary><c>CURLUE_OUT_OF_MEMORY</c> (7): a memory allocation failed.</summary>
    OutOfMemory = 7,

    /// <summary><c>CURLUE_USER_NOT_ALLOWED</c> (8): credentials were given where they are not allowed.</summary>
    UserNotAllowed = 8,

    /// <summary><c>CURLUE_UNKNOWN_PART</c> (9): the URL part was not recognised.</summary>
    UnknownPart = 9,

    /// <summary><c>CURLUE_NO_SCHEME</c> (10): the URL has no scheme.</summary>
    NoScheme = 10,

    /// <summary><c>CURLUE_NO_USER</c> (11): the URL has no user name.</summary>
    NoUser = 11,

    /// <summary><c>CURLUE_NO_PASSWORD</c> (12): the URL has no password.</summary>
    NoPassword = 12,

    /// <summary><c>CURLUE_NO_OPTIONS</c> (13): the URL has no login options.</summary>
    NoOptions = 13,

    /// <summary><c>CURLUE_NO_HOST</c> (14): the URL has no host.</summary>
    NoHost = 14,

    /// <summary><c>CURLUE_NO_PORT</c> (15): the URL has no port.</summary>
    NoPort = 15,

    /// <summary><c>CURLUE_NO_QUERY</c> (16): the URL has no query.</summary>
    NoQuery = 16,

    /// <summary><c>CURLUE_NO_FRAGMENT</c> (17): the URL has no fragment.</summary>
    NoFragment = 17,

    /// <summary><c>CURLUE_NO_ZONEID</c> (18): the URL has no IPv6 zone identifier.</summary>
    NoZoneId = 18,

    /// <summary><c>CURLUE_BAD_FILE_URL</c> (19): the file:// URL is not valid.</summary>
    BadFileUrl = 19,

    /// <summary><c>CURLUE_BAD_FRAGMENT</c> (20): the fragment is not valid.</summary>
    BadFragment = 20,

    /// <summary><c>CURLUE_BAD_HOSTNAME</c> (21): the host name is not valid.</summary>
    BadHostname = 21,

    /// <summary><c>CURLUE_BAD_IPV6</c> (22): the IPv6 address is not valid.</summary>
    BadIPv6 = 22,

    /// <summary><c>CURLUE_BAD_LOGIN</c> (23): the login part is not valid.</summary>
    BadLogin = 23,

    /// <summary><c>CURLUE_BAD_PASSWORD</c> (24): the password is not valid.</summary>
    BadPassword = 24,

    /// <summary><c>CURLUE_BAD_PATH</c> (25): the path is not valid.</summary>
    BadPath = 25,

    /// <summary><c>CURLUE_BAD_QUERY</c> (26): the query is not valid.</summary>
    BadQuery = 26,

    /// <summary><c>CURLUE_BAD_SCHEME</c> (27): the scheme is not valid.</summary>
    BadScheme = 27,

    /// <summary><c>CURLUE_BAD_SLASHES</c> (28): the URL has the wrong number of slashes after the scheme.</summary>
    BadSlashes = 28,

    /// <summary><c>CURLUE_BAD_USER</c> (29): the user name is not valid.</summary>
    BadUser = 29,

    /// <summary><c>CURLUE_LACKS_IDN</c> (30): the host needs internationalised domain name support, which is not built in.</summary>
    LacksIdn = 30,

    /// <summary><c>CURLUE_TOO_LARGE</c> (31): a part exceeded the maximum size allowed.</summary>
    TooLarge = 31,

    /// <summary><c>CURLUE_BACKSLASH</c> (32): the URL contains a backslash where none is allowed.</summary>
    Backslash = 32,
}
