namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="FileNameMatchCallback"/> returns: libcurl's <c>CURL_FNMATCHFUNC_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum FileNameMatchResult
{
    /// <summary><c>CURL_FNMATCHFUNC_MATCH</c> (0): the name matches the pattern.</summary>
    Match = 0,

    /// <summary><c>CURL_FNMATCHFUNC_NOMATCH</c> (1): the name does not match the pattern.</summary>
    NoMatch = 1,

    /// <summary><c>CURL_FNMATCHFUNC_FAIL</c> (2): matching failed; fail the transfer.</summary>
    Fail = 2,
}
