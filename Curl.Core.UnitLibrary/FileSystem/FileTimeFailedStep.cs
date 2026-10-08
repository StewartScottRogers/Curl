namespace Curl.Core.FileSystem;

/// <summary>
/// The step at which <see cref="IFileTimeSetter.TrySetLastWriteUnixSeconds" /> failed, which
/// picks curl 8.21.0's Windows warning: <c>CreateFile failed</c> or <c>SetFileTime failed</c>
/// (BL-1453).
/// </summary>
public enum FileTimeFailedStep
{
    /// <summary>Nothing failed: the time was set.</summary>
    None,

    /// <summary>The file could not be opened, so its time was never set.</summary>
    Open,

    /// <summary>
    /// The file was opened, or needs no open, but the operating system refused the time:
    /// Windows' <c>SetFileTime</c> or POSIX <c>utimes</c>.
    /// </summary>
    SetTime,
}
