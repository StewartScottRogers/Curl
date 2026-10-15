namespace Curl.Api.EngineContracts.Url;

/// <summary>
/// A part of a URL handle (<c>CURLUPart</c>, <c>CURLUPART_*</c>), with upstream curl 8.22.0's
/// numbers from <c>include/curl/urlapi.h</c>.
/// </summary>
public enum CurlUrlPart
{
    /// <summary><c>CURLUPART_URL</c> (0): the whole URL.</summary>
    Url = 0,

    /// <summary><c>CURLUPART_SCHEME</c> (1): the scheme.</summary>
    Scheme = 1,

    /// <summary><c>CURLUPART_USER</c> (2): the user name.</summary>
    User = 2,

    /// <summary><c>CURLUPART_PASSWORD</c> (3): the password.</summary>
    Password = 3,

    /// <summary><c>CURLUPART_OPTIONS</c> (4): the login options (IMAP, POP3 and SMTP).</summary>
    Options = 4,

    /// <summary><c>CURLUPART_HOST</c> (5): the host name or address.</summary>
    Host = 5,

    /// <summary><c>CURLUPART_PORT</c> (6): the port number, as text.</summary>
    Port = 6,

    /// <summary><c>CURLUPART_PATH</c> (7): the path.</summary>
    Path = 7,

    /// <summary><c>CURLUPART_QUERY</c> (8): the query, without its <c>?</c>.</summary>
    Query = 8,

    /// <summary><c>CURLUPART_FRAGMENT</c> (9): the fragment, without its <c>#</c>.</summary>
    Fragment = 9,

    /// <summary><c>CURLUPART_ZONEID</c> (10): the IPv6 zone identifier.</summary>
    ZoneId = 10,
}
