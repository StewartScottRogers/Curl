namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_FNMATCH_FUNCTION</c>: matches a file name against a wildcard pattern in place of
/// libcurl's own matcher. <c>CURLOPT_FNMATCH_DATA</c> is the delegate's closure.
/// </summary>
/// <param name="pattern">The wildcard pattern.</param>
/// <param name="name">The file name to test.</param>
/// <returns>Whether the name matches.</returns>
public delegate FileNameMatchResult FileNameMatchCallback(string pattern, string name);
