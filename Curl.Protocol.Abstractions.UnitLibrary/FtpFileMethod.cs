namespace Curl.Protocol.Abstractions;

/// <summary>
/// How an FTP transfer reaches the file in the URL's path, per <c>--ftp-method</c>
/// (ADR-0006).
/// </summary>
/// <remarks>
/// curl 8.21.0 reads <c>multicwd</c>, <c>nocwd</c> and <c>singlecwd</c> without regard to
/// case, and warns about any other value and uses <see cref="MultiCwd" />.
/// </remarks>
public enum FtpFileMethod
{
    /// <summary>
    /// The default, <c>multicwd</c>: one <c>CWD</c> for each directory in the path, then the
    /// file name on its own.
    /// </summary>
    MultiCwd = 0,

    /// <summary>
    /// <c>nocwd</c>: no <c>CWD</c>; each command names the full path.
    /// </summary>
    NoCwd,

    /// <summary>
    /// <c>singlecwd</c>: one <c>CWD</c> to the whole directory, then the file name on its own.
    /// </summary>
    SingleCwd,
}
