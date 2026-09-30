namespace Curl.Core.Multipart;

/// <summary>
/// How a part's name and file name are escaped inside the quotes of its
/// <c>Content-Disposition</c>, as libcurl 8.21.0's <c>escape_string</c> escapes them.
/// </summary>
public enum MultipartNameEscaping
{
    /// <summary>
    /// curl's default, the WHATWG form rule: <c>"</c> becomes <c>%22</c>, CR <c>%0D</c> and LF
    /// <c>%0A</c>; a backslash is sent as it is.
    /// </summary>
    Percent = 0,

    /// <summary>
    /// <c>--form-escape</c>: <c>\</c> becomes <c>\\</c> and <c>"</c> becomes <c>\"</c>; CR and LF
    /// are sent as they are.
    /// </summary>
    Backslash,
}
