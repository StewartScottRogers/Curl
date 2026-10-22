namespace Curl.Engine;

/// <summary>
/// Where <see cref="UrlPartText.UrlEncodeString" /> writes a space as <c>+</c>: upstream
/// <c>urlencode_str</c>'s <c>query</c> argument, with its values.
/// </summary>
internal enum UrlEncodeQuery
{
    /// <summary><c>QUERY_NO</c>: the text is not a query, so a space is <c>%20</c> throughout.</summary>
    No = 0,

    /// <summary><c>QUERY_NOT_YET</c>: a space is <c>%20</c> until the first <c>?</c>, then <c>+</c>.</summary>
    NotYet = 1,

    /// <summary><c>QUERY_YES</c>: the text is a query, so a space is <c>+</c> throughout.</summary>
    Yes = 2,
}
