namespace Curl.Protocol.Abstractions;

/// <summary>
/// The HTTP version a request line asks for (ADR-0014).
/// </summary>
/// <remarks>
/// <see cref="Http11" /> is first so that the default value is curl's default. Values for
/// HTTP/2 and HTTP/3 will be appended by a later ADR when a task implements them.
/// </remarks>
public enum HttpVersionPreference
{
    /// <summary>
    /// HTTP/1.1: the default, and <c>--http1.1</c>.
    /// </summary>
    Http11 = 0,

    /// <summary>
    /// HTTP/1.0, per <c>-0</c>/<c>--http1.0</c>.
    /// </summary>
    Http10,
}
