namespace Curl.Api.Options.Http;

/// <summary>
/// The <c>CURL_REDIR_POST_*</c> bits of <c>CURLOPT_POSTREDIR</c>: the redirect codes after
/// which a <c>POST</c> stays a <c>POST</c>. Each member's value is libcurl's bit.
/// </summary>
[Flags]
public enum PostRedirect
{
    /// <summary>
    /// <c>CURL_REDIR_GET_ALL</c> (0): a <c>POST</c> becomes a <c>GET</c> after a 301, 302 or
    /// 303; libcurl's default.
    /// </summary>
    None = 0,

    /// <summary><c>CURL_REDIR_POST_301</c> (1): a <c>POST</c> stays a <c>POST</c> after a 301.</summary>
    Post301 = 1 << 0,

    /// <summary><c>CURL_REDIR_POST_302</c> (2): a <c>POST</c> stays a <c>POST</c> after a 302.</summary>
    Post302 = 1 << 1,

    /// <summary><c>CURL_REDIR_POST_303</c> (4): a <c>POST</c> stays a <c>POST</c> after a 303.</summary>
    Post303 = 1 << 2,

    /// <summary><c>CURL_REDIR_POST_ALL</c> (7): all three.</summary>
    All = Post301 | Post302 | Post303,
}
