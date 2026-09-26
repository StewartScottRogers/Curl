namespace Curl.Protocol.Abstractions;

/// <summary>
/// How existing content is treated when a file is opened for writing.
/// </summary>
/// <remarks>
/// curl chooses between the two: a plain download to a file truncates, and
/// <c>-C</c>/<c>--continue-at</c> appends to what is already there. The choice belongs
/// to the caller rather than to <see cref="IFileSystem" />, so a test can assert which
/// mode a handler asked for without inspecting a file on disk.
/// </remarks>
public enum FileWriteMode
{
    /// <summary>Discard any existing content, creating the file when it does not exist.</summary>
    Truncate = 0,

    /// <summary>Keep any existing content and position writes after the end of it.</summary>
    Append,
}
