namespace Curl.Protocol.Abstractions;

/// <summary>
/// Why an attempt to open a local file succeeded or failed.
/// </summary>
/// <remarks>
/// These are the distinctions curl's exit code and message depend on, and no more. A
/// missing file has to be told apart from a directory, because curl prints different
/// text for each while reporting the same exit 37
/// (<see cref="CurlExitCode.FileCouldntReadFile" />) for a source it cannot read,
/// where a failed <em>destination</em> open is exit 23
/// (<see cref="CurlExitCode.WriteError" />). A bare boolean would lose that, and a
/// thrown <see cref="IOException" /> would force every fake in a test to know which
/// exception type the real file system happens to raise.
/// </remarks>
public enum FileAccessStatus
{
    /// <summary>The file was opened and <see cref="FileOpenResult.Content" /> is usable.</summary>
    Ok = 0,

    /// <summary>No file exists at the requested path, or a parent directory is missing.</summary>
    NotFound,

    /// <summary>The path names a directory rather than a file.</summary>
    IsDirectory,

    /// <summary>The operating system refused access to a path that exists.</summary>
    AccessDenied,

    /// <summary>The open failed for any other reason the operating system reported.</summary>
    IoError,
}
