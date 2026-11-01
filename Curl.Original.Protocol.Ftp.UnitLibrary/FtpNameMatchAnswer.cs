namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// What <see cref="IFtpWildcardHooks.MatchName" /> answers, as libcurl's
/// <c>CURL_FNMATCHFUNC_MATCH</c>, <c>CURL_FNMATCHFUNC_NOMATCH</c> and <c>CURL_FNMATCHFUNC_FAIL</c>.
/// </summary>
public enum FtpNameMatchAnswer
{
    /// <summary>The name matches the pattern: the entry is kept.</summary>
    Match = 0,

    /// <summary>The name does not match: the entry is left out.</summary>
    NoMatch = 1,

    /// <summary>Matching failed: the entry is left out, as curl 8.22.0 treats any answer but a match.</summary>
    Fail = 2,
}
