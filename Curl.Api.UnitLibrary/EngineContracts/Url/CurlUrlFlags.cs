namespace Curl.Api.EngineContracts.Url;

/// <summary>
/// The flags of <see cref="ICurlUrlHandle.Get"/> and <see cref="ICurlUrlHandle.Set"/>
/// (<c>CURLU_*</c>), with upstream curl 8.22.0's numbers from <c>include/curl/urlapi.h</c>.
/// </summary>
[Flags]
public enum CurlUrlFlags
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary><c>CURLU_DEFAULT_PORT</c> (1 &lt;&lt; 0): return the scheme's default port when none is set.</summary>
    DefaultPort = 1 << 0,

    /// <summary><c>CURLU_NO_DEFAULT_PORT</c> (1 &lt;&lt; 1): return no port when it is the scheme's default.</summary>
    NoDefaultPort = 1 << 1,

    /// <summary><c>CURLU_DEFAULT_SCHEME</c> (1 &lt;&lt; 2): return <c>https</c> when no scheme is set.</summary>
    DefaultScheme = 1 << 2,

    /// <summary><c>CURLU_NON_SUPPORT_SCHEME</c> (1 &lt;&lt; 3): accept a scheme libcurl does not support.</summary>
    NonSupportScheme = 1 << 3,

    /// <summary><c>CURLU_PATH_AS_IS</c> (1 &lt;&lt; 4): leave <c>../</c> and <c>./</c> in the path.</summary>
    PathAsIs = 1 << 4,

    /// <summary><c>CURLU_DISALLOW_USER</c> (1 &lt;&lt; 5): refuse a URL with a user name or password.</summary>
    DisallowUser = 1 << 5,

    /// <summary><c>CURLU_URLDECODE</c> (1 &lt;&lt; 6): URL-decode the part on get.</summary>
    UrlDecode = 1 << 6,

    /// <summary><c>CURLU_URLENCODE</c> (1 &lt;&lt; 7): URL-encode the part on set or get.</summary>
    UrlEncode = 1 << 7,

    /// <summary><c>CURLU_APPENDQUERY</c> (1 &lt;&lt; 8): append to the query with <c>&amp;</c> rather than replace it.</summary>
    AppendQuery = 1 << 8,

    /// <summary><c>CURLU_GUESS_SCHEME</c> (1 &lt;&lt; 9): guess the scheme from the host name when none is given.</summary>
    GuessScheme = 1 << 9,

    /// <summary><c>CURLU_NO_AUTHORITY</c> (1 &lt;&lt; 10): accept an empty authority for an unknown scheme.</summary>
    NoAuthority = 1 << 10,

    /// <summary><c>CURLU_ALLOW_SPACE</c> (1 &lt;&lt; 11): accept spaces in the URL.</summary>
    AllowSpace = 1 << 11,

    /// <summary><c>CURLU_PUNYCODE</c> (1 &lt;&lt; 12): return an international host name in punycode.</summary>
    Punycode = 1 << 12,

    /// <summary><c>CURLU_PUNY2IDN</c> (1 &lt;&lt; 13): return a punycode host name as an international one.</summary>
    PunycodeToIdn = 1 << 13,

    /// <summary><c>CURLU_GET_EMPTY</c> (1 &lt;&lt; 14): return an empty query or fragment rather than none.</summary>
    GetEmpty = 1 << 14,

    /// <summary><c>CURLU_NO_GUESS_SCHEME</c> (1 &lt;&lt; 15): return no guessed scheme on get.</summary>
    NoGuessScheme = 1 << 15,
}
