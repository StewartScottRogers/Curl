namespace Curl.Authentication;

/// <summary>
/// How a lookup in a netrc file ended.
/// </summary>
public enum NetrcLookupOutcome
{
    /// <summary>An entry for the host (and user name, when one was given) was found.</summary>
    Found,

    /// <summary>No entry matched; curl carries on without netrc credentials.</summary>
    NotFound,

    /// <summary>
    /// The file was malformed before a match was made; curl fails the transfer with
    /// <c>curl: (26) .netrc error: syntax error</c>.
    /// </summary>
    SyntaxError,
}
