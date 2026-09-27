namespace Curl.Protocol.Abstractions;

/// <summary>
/// How existing content is treated when a file is opened for writing.
/// </summary>
/// <remarks>
/// curl chooses among the three: a plain download to a file truncates,
/// <c>-C</c>/<c>--continue-at</c> appends to what is already there, and a
/// <c>-J</c>/<c>--remote-header-name</c> file is created only when nothing is there, as
/// curl's <c>O_EXCL</c> open does. The choice belongs
/// to the caller rather than to <see cref="IFileSystem" />, so a test can assert which
/// mode a handler asked for without inspecting a file on disk.
/// </remarks>
public enum FileWriteMode
{
    /// <summary>Discard any existing content, creating the file when it does not exist.</summary>
    Truncate = 0,

    /// <summary>Keep any existing content and position writes after the end of it.</summary>
    Append,

    /// <summary>
    /// Create the file, failing with <see cref="FileAccessStatus.AlreadyExists" /> and
    /// leaving its content untouched when one is already there. The check and the create
    /// are one operating-system call, so a file another process creates in between is
    /// never overwritten.
    /// </summary>
    CreateNew,
}
